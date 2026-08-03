using System.IO.Compression;
using System.Text;
using System.Text.Json;
using EdhDeckBuilder.Infrastructure.Scryfall.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdhDeckBuilder.Infrastructure.Scryfall;

internal sealed class ScryfallBulkClient(
    HttpClient http,
    IOptions<ScryfallOptions> options,
    ILogger<ScryfallBulkClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private const string BulkDataUrl     = "https://api.scryfall.com/bulk-data";
    private const string OracleCardsType  = "oracle_cards";
    private const string CacheFileName   = "oracle_cards.json";

    public async Task<string> GetOracleCardsFileAsync(CancellationToken ct = default)
    {
        var opts = options.Value;
        Directory.CreateDirectory(opts.CacheDirectory);
        var cachePath = Path.Combine(opts.CacheDirectory, CacheFileName);

        if (IsCacheFresh(cachePath, opts.CacheMaxAge))
        {
            logger.LogDebug("Using cached Scryfall oracle cards at {Path}", cachePath);
            return cachePath;
        }

        var (downloadUri, isJsonl) = await ResolveDownloadUriAsync(ct);
        logger.LogInformation("Downloading Scryfall oracle cards ({Format}) from {Uri}",
            isJsonl ? "JSONL.gz" : "JSON", downloadUri);

        await DownloadAndSaveAsync(downloadUri, isJsonl, cachePath, ct);

        logger.LogInformation("Saved Scryfall oracle cards to {Path}", cachePath);
        return cachePath;
    }

    private async Task DownloadAndSaveAsync(string uri, bool isJsonl, string cachePath, CancellationToken ct)
    {
        await using var response = await http.GetStreamAsync(uri, ct);

        if (!isJsonl)
        {
            await using var jsonFile = File.Create(cachePath);
            await response.CopyToAsync(jsonFile, ct);
            return;
        }

        // JSONL.gz — decompress and convert to a JSON array so CardRepository can continue to
        // use JsonSerializer.DeserializeAsyncEnumerable<ScryfallCard>, which expects a JSON array.
        await using var gzip = new GZipStream(response, CompressionMode.Decompress);
        using  var reader    = new StreamReader(gzip, Encoding.UTF8);
        await using var file = File.Create(cachePath);
        await using var writer = new StreamWriter(file, Encoding.UTF8, leaveOpen: true);

        await writer.WriteAsync("[\n");
        bool first = true;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!first) await writer.WriteAsync(",\n");
            first = false;
            await writer.WriteAsync(line);
        }
        await writer.WriteAsync("\n]");
    }

    private async Task<(string Uri, bool IsJsonl)> ResolveDownloadUriAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync(BulkDataUrl, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Scryfall bulk-data manifest returned {(int)response.StatusCode}: {body}",
                inner: null,
                response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var list = await JsonSerializer.DeserializeAsync<BulkDataList>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException("Scryfall bulk-data manifest was null.");

        var entry = list.Data.FirstOrDefault(e => e.Type == OracleCardsType)
            ?? throw new InvalidOperationException($"No '{OracleCardsType}' entry in Scryfall bulk-data manifest.");

        if (!string.IsNullOrWhiteSpace(entry.DownloadUri))
            return (entry.DownloadUri, IsJsonl: false);

        if (!string.IsNullOrWhiteSpace(entry.JsonlDownloadUri))
            return (entry.JsonlDownloadUri, IsJsonl: true);

        throw new InvalidOperationException(
            $"Scryfall bulk-data manifest has no download URI for '{OracleCardsType}'. " +
            $"Scryfall may have changed their API format.");
    }

    private static bool IsCacheFresh(string path, TimeSpan maxAge)
        => File.Exists(path) && (DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(path)) < maxAge;
}
