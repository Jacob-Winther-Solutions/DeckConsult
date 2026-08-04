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
/// Tests for must-include (locked) card behaviour in the build pipeline:
/// - Locked card appears in the final deck with IsLocked = true.
/// - Locked card is exempt from per-card and total budget enforcement.
/// - Locked card is not removed by RepairIllegalCards.
/// - Locked card is excluded from cut suggestions.
/// </summary>
public sealed class LockedCardsTests
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
        public Task<IReadOnlyList<(string, string)>> GetPartnerWithPairsAsync(CancellationToken __)
            => Task.FromResult<IReadOnlyList<(string, string)>>([]);
        public Task<IReadOnlyList<CardCandidate>?> GetPartnerPairRecommendationsAsync(Card _, Card __, CancellationToken ___)
            => Task.FromResult<IReadOnlyList<CardCandidate>?>(null);
    }

    private sealed class RoleParsingClassifier : ILlmClassifier
    {
        public Task<IReadOnlyList<ClassificationResult>> ClassifyAsync(
            IReadOnlyList<CardCandidate> candidates,
            IReadOnlyList<Card> commanders,
            CancellationToken ct = default,
            Func<string, Task>? subProgress = null)
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
            return Enum.TryParse<CardRole>(prefix, ignoreCase: true, out var r) ? r : CardRole.Unmatched;
        }
    }

    private sealed class InclusionOrderSelector : ICardSelector
    {
        public Task<IReadOnlyList<SelectionResult>> SelectAsync(
            CardRole role, IReadOnlyList<FillCandidate> candidates,
            BuildContext context, BuildState state, CancellationToken ct)
        {
            IReadOnlyList<SelectionResult> results = candidates
                .OrderByDescending(c => c.Candidate.Inclusion)
                .ThenBy(c => c.Card.OracleId)
                .Select((c, i) => new SelectionResult
                {
                    OracleId  = c.Card.OracleId,
                    Rank      = i + 1,
                    Rationale = $"Selected {role} card.",
                }).ToList();
            return Task.FromResult(results);
        }
    }

    // ── Factories ─────────────────────────────────────────────────────────────

    private static Card MakeCommander(Color ci = Color.Green) => new()
    {
        ScryfallId        = Guid.NewGuid(),
        OracleId          = Guid.NewGuid(),
        Name              = "Test Commander",
        TypeLine          = "Legendary Creature",
        Types             = CardType.Creature,
        ColorIdentity     = ci,
        IsLegendary       = true,
        CanBeCommander    = true,
        CommanderLegality = Legality.Legal,
    };

    private static Card MakeSpell(string name, Color ci = Color.Green, decimal? price = null) => new()
    {
        ScryfallId        = Guid.NewGuid(),
        OracleId          = Guid.NewGuid(),
        Name              = name,
        TypeLine          = "Instant",
        Types             = CardType.Instant,
        ColorIdentity     = ci,
        CommanderLegality = Legality.Legal,
        PriceUsd          = price,
    };

    private static IReadOnlyList<CardCandidate> CreatePool(Color ci)
    {
        var cards = new List<CardCandidate>();
        foreach (var (role, count) in new[]
        {
            (CardRole.Plan, 12), (CardRole.Ramp, 12), (CardRole.CardAdvantage, 10),
            (CardRole.TargetedDisruption, 8), (CardRole.MassDisruption, 8),
            (CardRole.Tutor, 8), (CardRole.Protection, 8),
            (CardRole.Payoff, 10), (CardRole.Synergy, 14),
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
                    ColorIdentity     = ci,
                    CommanderLegality = Legality.Legal,
                    PriceUsd          = 1.00m,
                };
                cards.Add(new CardCandidate(card, Math.Round(0.9 - i * 0.04, 4), "Test"));
            }
        }
        return cards;
    }

    private sealed class NoOpComboCardSource : IComboCardSource
    {
        public Task<IReadOnlyList<CardCandidate>> GetComboCandidatesAsync(
            IReadOnlyList<Card> _, IReadOnlyList<Card> __, CancellationToken ___)
            => Task.FromResult<IReadOnlyList<CardCandidate>>([]);
    }

    private static DeckBuilder MakeBuilder(IReadOnlyList<CardCandidate> pool) =>
        new(new FixedSuggestionSource(pool),
            new RoleParsingClassifier(),
            new InclusionOrderSelector(),
            new NoOpComboCardSource(),
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<DeckBuilder>());

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LockedCard_AppearsInFinalDeck_WithIsLockedTrue()
    {
        var commander = MakeCommander();
        var lockedCard = MakeSpell("Synergy_locked");
        var pool = CreatePool(Color.Green);
        var builder = MakeBuilder(pool);

        var result = await builder.BuildAsync(
            [commander],
            DeckTemplate.Balanced,
            [],
            lockedCards: [lockedCard]);

        var inDeck = result.Deck.FirstOrDefault(s => s.Card.OracleId == lockedCard.OracleId);
        Assert.NotNull(inDeck);
        Assert.True(inDeck.IsLocked);
    }

    [Fact]
    public async Task LockedCard_IsExcluded_FromPerCardBudgetWarning()
    {
        var commander = MakeCommander();
        // Locked card costs $100 — well above the $5 per-card limit.
        var lockedCard = MakeSpell("Synergy_expensive", price: 100.00m);
        var pool = CreatePool(Color.Green);
        var builder = MakeBuilder(pool);

        var constraints = new SoftConstraints
        {
            Bracket        = Bracket.Three,
            MaxCardPriceUsd = 5.00m,
        };

        var result = await builder.BuildAsync(
            [commander],
            DeckTemplate.Balanced,
            [],
            constraints: constraints,
            lockedCards: [lockedCard]);

        Assert.DoesNotContain(result.BudgetWarnings, w => w.Contains("Synergy_expensive"));
    }

    [Fact]
    public async Task LockedCard_IsExcluded_FromTotalBudgetEnforcement()
    {
        var commander = MakeCommander();
        // Locked card costs $200 — above the total budget by itself.
        var lockedCard = MakeSpell("Synergy_expensive", price: 200.00m);
        var pool = CreatePool(Color.Green);
        var builder = MakeBuilder(pool);

        // Total budget $50 — without locked exclusion, this would trigger a swap pass and a warning.
        var constraints = new SoftConstraints
        {
            Bracket       = Bracket.Three,
            TotalBudgetUsd = 50.00m,
        };

        var result = await builder.BuildAsync(
            [commander],
            DeckTemplate.Balanced,
            [],
            constraints: constraints,
            lockedCards: [lockedCard]);

        // The locked card must still be in the deck.
        Assert.Contains(result.Deck, s => s.Card.OracleId == lockedCard.OracleId && s.IsLocked);
    }

    [Fact]
    public async Task LockedCard_IsNotInCutSuggestions()
    {
        var commander = MakeCommander();
        var lockedCard = MakeSpell("Synergy_locked");
        var pool = CreatePool(Color.Green);
        var builder = MakeBuilder(pool);

        var result = await builder.BuildAsync(
            [commander],
            DeckTemplate.Balanced,
            [],
            lockedCards: [lockedCard]);

        var allCutIds = result.CutSuggestions.Values
            .SelectMany(cuts => cuts.Select(c => c.Card.OracleId))
            .ToHashSet();

        Assert.DoesNotContain(lockedCard.OracleId, allCutIds);
    }

    [Fact]
    public async Task LockedCard_OutsideColorIdentity_NotRemovedByRepair()
    {
        var commander = MakeCommander(Color.Green);
        // Red card — outside green color identity.
        var lockedCard = MakeSpell("Synergy_red", ci: Color.Red);
        var pool = CreatePool(Color.Green);
        var builder = MakeBuilder(pool);

        var result = await builder.BuildAsync(
            [commander],
            DeckTemplate.Balanced,
            [],
            lockedCards: [lockedCard]);

        // Despite being outside CI, the locked card must survive repair.
        Assert.Contains(result.Deck, s => s.Card.OracleId == lockedCard.OracleId && s.IsLocked);
    }
}
