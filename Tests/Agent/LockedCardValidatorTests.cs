using EdhDeckBuilder.Agent;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Tests.Agent;

/// <summary>
/// Tests for <see cref="LockedCardValidator"/>: name resolution, color identity checks,
/// blank/duplicate handling.
/// </summary>
public sealed class LockedCardValidatorTests
{
    // ── Mock ──────────────────────────────────────────────────────────────────

    private static Card MakeCard(string name, Color ci = Color.Green) => new()
    {
        ScryfallId        = Guid.NewGuid(),
        OracleId          = Guid.NewGuid(),
        Name              = name,
        ColorIdentity     = ci,
        ManaCost          = "",
        TypeLine          = "Instant",
        OracleText        = "",
        CommanderLegality = Legality.Legal,
    };

    private sealed class StubRepository : ICardRepository
    {
        private readonly Dictionary<string, Card> _byName;
        public StubRepository(IEnumerable<Card> cards) =>
            _byName = cards.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        public Task<Card?> GetByNameAsync(string name, CancellationToken ct = default)
            => Task.FromResult(_byName.TryGetValue(name, out var c) ? c : (Card?)null);

        public Task<Card?> GetByOracleIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Card?>(null);
        public Task<Card?> GetByScryfallIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Card?>(null);
        public Task<IReadOnlyList<Card>> SearchAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Card>>([]);
        public Task<IReadOnlyList<Card>> GetCommandersAsync(Color? colorFilter, bool exactMatch, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Card>>([]);
        public Task<IReadOnlyList<EdhDeckBuilder.Core.Partnerships.PartnerCombo>> GetPartnerCombosAsync(
            Color? colorFilter, bool exactMatch, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EdhDeckBuilder.Core.Partnerships.PartnerCombo>>([]);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnknownName_IsInUnrecognizedNames()
    {
        var repo = new StubRepository([]);
        var sut  = new LockedCardValidator(repo);

        var result = await sut.ValidateAsync(["Nonexistent Card"], Color.Green);

        Assert.Single(result.UnrecognizedNames, "Nonexistent Card");
        Assert.Empty(result.ValidCards);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public async Task KnownCardWithinColorIdentity_IsInValidCards()
    {
        var sol = MakeCard("Sol Ring", Color.None);  // colorless — legal in any deck
        var repo = new StubRepository([sol]);
        var sut  = new LockedCardValidator(repo);

        var result = await sut.ValidateAsync(["Sol Ring"], Color.Green);

        Assert.Single(result.ValidCards);
        Assert.Empty(result.UnrecognizedNames);
        Assert.Empty(result.WrongColorCards);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public async Task KnownCardOutsideColorIdentity_IsInWrongColorCards()
    {
        var bolts = MakeCard("Lightning Bolt", Color.Red);
        var repo  = new StubRepository([bolts]);
        var sut   = new LockedCardValidator(repo);

        var result = await sut.ValidateAsync(["Lightning Bolt"], Color.Green);

        Assert.Single(result.WrongColorCards);
        Assert.Empty(result.ValidCards);
        Assert.Empty(result.UnrecognizedNames);
        Assert.False(result.HasErrors);  // wrong color is a warning, not a blocking error
    }

    [Fact]
    public async Task BlankLines_AreIgnored()
    {
        var repo = new StubRepository([]);
        var sut  = new LockedCardValidator(repo);

        var result = await sut.ValidateAsync(["", "  ", "\t"], Color.Green);

        Assert.Empty(result.UnrecognizedNames);
        Assert.Empty(result.ValidCards);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public async Task DuplicateNames_AreDeduplicatedBeforeLookup()
    {
        var sol  = MakeCard("Sol Ring", Color.None);
        var repo = new StubRepository([sol]);
        var sut  = new LockedCardValidator(repo);

        var result = await sut.ValidateAsync(["Sol Ring", "Sol Ring", "SOL RING"], Color.Green);

        Assert.Single(result.ValidCards);
    }

    [Fact]
    public async Task MixedInput_SortedIntoCorrectBuckets()
    {
        var sol  = MakeCard("Sol Ring", Color.None);
        var bolt = MakeCard("Lightning Bolt", Color.Red);
        var repo = new StubRepository([sol, bolt]);
        var sut  = new LockedCardValidator(repo);

        var result = await sut.ValidateAsync(
            ["Sol Ring", "Lightning Bolt", "Ghost Card"],
            Color.Green);

        Assert.Single(result.ValidCards,        c => c.Name == "Sol Ring");
        Assert.Single(result.WrongColorCards,   c => c.Name == "Lightning Bolt");
        Assert.Single(result.UnrecognizedNames, n => n == "Ghost Card");
        Assert.True(result.HasErrors);
    }
}
