using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Pipeline;
using EdhDeckBuilder.Agent.Selection;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Tests.Agent;

/// <summary>
/// Integration tests for <see cref="DeckBuilder"/>. All LLM calls are replaced by
/// deterministic mocks: roles parsed from card name prefixes, selection ordered by inclusion.
/// These tests verify structural invariants of the pipeline output — not card quality.
/// </summary>
public sealed class DeckBuilderTests
{
    // ── Mocks ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a fixed pool for any commander. Pool cards are named "Role_N" so the
    /// classifier can assign roles deterministically without an LLM call.
    /// </summary>
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

    /// <summary>
    /// Assigns roles by parsing the card name prefix ("Plan_0" → Plan, "Ramp_3" → Ramp, …).
    /// Unrecognised prefixes fall back to Synergy. Works for commander cards too.
    /// </summary>
    private sealed class RoleParsingClassifier : ILlmClassifier
    {
        public Task<IReadOnlyList<ClassificationResult>> ClassifyBatchAsync(
            IReadOnlyList<CardCandidate> candidates,
            IReadOnlyList<Card> commanders,
            CancellationToken ct)
        {
            IReadOnlyList<ClassificationResult> results = candidates.Select(c => new ClassificationResult
            {
                OracleId    = c.Card.OracleId,
                PrimaryRole = ParseRole(c.Card.Name),
            }).ToList();
            return Task.FromResult(results);
        }

        private static CardRole ParseRole(string name)
        {
            var prefix = name.Contains('_') ? name[..name.IndexOf('_')] : name;
            return Enum.TryParse<CardRole>(prefix, ignoreCase: true, out var r) ? r : CardRole.Synergy;
        }
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
                    Rationale = $"Selected as the top {role} card by inclusion.",
                })
                .ToList();
            return Task.FromResult(results);
        }
    }

    // ── Factories ─────────────────────────────────────────────────────────────

    private static Card MakeCommander(string name, Color colorIdentity) => new()
    {
        ScryfallId        = Guid.NewGuid(),
        OracleId          = Guid.NewGuid(),
        Name              = name,
        TypeLine          = "Legendary Creature",
        Types             = CardType.Creature,
        ColorIdentity     = colorIdentity,
        IsLegendary       = true,
        CanBeCommander    = true,
        CommanderLegality = Legality.Legal,
    };

    /// <summary>
    /// Creates a pool large enough to fill a full 99-slot deck.
    /// Spells named "Role_N" so RoleParsingClassifier assigns the right primary role.
    /// Non-basic lands named "Land_N" so they are classified as CardRole.Land and picked up by ColorFixingPass.
    /// </summary>
    private static IReadOnlyList<CardCandidate> CreatePool(Color colorIdentity)
    {
        var cards = new List<CardCandidate>();

        // 10–12 cards per role in FillOrder
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
                var card = new Card
                {
                    ScryfallId        = Guid.NewGuid(),
                    OracleId          = Guid.NewGuid(),
                    Name              = $"{role}_{i}",
                    TypeLine          = "Instant",
                    Types             = CardType.Instant,
                    ColorIdentity     = colorIdentity,
                    CommanderLegality = Legality.Legal,
                };
                cards.Add(new CardCandidate(card, Math.Round(0.9 - i * 0.04, 4), "Test"));
            }
        }

        // Non-basic color-fixing lands (named "Land_N" → classified as CardRole.Land by parser)
        for (int i = 0; i < 10; i++)
        {
            var card = new Card
            {
                ScryfallId        = Guid.NewGuid(),
                OracleId          = Guid.NewGuid(),
                Name              = $"Land_{i}",
                TypeLine          = "Land",
                Types             = CardType.Land,
                ColorIdentity     = colorIdentity,
                IsBasicLand       = false,
                CommanderLegality = Legality.Legal,
            };
            cards.Add(new CardCandidate(card, Math.Round(0.7 - i * 0.05, 4), "Lands"));
        }

        return cards;
    }

    private static DeckBuilder MakeBuilder(IReadOnlyList<CardCandidate> pool) =>
        new(new FixedSuggestionSource(pool),
            new RoleParsingClassifier(),
            new InclusionOrderSelector());

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_deck_plus_basics_fills_all_99_slots()
    {
        var miirym = MakeCommander("Plan_Commander", Color.Red | Color.Green);
        var pool   = CreatePool(Color.Red | Color.Green);
        var result = await MakeBuilder(pool).BuildAsync(
            [miirym], DeckTemplate.Balanced,
            [new WeightedArchetype(ArchetypeLibrary.All[Archetype.Midrange], 1.0)]);

        int total = result.Deck.Count + result.BasicLandCounts.Values.Sum();
        Assert.Equal(99, total);
    }

    [Fact]
    public async Task BuildAsync_all_committed_cards_within_commander_color_identity()
    {
        var miirym = MakeCommander("Plan_Commander", Color.Red | Color.Green);
        var pool   = CreatePool(Color.Red | Color.Green);
        var result = await MakeBuilder(pool).BuildAsync(
            [miirym], DeckTemplate.Balanced,
            [new WeightedArchetype(ArchetypeLibrary.All[Archetype.Midrange], 1.0)]);

        var commanderCi = miirym.ColorIdentity;
        Assert.All(result.Deck, s =>
            Assert.True(s.Card.ColorIdentity.IsWithin(commanderCi),
                $"{s.Card.Name} has CI {s.Card.ColorIdentity} outside {commanderCi}"));
    }

    [Fact]
    public async Task BuildAsync_runner_ups_are_disjoint_from_deck()
    {
        var miirym = MakeCommander("Plan_Commander", Color.Red | Color.Green);
        var pool   = CreatePool(Color.Red | Color.Green);
        var result = await MakeBuilder(pool).BuildAsync(
            [miirym], DeckTemplate.Balanced,
            [new WeightedArchetype(ArchetypeLibrary.All[Archetype.Midrange], 1.0)]);

        var deckIds = result.Deck.Select(s => s.Card.OracleId).ToHashSet();
        Assert.All(result.RunnerUps, c => Assert.DoesNotContain(c.Card.OracleId, deckIds));
    }

    [Fact]
    public async Task BuildAsync_basic_land_counts_sum_to_remaining_basic_slots()
    {
        var miirym = MakeCommander("Plan_Commander", Color.Red | Color.Green);
        var pool   = CreatePool(Color.Red | Color.Green);
        var result = await MakeBuilder(pool).BuildAsync(
            [miirym], DeckTemplate.Balanced,
            [new WeightedArchetype(ArchetypeLibrary.All[Archetype.Midrange], 1.0)]);

        // BasicLandCounts must only include colors within the commander's CI.
        var cmdCi = miirym.ColorIdentity;
        Assert.All(result.BasicLandCounts.Keys, name =>
        {
            Color color = name switch
            {
                "Plains"   => Color.White,
                "Island"   => Color.Blue,
                "Swamp"    => Color.Black,
                "Mountain" => Color.Red,
                "Forest"   => Color.Green,
                _          => Color.None,
            };
            Assert.True(color == Color.None || color.IsWithin(cmdCi),
                $"Basic '{name}' is outside commander CI {cmdCi}");
        });

        // Total basics + deck = 99
        Assert.Equal(99, result.Deck.Count + result.BasicLandCounts.Values.Sum());
    }

    [Fact]
    public async Task BuildAsync_colorless_commander_uses_wastes_as_basic_land()
    {
        var kozilek = MakeCommander("Plan_Commander", Color.None);
        var pool    = CreatePool(Color.None);
        var result  = await MakeBuilder(pool).BuildAsync(
            [kozilek], DeckTemplate.Balanced,
            [new WeightedArchetype(ArchetypeLibrary.All[Archetype.Midrange], 1.0)]);

        Assert.True(result.BasicLandCounts.ContainsKey("Wastes"),
            "Colorless commander should use Wastes as basic land.");
        Assert.Single(result.BasicLandCounts);
    }

    [Fact]
    public async Task BuildAsync_colorless_commander_fills_all_99_slots()
    {
        var kozilek = MakeCommander("Plan_Commander", Color.None);
        var pool    = CreatePool(Color.None);
        var result  = await MakeBuilder(pool).BuildAsync(
            [kozilek], DeckTemplate.Balanced,
            [new WeightedArchetype(ArchetypeLibrary.All[Archetype.Midrange], 1.0)]);

        int total = result.Deck.Count + result.BasicLandCounts.Values.Sum();
        Assert.Equal(99, total);
    }

    [Fact]
    public async Task BuildAsync_partner_pair_fills_98_non_commander_slots()
    {
        var partner1 = MakeCommander("Plan_Commander1", Color.Red | Color.Green);
        var partner2 = MakeCommander("Ramp_Commander2", Color.Red | Color.Green);
        var pool     = CreatePool(Color.Red | Color.Green);

        var result = await MakeBuilder(pool).BuildAsync(
            [partner1, partner2], DeckTemplate.Balanced,
            [new WeightedArchetype(ArchetypeLibrary.All[Archetype.Midrange], 1.0)]);

        int total = result.Deck.Count + result.BasicLandCounts.Values.Sum();
        Assert.Equal(98, total);
    }
}
