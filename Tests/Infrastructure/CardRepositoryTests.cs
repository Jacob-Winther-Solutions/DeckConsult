using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Infrastructure.Edhrec;
using EdhDeckBuilder.Infrastructure.Edhrec.Dto;
using EdhDeckBuilder.Infrastructure.Scryfall;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EdhDeckBuilder.Tests.Infrastructure;

public sealed class CardRepositoryTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly CardRepository _repo;

    public CardRepositoryTests()
    {
        // Write a fresh fixture (just created = current timestamp = within 24h = fresh cache).
        _dir.WriteFile("oracle_cards.json", Fixtures.ScryfallOracleCards);

        var opts = Options.Create(new ScryfallOptions
        {
            CacheDirectory = _dir.Path,
            CacheMaxAge    = TimeSpan.FromHours(24),
        });
        // HttpClient is never called — the fixture file is always fresh.
        var bulk = new ScryfallBulkClient(new HttpClient(), opts, NullLogger<ScryfallBulkClient>.Instance);

        // Create a mock EdhrecClient that returns test partnership data
        var mockEdhrec = new MockEdhrecClient();
        _repo = new CardRepository(bulk, mockEdhrec, NullLogger<CardRepository>.Instance);
    }

    public void Dispose() => _dir.Dispose();

    // --- GetByNameAsync -------------------------------------------------------

    [Fact]
    public async Task GetByNameAsync_returns_card_for_exact_name()
    {
        var card = await _repo.GetByNameAsync("Sol Ring");
        Assert.NotNull(card);
        Assert.Equal("Sol Ring", card.Name);
    }

    [Fact]
    public async Task GetByNameAsync_is_case_insensitive()
    {
        var card = await _repo.GetByNameAsync("sol ring");
        Assert.NotNull(card);
        Assert.Equal("Sol Ring", card.Name);
    }

    [Fact]
    public async Task GetByNameAsync_returns_null_for_unknown_card()
    {
        var card = await _repo.GetByNameAsync("Mox Emerald");
        Assert.Null(card);
    }

    [Fact]
    public async Task GetByNameAsync_trims_leading_and_trailing_whitespace()
    {
        var card = await _repo.GetByNameAsync("  Sol Ring  ");
        Assert.NotNull(card);
    }

    // --- GetByOracleIdAsync ---------------------------------------------------

    [Fact]
    public async Task GetByOracleIdAsync_returns_card_for_known_id()
    {
        var card = await _repo.GetByOracleIdAsync(Fixtures.KorvoldId);
        Assert.NotNull(card);
        Assert.Equal("Korvold, Fae-Cursed King", card.Name);
    }

    [Fact]
    public async Task GetByOracleIdAsync_returns_null_for_unknown_id()
    {
        var card = await _repo.GetByOracleIdAsync(Guid.NewGuid());
        Assert.Null(card);
    }

    // --- SearchAsync ----------------------------------------------------------

    [Fact]
    public async Task SearchAsync_returns_cards_containing_query()
    {
        // "Ring" matches "Sol Ring" only
        var results = await _repo.SearchAsync("Ring");
        Assert.Single(results);
        Assert.Equal("Sol Ring", results[0].Name);
    }

    [Fact]
    public async Task SearchAsync_is_case_insensitive()
    {
        var results = await _repo.SearchAsync("korvold");
        Assert.Single(results);
        Assert.Equal("Korvold, Fae-Cursed King", results[0].Name);
    }

    [Fact]
    public async Task SearchAsync_returns_results_ordered_by_name()
    {
        // "r" matches many cards: Drana, Dranarator, Forest, Korvold, Rograkh, Sol Ring, Thrasios, Tymna
        // Just verify results are ordered alphabetically
        var results = await _repo.SearchAsync("r");
        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(string.Compare(results[i - 1].Name, results[i].Name, StringComparison.Ordinal) < 0,
                $"Results not ordered: {results[i - 1].Name} >= {results[i].Name}");
        }
    }

    [Fact]
    public async Task SearchAsync_returns_empty_list_for_no_match()
    {
        var results = await _repo.SearchAsync("Mox Diamond");
        Assert.Empty(results);
    }

    // --- field mapping --------------------------------------------------------

    [Fact]
    public async Task Korvold_is_mapped_correctly()
    {
        var card = await _repo.GetByNameAsync("Korvold, Fae-Cursed King");
        Assert.NotNull(card);
        Assert.Equal(Fixtures.KorvoldId,               card.OracleId);
        Assert.Equal(Color.Black | Color.Red | Color.Green, card.ColorIdentity);
        Assert.Equal(5m,                               card.ManaValue);
        Assert.True(card.IsLegendary);
        Assert.True(card.CanBeCommander);
        Assert.True(card.Types.HasFlag(CardType.Creature));
        Assert.Equal("5", card.Power);
        Assert.Equal("5", card.Toughness);
        Assert.Equal(Legality.Legal, card.CommanderLegality);
    }

    [Fact]
    public async Task Forest_is_mapped_as_basic_land()
    {
        var card = await _repo.GetByNameAsync("Forest");
        Assert.NotNull(card);
        Assert.True(card.IsBasicLand);
        Assert.True(card.Types.HasFlag(CardType.Land));
        Assert.Equal(Color.None, card.ColorIdentity);
        Assert.False(card.CanBeCommander);
    }

    [Fact]
    public async Task Sol_Ring_is_mapped_as_colorless_artifact()
    {
        var card = await _repo.GetByNameAsync("Sol Ring");
        Assert.NotNull(card);
        Assert.True(card.Types.HasFlag(CardType.Artifact));
        Assert.Equal(Color.None, card.ColorIdentity);
        Assert.Equal(1m, card.ManaValue);
        Assert.False(card.CanBeCommander);
    }

    // --- GetPartnerCombosAsync ------------------------------------------------

    [Fact]
    public async Task GetPartnerCombosAsync_returns_generic_partner_pairs()
    {
        var combos = await _repo.GetPartnerCombosAsync();

        // Thrasios (G/U) + Tymna (W/B) is a valid partner pair
        var thrasiosTymna = combos.FirstOrDefault(c =>
            (c.FirstCardId == Fixtures.ThrasiosId && c.SecondCardId == Fixtures.TymnaId)
            || (c.FirstCardId == Fixtures.TymnaId && c.SecondCardId == Fixtures.ThrasiosId));

        Assert.NotNull(thrasiosTymna);
    }

    [Fact]
    public async Task GetPartnerCombosAsync_returns_partner_with_specific_pairs()
    {
        var combos = await _repo.GetPartnerCombosAsync();

        // Rograkh + Drana is a "partner with" specific pair
        var rograkDrana = combos.FirstOrDefault(c =>
            (c.FirstCardId == Fixtures.RograkId && c.SecondCardId == Fixtures.DranaId)
            || (c.FirstCardId == Fixtures.DranaId && c.SecondCardId == Fixtures.RograkId));

        Assert.NotNull(rograkDrana);
    }

    [Fact]
    public async Task GetPartnerCombosAsync_filters_by_color_identity()
    {
        // Thrasios (G/U) + Tymna (W/B) = W/U/B/G (4-color)
        var combos = await _repo.GetPartnerCombosAsync(Color.White | Color.Blue | Color.Black | Color.Green);

        var thrasiosTymna = combos.FirstOrDefault(c =>
            (c.FirstCardId == Fixtures.ThrasiosId && c.SecondCardId == Fixtures.TymnaId)
            || (c.FirstCardId == Fixtures.TymnaId && c.SecondCardId == Fixtures.ThrasiosId));

        Assert.NotNull(thrasiosTymna);
    }

    [Fact]
    public async Task GetPartnerCombosAsync_exact_match_enforces_strict_identity()
    {
        // Thrasios (G/U) + Tymna (W/B) = W/U/B/G
        // exactMatch=true should only return if combined identity == W/U/B/G exactly
        var combos = await _repo.GetPartnerCombosAsync(
            Color.White | Color.Blue | Color.Black | Color.Green,
            exactMatch: true);

        var thrasiosTymna = combos.FirstOrDefault(c =>
            (c.FirstCardId == Fixtures.ThrasiosId && c.SecondCardId == Fixtures.TymnaId)
            || (c.FirstCardId == Fixtures.TymnaId && c.SecondCardId == Fixtures.ThrasiosId));

        Assert.NotNull(thrasiosTymna);

        // But if we ask for exact U/R/G, it should not match (since it's W/U/B/G)
        var combo5ColorOnly = await _repo.GetPartnerCombosAsync(
            Color.White | Color.Blue | Color.Red | Color.Black | Color.Green,
            exactMatch: true);

        // The combo should not be in this result because combined identity is W/U/B/G, not W/U/B/R/G
        var notFound = combo5ColorOnly.FirstOrDefault(c =>
            (c.FirstCardId == Fixtures.ThrasiosId && c.SecondCardId == Fixtures.TymnaId)
            || (c.FirstCardId == Fixtures.TymnaId && c.SecondCardId == Fixtures.ThrasiosId));

        Assert.Null(notFound);
    }

    [Fact]
    public async Task GetPartnerCombosAsync_returns_empty_when_no_filter_match()
    {
        // Ask for partners with Red + Blue only (R/U) — no combos should match
        var combos = await _repo.GetPartnerCombosAsync(Color.Red | Color.Blue, exactMatch: true);
        Assert.Empty(combos);
    }

    [Fact]
    public async Task GetPartnerCombosAsync_null_filter_returns_all_combos()
    {
        var combos = await _repo.GetPartnerCombosAsync(colorFilter: null);

        // Should include at least Thrasios+Tymna and Rograkh+Drana
        Assert.NotEmpty(combos);
        Assert.True(combos.Count >= 2);
    }

    // ── Mock EdhrecClient ────────────────────────────────────────────────────────

    private sealed class MockEdhrecClient : IEdhrecClient
    {
        public Task<EdhrecPage?> GetCommanderPageAsync(string slug, CancellationToken ct = default)
            => Task.FromResult<EdhrecPage?>(null);

        public Task<EdhrecPage?> GetAverageDeckPageAsync(string slug, CancellationToken ct = default)
            => Task.FromResult<EdhrecPage?>(null);

        public Task<EdhrecPartnerPage?> GetPartnersPageAsync(CancellationToken ct = default)
        {
            // Return test partnership data with Thrasios+Tymna and Rograkh+Drana
            var page = new EdhrecPartnerPage
            {
                Container = new EdhrecPartnerContainer
                {
                    JsonDict = new EdhrecPartnerJsonDict
                    {
                        Cardlists =
                        [
                            // Generic Partners cardlist (Thrasios + Tymna)
                            new EdhrecPartnerCardlist
                            {
                                Header = "Partners",
                                Tag = "partners",
                                Cardviews =
                                [
                                    new EdhrecPartnerCardView { Name = "Thrasios, Triton Hero", Id = Fixtures.ThrasiosId.ToString() },
                                    new EdhrecPartnerCardView { Name = "Tymna the Weaver", Id = Fixtures.TymnaId.ToString() },
                                ]
                            },
                            // Partner with cardlist: each entry is "Card1 // Card2" (real EDHREC format)
                            new EdhrecPartnerCardlist
                            {
                                Header = "Partner with",
                                Tag = "partnerwith",
                                Cardviews =
                                [
                                    new EdhrecPartnerCardView { Name = "Rograkh, Son of Rohgadh // Drana, Liberator of Zendikar" },
                                ]
                            },
                        ]
                    }
                }
            };

            return Task.FromResult<EdhrecPartnerPage?>(page);
        }

        public Task<EdhrecPage?> GetPartnerPairRecommendationsAsync(string firstSlug, string secondSlug, CancellationToken ct = default)
            => Task.FromResult<EdhrecPage?>(null);  // Mock returns null; real implementation tested separately
    }
}
