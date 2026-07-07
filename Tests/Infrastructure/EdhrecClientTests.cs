using EdhDeckBuilder.Infrastructure.Edhrec;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace EdhDeckBuilder.Tests.Infrastructure;

public sealed class EdhrecClientTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task GetCommanderPageAsync_returns_deserialized_page_from_fresh_cache()
    {
        _dir.WriteFile("korvold-fae-cursed-king.json", Fixtures.EdhrecKorvoldPage);

        var client = BuildClient(new HttpClient());  // never called — cache is fresh
        var page = await client.GetCommanderPageAsync("korvold-fae-cursed-king");

        Assert.NotNull(page);
        var cardlists = page.Container?.JsonDict?.Cardlists;
        Assert.NotNull(cardlists);
        Assert.Equal(2, cardlists.Count);
    }

    [Fact]
    public async Task GetCommanderPageAsync_fetches_writes_and_returns_page_when_stale()
    {
        bool fetched = false;
        var handler = new FakeHttpHandler(_ =>
        {
            fetched = true;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Fixtures.EdhrecKorvoldPage, Encoding.UTF8, "application/json"),
            };
        });

        var client = BuildClient(new HttpClient(handler));
        var page = await client.GetCommanderPageAsync("korvold-fae-cursed-king");

        Assert.True(fetched);
        Assert.NotNull(page);
        // The response must also be persisted so the next call hits cache
        Assert.True(File.Exists(_dir.FilePath("korvold-fae-cursed-king.json")));
    }

    [Fact]
    public async Task GetCommanderPageAsync_returns_null_when_edhrec_has_no_page()
    {
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = BuildClient(new HttpClient(handler));

        var page = await client.GetCommanderPageAsync("unknown-commander");

        Assert.Null(page);
    }

    [Fact]
    public async Task GetAverageDeckPageAsync_uses_different_cache_key_than_commander_page()
    {
        // Pre-populate both; they must live in separate files.
        _dir.WriteFile("korvold-fae-cursed-king.json",     Fixtures.EdhrecKorvoldPage);
        _dir.WriteFile("avg-korvold-fae-cursed-king.json", Fixtures.EdhrecKorvoldPage);

        var client = BuildClient(new HttpClient());

        var commander = await client.GetCommanderPageAsync("korvold-fae-cursed-king");
        var average   = await client.GetAverageDeckPageAsync("korvold-fae-cursed-king");

        Assert.NotNull(commander);
        Assert.NotNull(average);
        // If they shared a cache key, writing one would overwrite the other.
        Assert.True(File.Exists(_dir.FilePath("korvold-fae-cursed-king.json")));
        Assert.True(File.Exists(_dir.FilePath("avg-korvold-fae-cursed-king.json")));
    }

    // ── Partner pair recommendations ────────────────────────────────────────

    [Fact]
    public async Task GetPartnerPairRecommendationsAsync_returns_deserialized_page_from_fresh_cache()
    {
        _dir.WriteFile("thrasios-triton-hero-tymna-the-weaver-partner.json", Fixtures.EdhrecKorvoldPage);

        var client = BuildClient(new HttpClient());  // never called — cache is fresh
        var page = await client.GetPartnerPairRecommendationsAsync(
            "thrasios-triton-hero", "tymna-the-weaver");

        Assert.NotNull(page);
        var cardlists = page.Container?.JsonDict?.Cardlists;
        Assert.NotNull(cardlists);
        Assert.Equal(2, cardlists.Count);
    }

    [Fact]
    public async Task GetPartnerPairRecommendationsAsync_fetches_writes_and_returns_page_when_stale()
    {
        bool fetched = false;
        var handler = new FakeHttpHandler(_ =>
        {
            fetched = true;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Fixtures.EdhrecKorvoldPage, Encoding.UTF8, "application/json"),
            };
        });

        var client = BuildClient(new HttpClient(handler));
        var page = await client.GetPartnerPairRecommendationsAsync(
            "thrasios-triton-hero", "tymna-the-weaver");

        Assert.True(fetched);
        Assert.NotNull(page);
        // The response must also be persisted so the next call hits cache
        Assert.True(File.Exists(_dir.FilePath("thrasios-triton-hero-tymna-the-weaver-partner.json")));
    }

    [Fact]
    public async Task GetPartnerPairRecommendationsAsync_returns_null_for_nonexistent_pair()
    {
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = BuildClient(new HttpClient(handler));

        var page = await client.GetPartnerPairRecommendationsAsync(
            "sol-ring", "forest");

        Assert.Null(page);
    }

    [Fact]
    public async Task GetPartnerPairRecommendationsAsync_follows_redirect_to_canonical_ordering()
    {
        // Non-canonical order request (second-first) redirects to canonical (first-second)
        var redirectResponse = """
            {
              "redirect": "/commanders/thrasios-triton-hero-tymna-the-weaver"
            }
            """;

        var handler = new FakeHttpHandler(request =>
        {
            // First request: non-canonical order, return redirect
            if (request.RequestUri?.ToString().Contains("tymna-the-weaver-thrasios-triton-hero") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(redirectResponse, Encoding.UTF8, "application/json"),
                };
            }

            // Second request: canonical order, return page
            if (request.RequestUri?.ToString().Contains("thrasios-triton-hero-tymna-the-weaver") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Fixtures.EdhrecKorvoldPage, Encoding.UTF8, "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = BuildClient(new HttpClient(handler));
        var page = await client.GetPartnerPairRecommendationsAsync(
            "tymna-the-weaver", "thrasios-triton-hero");

        Assert.NotNull(page);
        Assert.NotEmpty(page.Container?.JsonDict?.Cardlists ?? []);
    }

    [Fact]
    public async Task GetPartnerPairRecommendationsAsync_caches_canonical_response_after_redirect()
    {
        // After redirect is followed, the canonical response is cached under canonical slug,
        // so both canonical and non-canonical requests use the same cache entry
        var redirectResponse = """
            {
              "redirect": "/commanders/first-canonical-second-canonical"
            }
            """;

        int fetchCount = 0;
        var handler = new FakeHttpHandler(request =>
        {
            fetchCount++;

            // Non-canonical request returns redirect
            if (request.RequestUri?.ToString().Contains("second-canonical-first-canonical") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(redirectResponse, Encoding.UTF8, "application/json"),
                };
            }

            // Canonical request returns page
            if (request.RequestUri?.ToString().Contains("first-canonical-second-canonical") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Fixtures.EdhrecKorvoldPage, Encoding.UTF8, "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = BuildClient(new HttpClient(handler));

        // First call: non-canonical order → fetches redirect check (1) + fetches canonical (2)
        var page1 = await client.GetPartnerPairRecommendationsAsync(
            "second-canonical", "first-canonical");
        Assert.NotNull(page1);
        Assert.Equal(2, fetchCount);  // Redirect check + canonical fetch

        // Reset fetch count for second assertion
        fetchCount = 0;

        // Second call: canonical order → redirect check (1) + cache hit (0 additional)
        var page2 = await client.GetPartnerPairRecommendationsAsync(
            "first-canonical", "second-canonical");
        Assert.NotNull(page2);
        Assert.Equal(1, fetchCount);  // Only redirect check, canonical is cached

        // Verify canonical cache file exists
        Assert.True(File.Exists(_dir.FilePath("first-canonical-second-canonical-partner.json")));
    }

    private EdhrecClient BuildClient(HttpClient http) => new(
        http,
        Options.Create(new EdhrecOptions { CacheDirectory = _dir.Path, CacheMaxAge = TimeSpan.FromDays(7) }),
        NullLogger<EdhrecClient>.Instance);
}
