using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Infrastructure.Edhrec;
using EdhDeckBuilder.Infrastructure.Scryfall;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace EdhDeckBuilder.Tests.Infrastructure;

public sealed class SuggestionSourceTests
{
    // ---  happy path ----------------------------------------------------------

    [Fact]
    public async Task GetRecommendationsAsync_returns_candidates_ordered_by_inclusion_descending()
    {
        using var dir = StandardDir();
        var (source, korvold) = await CreateAsync(dir);

        var candidates = await source.GetRecommendationsAsync(korvold);

        // Fixture: Sol Ring appears at 0.85 (High Synergy), Cultivate at 0.60 (Ramp)
        Assert.Equal(2, candidates.Count);
        Assert.Equal("Sol Ring",  candidates[0].Card.Name);
        Assert.Equal("Cultivate", candidates[1].Card.Name);
    }

    [Fact]
    public async Task GetRecommendationsAsync_deduplicates_card_keeping_highest_inclusion_section()
    {
        using var dir = StandardDir();
        var (source, korvold) = await CreateAsync(dir);

        var candidates = await source.GetRecommendationsAsync(korvold);

        // Sol Ring is in Ramp (0.80) AND High Synergy (0.85) — High Synergy wins
        var solRing = candidates.Single(c => c.Card.Name == "Sol Ring");
        Assert.Equal("High Synergy Cards", solRing.Section);
        Assert.Equal(0.85, solRing.Inclusion, precision: 2);
    }

    [Fact]
    public async Task GetRecommendationsAsync_inclusion_rate_matches_num_decks_over_potential_decks()
    {
        using var dir = StandardDir();
        var (source, korvold) = await CreateAsync(dir);

        var candidates = await source.GetRecommendationsAsync(korvold);

        var cultivate = candidates.Single(c => c.Card.Name == "Cultivate");
        Assert.Equal(0.60, cultivate.Inclusion, precision: 2);  // 600 / 1000
    }

    // --- edge cases -----------------------------------------------------------

    [Fact]
    public async Task GetRecommendationsAsync_skips_cards_not_in_repository()
    {
        // EDHREC page adds "Mox Emerald" which is not in the Scryfall fixture
        const string pageWithUnknown = """
            {
              "container": {
                "json_dict": {
                  "cardlists": [
                    {
                      "header": "Ramp",
                      "tag": "ramp",
                      "cardviews": [
                        { "name": "Sol Ring",   "num_decks": 800, "potential_decks": 1000, "synergy": 0.10 },
                        { "name": "Mox Emerald","num_decks": 900, "potential_decks": 1000, "synergy": 0.50 }
                      ]
                    }
                  ]
                }
              }
            }
            """;

        using var dir = StandardDir(overrideCommanderPage: pageWithUnknown);
        var (source, korvold) = await CreateAsync(dir);

        var candidates = await source.GetRecommendationsAsync(korvold);

        Assert.DoesNotContain(candidates, c => c.Card.Name == "Mox Emerald");
        Assert.Contains(candidates, c => c.Card.Name == "Sol Ring");
    }

    [Fact]
    public async Task GetRecommendationsAsync_returns_empty_when_edhrec_has_no_page()
    {
        using var dir = StandardDir(writeCommanderPage: false);
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var (source, korvold) = await CreateAsync(dir, edhrecHttp: new HttpClient(handler));

        var candidates = await source.GetRecommendationsAsync(korvold);

        Assert.Empty(candidates);
    }

    // --- average deck ---------------------------------------------------------

    [Fact]
    public async Task GetAverageDeckAsync_returns_candidates_from_avg_cache_file()
    {
        using var dir = StandardDir();
        var (source, korvold) = await CreateAsync(dir);

        var candidates = await source.GetAverageDeckAsync(korvold);

        Assert.Equal(2, candidates.Count);
        Assert.Equal("Sol Ring", candidates[0].Card.Name);
    }

    [Fact]
    public async Task GetAverageDeckAsync_returns_empty_when_avg_page_missing()
    {
        using var dir = StandardDir(writeAvgPage: false);
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var (source, korvold) = await CreateAsync(dir, edhrecHttp: new HttpClient(handler));

        var candidates = await source.GetAverageDeckAsync(korvold);

        Assert.Empty(candidates);
    }

    // --- helpers --------------------------------------------------------------

    /// <summary>
    /// Creates a temp directory pre-populated with the standard fixtures.
    /// Parameters control which optional files are written, for edge-case tests.
    /// </summary>
    private static TempDir StandardDir(
        string? overrideCommanderPage = null,
        bool    writeCommanderPage    = true,
        bool    writeAvgPage          = true)
    {
        var dir = new TempDir();
        dir.WriteFile("oracle_cards.json", Fixtures.ScryfallOracleCards);
        if (writeCommanderPage)
            dir.WriteFile("korvold-fae-cursed-king.json", overrideCommanderPage ?? Fixtures.EdhrecKorvoldPage);
        if (writeAvgPage)
            dir.WriteFile("avg-korvold-fae-cursed-king.json", Fixtures.EdhrecKorvoldPage);
        return dir;
    }

    /// <summary>
    /// Builds a fully wired SuggestionSource from the given temp directory.
    /// Returns both the source and the Korvold card (loaded from the fixture repository)
    /// so tests can pass it to GetRecommendationsAsync / GetAverageDeckAsync.
    /// </summary>
    private static async Task<(SuggestionSource Source, Card Korvold)> CreateAsync(
        TempDir dir, HttpClient? edhrecHttp = null)
    {
        var scryfallOpts = Options.Create(new ScryfallOptions
        {
            CacheDirectory = dir.Path,
            CacheMaxAge    = TimeSpan.FromHours(24),
        });
        var bulk = new ScryfallBulkClient(new HttpClient(), scryfallOpts, NullLogger<ScryfallBulkClient>.Instance);
        var repo = new CardRepository(bulk, NullLogger<CardRepository>.Instance);

        var edhrecOpts = Options.Create(new EdhrecOptions
        {
            CacheDirectory = dir.Path,
            CacheMaxAge    = TimeSpan.FromDays(7),
        });
        var edhrec = new EdhrecClient(
            edhrecHttp ?? new HttpClient(),
            edhrecOpts,
            NullLogger<EdhrecClient>.Instance);

        var source  = new SuggestionSource(edhrec, repo, NullLogger<SuggestionSource>.Instance);
        var korvold = await repo.GetByNameAsync("Korvold, Fae-Cursed King")
            ?? throw new InvalidOperationException("Korvold not in fixture — check Fixtures.ScryfallOracleCards");

        return (source, korvold);
    }
}
