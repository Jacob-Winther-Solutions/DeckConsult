using System.Text.Json;
using EdhDeckBuilder.Infrastructure.Edhrec.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdhDeckBuilder.Infrastructure.Edhrec;

internal sealed class EdhrecClient(
    HttpClient http,
    IOptions<EdhrecOptions> options,
    ILogger<EdhrecClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private const string BaseUrl = "https://json.edhrec.com/pages";

    public Task<EdhrecPage?> GetCommanderPageAsync(string slug, CancellationToken ct = default)
        => FetchPageAsync($"commanders/{slug}", slug, ct);

    public Task<EdhrecPage?> GetAverageDeckPageAsync(string slug, CancellationToken ct = default)
        => FetchPageAsync($"average-decks/{slug}", $"avg-{slug}", ct);

    private async Task<EdhrecPage?> FetchPageAsync(string path, string cacheKey, CancellationToken ct)
    {
        var opts = options.Value;
        Directory.CreateDirectory(opts.CacheDirectory);
        var cachePath = Path.Combine(opts.CacheDirectory, $"{cacheKey}.json");

        if (!IsCacheFresh(cachePath, opts.CacheMaxAge))
        {
            var url = $"{BaseUrl}/{path}.json";
            logger.LogInformation("Fetching EDHREC page from {Url}", url);
            try
            {
                var content = await http.GetStringAsync(url, ct);
                await File.WriteAllTextAsync(cachePath, content, ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                logger.LogWarning("EDHREC has no page for {Path}", path);
                return null;
            }
        }

        await using var stream = File.OpenRead(cachePath);
        return await JsonSerializer.DeserializeAsync<EdhrecPage>(stream, JsonOptions, ct);
    }

    private static bool IsCacheFresh(string path, TimeSpan maxAge)
        => File.Exists(path) && (DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(path)) < maxAge;
}
