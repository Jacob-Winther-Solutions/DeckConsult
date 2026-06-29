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

    private const string BulkDataUrl    = "https://api.scryfall.com/bulk-data";
    private const string OracleCardsType = "oracle_cards";
    private const string CacheFileName  = "oracle_cards.json";

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

        var downloadUri = await ResolveDownloadUriAsync(ct);
        logger.LogInformation("Downloading Scryfall oracle cards from {Uri}", downloadUri);

        await using var response = await http.GetStreamAsync(downloadUri, ct);
        await using var file = File.Create(cachePath);
        await response.CopyToAsync(file, ct);

        logger.LogInformation("Saved Scryfall oracle cards to {Path}", cachePath);
        return cachePath;
    }

    private async Task<string> ResolveDownloadUriAsync(CancellationToken ct)
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

        return list.Data.FirstOrDefault(e => e.Type == OracleCardsType)?.DownloadUri
            ?? throw new InvalidOperationException($"No '{OracleCardsType}' entry in Scryfall bulk-data manifest.");
    }

    private static bool IsCacheFresh(string path, TimeSpan maxAge)
        => File.Exists(path) && (DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(path)) < maxAge;
}
