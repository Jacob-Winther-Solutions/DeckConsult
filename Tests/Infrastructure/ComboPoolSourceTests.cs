using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Partnerships;
using EdhDeckBuilder.Infrastructure.Spellbook;
using Microsoft.Extensions.Logging.Abstractions;

namespace EdhDeckBuilder.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="ComboPoolSource"/>. Verifies score normalization,
/// multi-combo accumulation, and filtering of unresolvable card names.
/// </summary>
public sealed class ComboPoolSourceTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────

    private sealed class StubComboSource(ComboSearchResult result) : IComboSource
    {
        public Task<ComboSearchResult> FindCombosAsync(
            IReadOnlyList<string> _, IReadOnlyList<string> __, CancellationToken ___)
            => Task.FromResult(result);

        public Task<string?> EstimateBracketTagAsync(
            IReadOnlyList<string> _, IReadOnlyList<string> __, CancellationToken ___)
            => Task.FromResult<string?>(null);
    }

    private sealed class StubCardRepository(IReadOnlyDictionary<string, Card> byName) : ICardRepository
    {
        public Task<Card?> GetByNameAsync(string name, CancellationToken ct = default)
            => Task.FromResult(byName.TryGetValue(name, out var c) ? c : (Card?)null);

        public Task<Card?> GetByOracleIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Card?>(null);

        public Task<Card?> GetByScryfallIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Card?>(null);

        public Task<IReadOnlyList<Card>> SearchAsync(string q, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Card>>([]);

        public Task<IReadOnlyList<Card>> GetCommandersAsync(
            Color? colorFilter = null, bool exactMatch = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Card>>([]);

        public Task<IReadOnlyList<PartnerCombo>> GetPartnerCombosAsync(
            Color? colorFilter = null, bool exactMatch = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PartnerCombo>>([]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Card MakeCard(string name) => new()
    {
        ScryfallId        = Guid.NewGuid(),
        OracleId          = Guid.NewGuid(),
        Name              = name,
        TypeLine          = "Instant",
        Types             = CardType.Instant,
        ColorIdentity     = Color.None,
        CommanderLegality = Legality.Legal,
    };

    private static ComboVariant MakeVariant(string id, int popularity, IReadOnlyList<string> missingNames) =>
        new()
        {
            Id              = id,
            OwnedPieces     = [],
            MissingCardNames = missingNames,
            MissingTemplates = [],
            ProducedEffects = [],
            Description     = "Test combo.",
            Popularity      = popularity,
        };

    private static ComboPoolSource MakeSource(ComboSearchResult result, IReadOnlyDictionary<string, Card> cards) =>
        new(new StubComboSource(result),
            new StubCardRepository(cards),
            NullLogger<ComboPoolSource>.Instance);

    private static readonly IReadOnlyList<Card> NoCommanders = [];
    private static readonly IReadOnlyList<Card> NoLocked     = [];

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Empty_almostIncluded_returns_no_candidates()
    {
        var source = MakeSource(
            new ComboSearchResult { Included = [], AlmostIncluded = [] },
            new Dictionary<string, Card>());

        var result = await source.GetComboCandidatesAsync(NoCommanders, NoLocked);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Most_popular_combo_piece_gets_inclusion_1()
    {
        var card = MakeCard("Thassa's Oracle");
        var variant = MakeVariant("c1", popularity: 100, missingNames: ["Thassa's Oracle"]);
        var searchResult = new ComboSearchResult
        {
            Included        = [],
            AlmostIncluded  = [variant],
        };
        var source = MakeSource(searchResult, new Dictionary<string, Card>
        {
            ["Thassa's Oracle"] = card,
        });

        var candidates = await source.GetComboCandidatesAsync(NoCommanders, NoLocked);

        Assert.Single(candidates);
        Assert.Equal(1.0, candidates[0].Inclusion);
    }

    [Fact]
    public async Task Less_popular_combo_piece_gets_proportional_score()
    {
        var cardA = MakeCard("CardA");
        var cardB = MakeCard("CardB");
        var variantA = MakeVariant("c1", popularity: 100, missingNames: ["CardA"]);
        var variantB = MakeVariant("c2", popularity: 50,  missingNames: ["CardB"]);
        var searchResult = new ComboSearchResult
        {
            Included       = [],
            AlmostIncluded = [variantA, variantB],
        };
        var source = MakeSource(searchResult, new Dictionary<string, Card>
        {
            ["CardA"] = cardA,
            ["CardB"] = cardB,
        });

        var candidates = await source.GetComboCandidatesAsync(NoCommanders, NoLocked);

        var byName = candidates.ToDictionary(c => c.Card.Name);
        Assert.Equal(1.0, byName["CardA"].Inclusion, precision: 5);
        Assert.Equal(0.5, byName["CardB"].Inclusion, precision: 5);
    }

    [Fact]
    public async Task Card_in_multiple_combos_accumulates_scores_up_to_1()
    {
        var shared = MakeCard("Shared");
        // Two equally popular combos both need "Shared"
        var v1 = MakeVariant("c1", popularity: 100, missingNames: ["Shared"]);
        var v2 = MakeVariant("c2", popularity: 100, missingNames: ["Shared"]);
        var searchResult = new ComboSearchResult
        {
            Included       = [],
            AlmostIncluded = [v1, v2],
        };
        var source = MakeSource(searchResult, new Dictionary<string, Card>
        {
            ["Shared"] = shared,
        });

        var candidates = await source.GetComboCandidatesAsync(NoCommanders, NoLocked);

        Assert.Single(candidates);
        Assert.Equal(1.0, candidates[0].Inclusion); // 1.0 + 1.0, capped at 1.0
    }

    [Fact]
    public async Task Unresolvable_card_name_is_excluded_from_candidates()
    {
        var card = MakeCard("Known Card");
        var variant = MakeVariant("c1", popularity: 80, missingNames: ["Known Card", "Unknown Card"]);
        var searchResult = new ComboSearchResult
        {
            Included       = [],
            AlmostIncluded = [variant],
        };
        // "Unknown Card" not in the repository
        var source = MakeSource(searchResult, new Dictionary<string, Card>
        {
            ["Known Card"] = card,
        });

        var candidates = await source.GetComboCandidatesAsync(NoCommanders, NoLocked);

        Assert.Single(candidates);
        Assert.Equal("Known Card", candidates[0].Card.Name);
    }

    [Fact]
    public async Task Combo_section_label_is_combo_piece()
    {
        var card = MakeCard("Tainted Pact");
        var variant = MakeVariant("c1", popularity: 60, missingNames: ["Tainted Pact"]);
        var searchResult = new ComboSearchResult
        {
            Included       = [],
            AlmostIncluded = [variant],
        };
        var source = MakeSource(searchResult, new Dictionary<string, Card>
        {
            ["Tainted Pact"] = card,
        });

        var candidates = await source.GetComboCandidatesAsync(NoCommanders, NoLocked);

        Assert.Single(candidates);
        Assert.Equal("Combo Piece", candidates[0].Section);
    }

    [Fact]
    public async Task Template_only_combos_produce_no_candidates()
    {
        // Variant with no MissingCardNames (only template slots)
        var variant = new ComboVariant
        {
            Id               = "c1",
            OwnedPieces      = [],
            MissingCardNames = [],
            MissingTemplates = ["Any mana rock"],
            ProducedEffects  = [],
            Description      = "Need a mana rock.",
            Popularity       = 50,
        };
        var searchResult = new ComboSearchResult
        {
            Included       = [],
            AlmostIncluded = [variant],
        };
        var source = MakeSource(searchResult, new Dictionary<string, Card>());

        var candidates = await source.GetComboCandidatesAsync(NoCommanders, NoLocked);

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task Zero_popularity_combo_piece_gets_minimum_score()
    {
        var card = MakeCard("Obscure Card");
        var popular  = MakeVariant("c1", popularity: 100, missingNames: ["Popular Piece"]);
        var obscure  = MakeVariant("c2", popularity: 0,   missingNames: ["Obscure Card"]);
        var popularCard = MakeCard("Popular Piece");
        var searchResult = new ComboSearchResult
        {
            Included       = [],
            AlmostIncluded = [popular, obscure],
        };
        var source = MakeSource(searchResult, new Dictionary<string, Card>
        {
            ["Popular Piece"] = popularCard,
            ["Obscure Card"]  = card,
        });

        var candidates = await source.GetComboCandidatesAsync(NoCommanders, NoLocked);

        var obscureCandidate = candidates.Single(c => c.Card.Name == "Obscure Card");
        Assert.True(obscureCandidate.Inclusion >= 0.05,
            $"Zero-popularity combo piece should have at least 0.05 score, got {obscureCandidate.Inclusion}");
    }
}
