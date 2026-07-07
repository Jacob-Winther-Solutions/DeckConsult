using System.Text.Json;
using EdhDeckBuilder.Infrastructure.Edhrec.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdhDeckBuilder.Infrastructure.Edhrec;

internal sealed class EdhrecClient : IEdhrecClient
{
    private readonly HttpClient _http;
    private readonly IOptions<EdhrecOptions> _options;
    private readonly ILogger<EdhrecClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private const string BaseUrl = "https://json.edhrec.com/pages";

    public EdhrecClient(HttpClient http, IOptions<EdhrecOptions> options, ILogger<EdhrecClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public Task<EdhrecPage?> GetCommanderPageAsync(string slug, CancellationToken ct = default)
        => FetchPageAsync($"commanders/{slug}", slug, ct);

    public Task<EdhrecPage?> GetAverageDeckPageAsync(string slug, CancellationToken ct = default)
        => FetchPageAsync($"average-decks/{slug}", $"avg-{slug}", ct);

    public async Task<EdhrecPartnerPage?> GetPartnersPageAsync(CancellationToken ct = default)
    {
        var opts = _options.Value;
        Directory.CreateDirectory(opts.CacheDirectory);
        var cachePath = Path.Combine(opts.CacheDirectory, "partners.json");

        if (!IsCacheFresh(cachePath, opts.CacheMaxAge))
        {
            var url = $"{BaseUrl}/partners.json";
            _logger.LogInformation("Fetching EDHREC partners page from {Url}", url);
            try
            {
                var content = await _http.GetStringAsync(url, ct);
                await File.WriteAllTextAsync(cachePath, content, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Failed to fetch EDHREC partners page: {StatusCode}", ex.StatusCode);
                return null;
            }
        }

        await using var stream = File.OpenRead(cachePath);
        return await JsonSerializer.DeserializeAsync<EdhrecPartnerPage>(stream, JsonOptions, ct);
    }

    public async Task<EdhrecPage?> GetPartnerPairRecommendationsAsync(string firstSlug, string secondSlug, CancellationToken ct = default)
    {
        var opts = _options.Value;
        Directory.CreateDirectory(opts.CacheDirectory);

        // Try the given order first; if redirect occurs, follow canonical path
        var (canonicalFirst, canonicalSecond) = await FetchPartnerPairWithRedirectAsync(firstSlug, secondSlug, opts, ct);

        if (canonicalFirst == null)
            return null;

        // Now fetch using canonical ordering
        var cachePath = Path.Combine(opts.CacheDirectory, $"{canonicalFirst}-{canonicalSecond}-partner.json");

        if (!IsCacheFresh(cachePath, opts.CacheMaxAge))
        {
            var url = $"{BaseUrl}/commanders/{canonicalFirst}-{canonicalSecond}.json";
            _logger.LogInformation("Fetching EDHREC partner-pair recommendations from {Url}", url);
            try
            {
                var content = await _http.GetStringAsync(url, ct);
                await File.WriteAllTextAsync(cachePath, content, ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("EDHREC has no partner-pair page for {First}-{Second}", canonicalFirst, canonicalSecond);
                return null;
            }
        }

        await using var stream = File.OpenRead(cachePath);
        return await JsonSerializer.DeserializeAsync<EdhrecPage>(stream, JsonOptions, ct);
    }

    private async Task<EdhrecPage?> FetchPageAsync(string path, string cacheKey, CancellationToken ct)
    {
        var opts = _options.Value;
        Directory.CreateDirectory(opts.CacheDirectory);
        var cachePath = Path.Combine(opts.CacheDirectory, $"{cacheKey}.json");

        if (!IsCacheFresh(cachePath, opts.CacheMaxAge))
        {
            var url = $"{BaseUrl}/{path}.json";
            _logger.LogInformation("Fetching EDHREC page from {Url}", url);
            try
            {
                var content = await _http.GetStringAsync(url, ct);
                await File.WriteAllTextAsync(cachePath, content, ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("EDHREC has no page for {Path}", path);
                return null;
            }
        }

        await using var stream = File.OpenRead(cachePath);
        return await JsonSerializer.DeserializeAsync<EdhrecPage>(stream, JsonOptions, ct);
    }

    private static bool IsCacheFresh(string path, TimeSpan maxAge)
        => File.Exists(path) && (DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(path)) < maxAge;

    private async Task<(string? FirstSlug, string? SecondSlug)> FetchPartnerPairWithRedirectAsync(
        string firstSlug,
        string secondSlug,
        EdhrecOptions opts,
        CancellationToken ct)
    {
        var url = $"{BaseUrl}/commanders/{firstSlug}-{secondSlug}.json";
        try
        {
            var content = await _http.GetStringAsync(url, ct);
            var doc = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);

            // Check for redirect
            if (doc.TryGetProperty("redirect", out var redirectProp) && redirectProp.ValueKind == JsonValueKind.String)
            {
                var redirectPath = redirectProp.GetString();
                if (!string.IsNullOrEmpty(redirectPath))
                {
                    // Extract canonical slugs from redirect path: "/commanders/slug1-slug2"
                    var slugs = ExtractSlugsFromPath(redirectPath);
                    if (slugs.First != null && slugs.Second != null)
                    {
                        _logger.LogInformation("Partner pair ({First}, {Second}) redirected to canonical ordering ({CanonicalFirst}, {CanonicalSecond})",
                            firstSlug, secondSlug, slugs.First, slugs.Second);
                        return (slugs.First, slugs.Second);
                    }
                }
            }

            // No redirect; use given order
            return (firstSlug, secondSlug);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("EDHREC partner pair not found: {First}-{Second}", firstSlug, secondSlug);
            return (null, null);
        }
    }

    private static (string? First, string? Second) ExtractSlugsFromPath(string path)
    {
        // Path format: "/commanders/slug1-slug2" or similar
        // Extract everything after "/commanders/" and split on the last hyphen
        const string prefix = "/commanders/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return (null, null);

        var slugPart = path[prefix.Length..];
        if (string.IsNullOrEmpty(slugPart))
            return (null, null);

        // Partner slugs are hyphen-separated. We need to find the split point.
        // Strategy: try to split from the right, knowing that most commander names
        // are short and the split is usually in the middle.
        // For now, simple heuristic: split on the rightmost hyphen that makes sense
        // (both sides non-empty).
        var lastHyphen = slugPart.LastIndexOf('-');
        if (lastHyphen <= 0 || lastHyphen >= slugPart.Length - 1)
            return (null, null);

        var first = slugPart[..lastHyphen];
        var second = slugPart[(lastHyphen + 1)..];

        return (first, second);
    }
}
