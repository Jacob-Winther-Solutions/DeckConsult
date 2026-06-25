using EdhDeckBuilder.Core.Cards;
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
        _repo = new CardRepository(bulk, NullLogger<CardRepository>.Instance);
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
        // "r" matches Forest, Korvold, Sol Ring — alphabetical: Forest < Korvold < Sol Ring
        var results = await _repo.SearchAsync("r");
        Assert.Equal(3, results.Count);
        Assert.True(string.Compare(results[0].Name, results[1].Name, StringComparison.Ordinal) < 0);
        Assert.True(string.Compare(results[1].Name, results[2].Name, StringComparison.Ordinal) < 0);
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
}
