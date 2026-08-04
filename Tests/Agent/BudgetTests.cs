using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Pipeline;
using EdhDeckBuilder.Agent.Selection;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Tests.Agent;

/// <summary>
/// Tests for the budget enforcement pipeline:
///   - FilterPool pre-filter (MaxCardPriceUsd)
///   - RepairBudgetExcess post-assembly swap pass (TotalBudgetUsd)
///   - DeckBuildResult.TotalPriceUsd and BudgetWarnings output
///
/// All LLM calls are replaced by deterministic mocks. The mock selector picks by
/// inclusion descending, so the highest-inclusion card of each role is always selected
/// first — this makes pool design predictable.
/// </summary>
public sealed class BudgetTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────

    private sealed class FixedSuggestionSource(IReadOnlyList<CardCandidate> pool) : ISuggestionSource
    {
        public Task<IReadOnlyList<CardCandidate>> GetRecommendationsAsync(Card _, CancellationToken __)
            => Task.FromResult(pool);
        public Task<IReadOnlyList<CardCandidate>> GetAverageDeckAsync(Card _, CancellationToken __)
            => Task.FromResult(pool);
        public Task<Dictionary<string, int>> GetPartnerPopularityAsync(CancellationToken __)
            => Task.FromResult(new Dictionary<string, int>());
        public Task<IReadOnlyList<(string FirstCardName, string SecondCardName)>> GetPartnerWithPairsAsync(CancellationToken __)
            => Task.FromResult<IReadOnlyList<(string, string)>>(new List<(string, string)>());
        public Task<IReadOnlyList<CardCandidate>?> GetPartnerPairRecommendationsAsync(Card _, Card __, CancellationToken ___)
            => Task.FromResult<IReadOnlyList<CardCandidate>?>(null);  // Mock returns null; real implementation tested separately
    }

    /// <summary>Assigns roles by parsing the card name prefix ("Ramp_0" → Ramp, etc.).</summary>
    private sealed class RoleParsingClassifier : ILlmClassifier
    {
        public Task<IReadOnlyList<ClassificationResult>> ClassifyAsync(
            IReadOnlyList<CardCandidate> candidates,
            IReadOnlyList<Card> commanders,
            CancellationToken ct = default,
            Func<string, Task>? subProgress = null)
        {
            IReadOnlyList<ClassificationResult> results = candidates
                .Select(c => new ClassificationResult
                {
                    OracleId    = c.Card.OracleId,
                    PrimaryRole = ParseRole(c.Card.Name),
                })
                .ToList();
            return Task.FromResult(results);
        }

        private static CardRole ParseRole(string name)
        {
            var prefix = name.Contains('_') ? name[..name.IndexOf('_')] : name;
            return Enum.TryParse<CardRole>(prefix, ignoreCase: true, out var r) ? r : CardRole.Synergy;
        }
    }

    private sealed class NoOpComboCardSource : IComboCardSource
    {
        public Task<IReadOnlyList<CardCandidate>> GetComboCandidatesAsync(
            IReadOnlyList<Card> _, IReadOnlyList<Card> __, CancellationToken ___)
            => Task.FromResult<IReadOnlyList<CardCandidate>>([]);
    }

    /// <summary>Ranks candidates by inclusion descending — stable, no LLM calls.</summary>
    private sealed class InclusionOrderSelector : ICardSelector
    {
        public Task<IReadOnlyList<SelectionResult>> SelectAsync(
            CardRole role,
            IReadOnlyList<FillCandidate> candidates,
            BuildContext context,
            BuildState state,
            CancellationToken ct)
        {
            IReadOnlyList<SelectionResult> results = candidates
                .OrderByDescending(c => c.Candidate.Inclusion)
                .ThenBy(c => c.Card.OracleId)
                .Select((c, i) => new SelectionResult
                {
                    OracleId  = c.Card.OracleId,
                    Rank      = i + 1,
                    Rationale = "mock",
                })
                .ToList();
            return Task.FromResult(results);
        }
    }

    // ── Factories ─────────────────────────────────────────────────────────────

    private static Card MakeCommander() => new()
    {
        ScryfallId        = Guid.NewGuid(),
        OracleId          = Guid.NewGuid(),
        Name              = "Plan_Commander",
        TypeLine          = "Legendary Creature",
        Types             = CardType.Creature,
        ColorIdentity     = Color.Black,
        IsLegendary       = true,
        CanBeCommander    = true,
        CommanderLegality = Legality.Legal,
    };

    /// <summary>
    /// Creates a pool large enough to fill a 99-card deck. Cards are named "Role_N"
    /// so <see cref="RoleParsingClassifier"/> assigns the correct primary role.
    /// Inclusion runs 0.90 → 0.46 across the 12 Ramp cards, 0.90 → 0.54 across 10
    /// CardAdvantage cards, etc. — highest index is always lowest inclusion.
    ///
    /// DeckTemplate.Balanced targets Ramp ideal = 10, so the mock selector commits
    /// Ramp_0…Ramp_9 (highest 10 by inclusion) and leaves Ramp_10 and Ramp_11
    /// uncommitted. This predictable behaviour is what the repair-swap tests rely on.
    ///
    /// <paramref name="priceOverrides"/> maps card names to USD prices. Omit a card to
    /// give it <c>null</c> (unknown price — not filtered by either budget axis).
    /// </summary>
    private static IReadOnlyList<CardCandidate> CreatePool(
        IReadOnlyDictionary<string, decimal?>? priceOverrides = null)
    {
        var overrides = priceOverrides ?? new Dictionary<string, decimal?>();
        var cards     = new List<CardCandidate>();

        foreach (var (role, count) in new[]
        {
            (CardRole.Plan,               12),
            (CardRole.Ramp,               12),
            (CardRole.CardAdvantage,      10),
            (CardRole.TargetedDisruption,  8),
            (CardRole.MassDisruption,      8),
            (CardRole.Tutor,               8),
            (CardRole.Protection,          8),
            (CardRole.Payoff,             10),
            (CardRole.Synergy,            14),
        })
        {
            for (int i = 0; i < count; i++)
            {
                var name = $"{role}_{i}";
                var card = new Card
                {
                    ScryfallId        = Guid.NewGuid(),
                    OracleId          = Guid.NewGuid(),
                    Name              = name,
                    TypeLine          = "Instant",
                    Types             = CardType.Instant,
                    ColorIdentity     = Color.Black,
                    CommanderLegality = Legality.Legal,
                    PriceUsd          = overrides.TryGetValue(name, out var p) ? p : null,
                };
                cards.Add(new CardCandidate(card, Math.Round(0.9 - i * 0.04, 4), "Test"));
            }
        }

        for (int i = 0; i < 10; i++)
        {
            var name = $"Land_{i}";
            var card = new Card
            {
                ScryfallId        = Guid.NewGuid(),
                OracleId          = Guid.NewGuid(),
                Name              = name,
                TypeLine          = "Land",
                Types             = CardType.Land,
                ColorIdentity     = Color.Black,
                IsBasicLand       = false,
                CommanderLegality = Legality.Legal,
                PriceUsd          = overrides.TryGetValue(name, out var p) ? p : null,
            };
            cards.Add(new CardCandidate(card, Math.Round(0.7 - i * 0.05, 4), "Lands"));
        }

        return cards;
    }

    private static DeckBuilder MakeBuilder(IReadOnlyList<CardCandidate> pool) =>
        new(new FixedSuggestionSource(pool),
            new RoleParsingClassifier(),
            new InclusionOrderSelector(),
            new NoOpComboCardSource(),
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<DeckBuilder>());

    private static SoftConstraints Constraints(decimal? maxCard = null, decimal? totalBudget = null) =>
        new() { Bracket = Bracket.Three, MaxCardPriceUsd = maxCard, TotalBudgetUsd = totalBudget };

    private static readonly IReadOnlyList<WeightedArchetype> MidrangeArchetype =
        [new WeightedArchetype(ArchetypeLibrary.All[Archetype.Midrange], 1.0)];

    // Control archetype has no Ramp adjustment, so resolved Ramp ideal = 10 (the pool baseline).
    // This keeps Ramp_10 and Ramp_11 uncommitted after greedy fill, which the repair-swap test needs.
    private static readonly IReadOnlyList<WeightedArchetype> ControlArchetype =
        [new WeightedArchetype(ArchetypeLibrary.All[Archetype.Control], 1.0)];

    // ── FilterPool (per-card pre-filter) tests ────────────────────────────────

    [Fact]
    public async Task FilterPool_excludes_card_with_known_price_above_per_card_limit()
    {
        // Ramp_0 ($20) exceeds MaxCardPriceUsd = $15 and must not reach the LLM or the deck.
        // Ramp_1..11 have null price → pass the filter and fill the Ramp slots instead.
        var pool = CreatePool(new Dictionary<string, decimal?> { ["Ramp_0"] = 20m });

        var result = await MakeBuilder(pool).BuildAsync(
            [MakeCommander()], DeckTemplate.Balanced, MidrangeArchetype,
            constraints: Constraints(maxCard: 15m));

        Assert.DoesNotContain("Ramp_0", result.Deck.Select(s => s.Card.Name));
    }

    [Fact]
    public async Task FilterPool_keeps_card_priced_exactly_at_the_per_card_limit()
    {
        // $15 == $15 is not strictly greater than the limit → card must NOT be filtered.
        var pool = CreatePool(new Dictionary<string, decimal?> { ["Ramp_0"] = 15m });

        var result = await MakeBuilder(pool).BuildAsync(
            [MakeCommander()], DeckTemplate.Balanced, MidrangeArchetype,
            constraints: Constraints(maxCard: 15m));

        Assert.Contains("Ramp_0", result.Deck.Select(s => s.Card.Name));
    }

    [Fact]
    public async Task FilterPool_keeps_null_price_cards_regardless_of_per_card_limit()
    {
        // All cards have no price data. Even with an extremely low limit, none are filtered.
        var pool = CreatePool(); // no price overrides → all PriceUsd = null

        var result = await MakeBuilder(pool).BuildAsync(
            [MakeCommander()], DeckTemplate.Balanced, MidrangeArchetype,
            constraints: Constraints(maxCard: 0.01m));

        // Ramp slots must still be filled (null-price cards were not excluded).
        var rampCards = result.Deck.Where(s => s.Roles.Primary == CardRole.Ramp).ToList();
        Assert.NotEmpty(rampCards);
        Assert.All(rampCards, s => Assert.Null(s.Card.PriceUsd));
    }

    // ── RepairBudgetExcess (total-budget repair pass) tests ───────────────────

    [Fact]
    public async Task RepairBudget_swaps_most_expensive_committed_card_for_cheapest_same_role_alternative()
    {
        // Use Control archetype: it has no Ramp adjustment, so Ramp ideal stays at the baseline
        // of 10. Greedy fill commits Ramp_0..9 and leaves Ramp_10 and Ramp_11 uncommitted.
        // (Midrange adds +1 to Ramp → ideal 11 → Ramp_10 would be committed during fill.)
        // RepairBudgetExcess must swap Ramp_0 ($100) for Ramp_10 ($2) to bring total under $50.
        var pool = CreatePool(new Dictionary<string, decimal?>
        {
            ["Ramp_0"]  = 100m,
            ["Ramp_10"] = 2m,
        });

        var result = await MakeBuilder(pool).BuildAsync(
            [MakeCommander()], DeckTemplate.Balanced, ControlArchetype,
            constraints: Constraints(totalBudget: 50m));

        var deckNames = result.Deck.Select(s => s.Card.Name).ToHashSet();
        Assert.DoesNotContain("Ramp_0",  deckNames); // swapped out
        Assert.Contains("Ramp_10",       deckNames); // swapped in
        Assert.True(result.TotalPriceUsd <= 50m,
            $"Expected total ≤ $50.00 after repair but was ${result.TotalPriceUsd:F2}");
        Assert.Empty(result.BudgetWarnings);
    }

    [Fact]
    public async Task RepairBudget_only_swaps_within_the_same_primary_role()
    {
        // Ramp_0 is expensive ($100); only Plan_10 ($1) is a cheap uncommitted alternative,
        // but it is a Plan card — a different primary role — and must NOT be used as a swap.
        var pool = CreatePool(new Dictionary<string, decimal?>
        {
            ["Ramp_0"]  = 100m,
            ["Plan_10"] = 1m,
        });

        var result = await MakeBuilder(pool).BuildAsync(
            [MakeCommander()], DeckTemplate.Balanced, MidrangeArchetype,
            constraints: Constraints(totalBudget: 10m));

        // The repair could not find a cheaper same-role (Ramp) alternative with a known price.
        // Ramp_0 stays in the deck; Plan_10 is irrelevant to the repair.
        var deckNames = result.Deck.Select(s => s.Card.Name).ToHashSet();
        Assert.Contains("Ramp_0", deckNames);
    }

    [Fact]
    public async Task RepairBudget_emits_total_warning_when_all_same_role_alternatives_cost_the_same()
    {
        // All 12 Ramp cards share price $100; the repair finds no cheaper same-role alternative.
        var priceOverrides = Enumerable.Range(0, 12)
            .ToDictionary(i => $"Ramp_{i}", _ => (decimal?)100m);
        var pool = CreatePool(priceOverrides);

        var result = await MakeBuilder(pool).BuildAsync(
            [MakeCommander()], DeckTemplate.Balanced, MidrangeArchetype,
            constraints: Constraints(totalBudget: 10m));

        Assert.True(result.TotalPriceUsd > 10m);
        Assert.Contains(result.BudgetWarnings,
            w => w.Contains("Total deck price") && w.Contains("exceeds the total budget"));
    }

    [Fact]
    public async Task RepairBudget_makes_no_changes_when_already_within_total_budget()
    {
        // Ramp_0 costs $5; total will be $5 (all others null). Budget is $100 — no repair needed.
        var pool = CreatePool(new Dictionary<string, decimal?> { ["Ramp_0"] = 5m });

        var result = await MakeBuilder(pool).BuildAsync(
            [MakeCommander()], DeckTemplate.Balanced, MidrangeArchetype,
            constraints: Constraints(totalBudget: 100m));

        Assert.Contains("Ramp_0", result.Deck.Select(s => s.Card.Name));
        Assert.Empty(result.BudgetWarnings);
    }

    // ── TotalPriceUsd and BudgetWarnings output tests ─────────────────────────

    [Fact]
    public async Task TotalPriceUsd_equals_sum_of_non_null_prices_in_the_committed_deck()
    {
        // Only Ramp_0 has a known price. Since it has the highest Ramp inclusion (0.90)
        // it is always committed. TotalPriceUsd must equal its price exactly.
        var pool = CreatePool(new Dictionary<string, decimal?> { ["Ramp_0"] = 7m });

        var result = await MakeBuilder(pool).BuildAsync(
            [MakeCommander()], DeckTemplate.Balanced, MidrangeArchetype);

        var expectedTotal = result.Deck.Sum(s => s.Card.PriceUsd ?? 0m);
        Assert.Equal(expectedTotal, result.TotalPriceUsd);
        Assert.Equal(7m, result.TotalPriceUsd);
    }

    [Fact]
    public async Task TotalPriceUsd_is_zero_when_no_cards_have_price_data()
    {
        var pool   = CreatePool(); // all PriceUsd = null
        var result = await MakeBuilder(pool).BuildAsync(
            [MakeCommander()], DeckTemplate.Balanced, MidrangeArchetype);

        Assert.Equal(0m, result.TotalPriceUsd);
    }

    [Fact]
    public async Task BudgetWarnings_empty_when_no_budget_constraints_set()
    {
        var pool   = CreatePool();
        var result = await MakeBuilder(pool).BuildAsync(
            [MakeCommander()], DeckTemplate.Balanced, MidrangeArchetype);

        Assert.Empty(result.BudgetWarnings);
    }

    [Fact]
    public async Task BudgetWarnings_empty_when_deck_is_within_both_budget_axes()
    {
        var pool = CreatePool(new Dictionary<string, decimal?> { ["Ramp_0"] = 5m });

        var result = await MakeBuilder(pool).BuildAsync(
            [MakeCommander()], DeckTemplate.Balanced, MidrangeArchetype,
            constraints: Constraints(maxCard: 10m, totalBudget: 100m));

        Assert.Empty(result.BudgetWarnings);
    }

    [Fact]
    public async Task BudgetWarnings_no_per_card_violations_when_pre_filter_removed_over_budget_cards()
    {
        // Ramp_0 ($30) is filtered out by FilterPool before it can be selected.
        // Ramp_1 ($10) is within the limit and selected instead.
        // The resulting deck should contain no per-card budget violations.
        var pool = CreatePool(new Dictionary<string, decimal?>
        {
            ["Ramp_0"] = 30m,
            ["Ramp_1"] = 10m,
        });

        var result = await MakeBuilder(pool).BuildAsync(
            [MakeCommander()], DeckTemplate.Balanced, MidrangeArchetype,
            constraints: Constraints(maxCard: 15m));

        Assert.DoesNotContain(result.BudgetWarnings,
            w => w.Contains("exceeds the per-card budget"));
    }
}
