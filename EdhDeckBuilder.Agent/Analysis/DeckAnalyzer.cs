using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Agent.Analysis;

public sealed class DeckAnalyzer(
    ICardRepository cardRepository,
    ILlmClassifier classifier,
    ILogger<DeckAnalyzer> logger) : IDeckAnalyzer
{
    private UsageTracker? _usageTracker;

    public UsageTracker? UsageTracker
    {
        get => _usageTracker;
        set
        {
            _usageTracker = value;
            if (value != null && classifier is IUsageTrackerAware tracked)
                tracked.SetUsageTracker(value);
        }
    }

    public async Task<DeckAnalysisResult> AnalyzeAsync(
        IReadOnlyList<Card> commanders,
        IReadOnlyList<ParsedCardEntry> entries,
        string? userFeedback = null,
        Func<string, Task>? progress = null,
        Func<string, Task>? subProgress = null,
        CancellationToken ct = default)
    {
        var commanderNames = string.Join(" / ", commanders.Select(c => c.Name));
        logger.LogInformation("DeckAnalysis_Start: commanders={Commanders}, entries={EntryCount}",
            commanderNames, entries.Count);

        // 1. Resolve entries → non-basics + basics + unresolved
        await (progress?.Invoke("Resolving card names") ?? Task.CompletedTask);
        var (nonBasics, basicLandCounts, unresolvedNames) = await ResolveCardsAsync(entries, ct);
        logger.LogInformation("DeckAnalysis_Resolve: {NonBasic} non-basics, {BasicTotal} basic land slots, {Unresolved} unresolved",
            nonBasics.Count, basicLandCounts.Values.Sum(), unresolvedNames.Count);

        // 2. Color identity violations (advisory)
        var commanderIdentity = commanders.Aggregate(Color.None, (ci, c) => ci | c.ColorIdentity);
        var violations = CheckColorIdentity(nonBasics, commanderIdentity);

        // 3. Classify commanders
        await (progress?.Invoke("Classifying cards") ?? Task.CompletedTask);
        var commanderCandidates = commanders
            .Select(c => new CardCandidate(c, 1.0, "Commander"))
            .ToList();
        var commanderClassifications = await classifier.ClassifyAsync(commanderCandidates, commanders, ct);
        var commanderClassifiedById  = commanderClassifications.ToDictionary(r => r.OracleId);
        var commanderCards = commanders.Select(card =>
        {
            commanderClassifiedById.TryGetValue(card.OracleId, out var result);
            return new AnalyzedCard
            {
                Card                = card,
                Roles               = BuildRoleProfile(result, CardRole.Plan),
                ClassifierReasoning = result?.Reasoning,
                IsCommander         = true,
            };
        }).ToList();

        // 4. Classify the 99 (non-basic, non-commander cards)
        IReadOnlyList<ClassificationResult> deckClassifications = [];
        if (nonBasics.Count > 0)
        {
            var candidates = nonBasics
                .Select(c => new CardCandidate(c, 1.0, "Analyzed"))
                .ToList();
            deckClassifications = await classifier.ClassifyAsync(candidates, commanders, ct, subProgress);
        }

        var deckClassifiedById = deckClassifications.ToDictionary(r => r.OracleId);
        var analyzedCards = nonBasics.Select(card =>
        {
            deckClassifiedById.TryGetValue(card.OracleId, out var result);
            return new AnalyzedCard
            {
                Card                = card,
                Roles               = BuildRoleProfile(result, CardRole.Unmatched),
                ClassifierReasoning = result?.Reasoning,
            };
        }).ToList();

        // 5. Compute coverage (commanders + 99 + basics)
        await (progress?.Invoke("Computing analysis") ?? Task.CompletedTask);
        var coverage = ComputeCoverage(commanderCards, analyzedCards, basicLandCounts);
        var (bracket, explanation) = BracketEstimator.Estimate(coverage);
        var gaps = ComputeGaps(coverage, DeckTemplate.Balanced.Targets);
        var totalPrice = nonBasics.Sum(c => c.PriceUsd ?? 0);

        logger.LogInformation("DeckAnalysis_Complete: {Cards} cards, bracket={Bracket}, gaps={Gaps}",
            analyzedCards.Count, bracket, gaps.Count);

        return new DeckAnalysisResult
        {
            Commanders              = commanders,
            CommanderCards          = commanderCards,
            Cards                   = analyzedCards,
            BasicLandCounts         = basicLandCounts,
            ActualCoverage          = coverage,
            EstimatedBracket        = bracket,
            BracketExplanation      = explanation,
            RoleGaps                = gaps,
            UnresolvedNames         = unresolvedNames,
            ColorIdentityViolations = violations,
            TotalPriceUsd           = totalPrice,
        };
    }

    private async Task<(List<Card> nonBasics, Dictionary<string, int> basics, List<string> unresolved)>
        ResolveCardsAsync(IReadOnlyList<ParsedCardEntry> entries, CancellationToken ct)
    {
        var nonBasics  = new List<Card>();
        var basics     = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var unresolved = new List<string>();
        var seenIds    = new HashSet<Guid>();

        foreach (var entry in entries)
        {
            var card = await cardRepository.GetByNameAsync(entry.Name, ct);
            if (card is null)
            {
                unresolved.Add(entry.Name);
            }
            else if (card.IsBasicLand)
            {
                basics[card.Name] = basics.GetValueOrDefault(card.Name) + entry.Quantity;
            }
            else if (seenIds.Add(card.OracleId))
            {
                nonBasics.Add(card);
            }
            // Duplicate oracle IDs for non-basics are silently skipped (singleton rule)
        }

        return (nonBasics, basics, unresolved);
    }

    private static IReadOnlyList<string> CheckColorIdentity(
        IReadOnlyList<Card> cards,
        Color commanderIdentity)
    {
        if (commanderIdentity == Color.None) return []; // colorless commanders allow any card
        var violations = new List<string>();
        foreach (var card in cards)
            if (!card.ColorIdentity.IsWithin(commanderIdentity))
                violations.Add($"{card.Name} ({card.ColorIdentity} outside {commanderIdentity})");
        return violations;
    }

    private static IReadOnlyDictionary<CardRole, double> ComputeCoverage(
        IReadOnlyList<AnalyzedCard> commanderCards,
        IReadOnlyList<AnalyzedCard> deckCards,
        IReadOnlyDictionary<string, int> basicLandCounts)
    {
        var coverage = new Dictionary<CardRole, double>();

        foreach (var cards in new[] { commanderCards, deckCards })
            foreach (var analyzed in cards)
                foreach (var role in analyzed.Roles.AllRoles())
                    coverage[role] = coverage.GetValueOrDefault(role) + analyzed.Roles.CoverageFor(role);

        var basicCount = basicLandCounts.Values.Sum();
        if (basicCount > 0)
            coverage[CardRole.Land] = coverage.GetValueOrDefault(CardRole.Land) + basicCount;

        return coverage;
    }

    private static RoleProfile BuildRoleProfile(ClassificationResult? result, CardRole defaultRole)
    {
        if (result is null) return RoleProfile.Of(defaultRole);
        var profile = result.ToRoleProfile();
        if (result.LandCredit > 0 &&
            profile.Primary != CardRole.Land &&
            !profile.Secondary.Any(s => s.Role == CardRole.Land))
        {
            profile = profile.With(new RoleContribution(CardRole.Land, RoleRelation.Modal, result.LandCredit));
        }
        return profile;
    }

    private static IReadOnlyList<RoleGap> ComputeGaps(
        IReadOnlyDictionary<CardRole, double> coverage,
        IReadOnlyDictionary<CardRole, RoleTarget> targets)
    {
        var gaps = new List<RoleGap>();
        foreach (var (role, target) in targets)
        {
            if (role == CardRole.Land) continue;
            var actual = coverage.GetValueOrDefault(role);
            if (actual < target.Min)
            {
                gaps.Add(new RoleGap
                {
                    Role           = role,
                    ActualCoverage = actual,
                    IdealTarget    = target.Ideal,
                    Shortfall      = target.Ideal - actual,
                });
            }
        }
        return [.. gaps.OrderByDescending(g => g.Shortfall)];
    }
}
