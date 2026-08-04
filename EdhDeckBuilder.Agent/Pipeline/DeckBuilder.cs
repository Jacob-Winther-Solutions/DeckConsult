using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Agent.Fill;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EdhDeckBuilder.Agent.Pipeline;

/// <summary>
/// Full deck-build pipeline. Orchestrates the staged sequence:
/// <list type="number">
///   <item>Resolve template (TemplateResolver + archetypes / themes / bracket).</item>
///   <item>Gather pool (ISuggestionSource, one call per commander, merged).</item>
///   <item>Filter pool (color identity, legality, singleton, exclude commanders).</item>
///   <item>Classify commanders → RoleProfiles → compute net targets.</item>
///   <item>Classify pool in batches → FillCandidate list.</item>
///   <item>Fill engine (greedy fill + reconciliation).</item>
///   <item>Color-fixing pass (swap basics for non-basic lands).</item>
///   <item>Repair illegal cards.</item>
///   <item>Distribute basic lands proportionally by pip demand.</item>
///   <item>Assemble DeckBuildResult.</item>
/// </list>
/// </summary>
public sealed class DeckBuilder(
    ISuggestionSource suggestionSource,
    ILlmClassifier classifier,
    ICardSelector selector,
    IComboCardSource comboCardSource,
    ILogger<DeckBuilder> logger) : IDeckBuilder
{
    private UsageTracker? _usageTracker;

    public UsageTracker? UsageTracker
    {
        get => _usageTracker;
        set
        {
            _usageTracker = value;
            if (value != null)
            {
                if (classifier is IUsageTrackerAware trackedClassifier)
                    trackedClassifier.SetUsageTracker(value);
                if (selector is IUsageTrackerAware trackedSelector)
                    trackedSelector.SetUsageTracker(value);
            }
        }
    }

    public async Task<DeckBuildResult> BuildAsync(
        IReadOnlyList<Card> commanders,
        DeckTemplate template,
        IReadOnlyList<WeightedArchetype> archetypes,
        IReadOnlyList<WeightedTheme>? themes = null,
        BracketProfile? bracket = null,
        SoftConstraints? constraints = null,
        Func<string, Task>? progress = null,
        CancellationToken ct = default,
        bool isLegalPartnerPair = false,
        Func<string, Task>? subProgress = null,
        IReadOnlyList<Card>? lockedCards = null)
    {
        var timer = Stopwatch.StartNew();
        var commanderNames = string.Join(" / ", commanders.Select(c => c.Name));
        logger.LogInformation("DeckBuild_Start: Commanders={CommanderNames}", commanderNames);

        // 1. Resolve template.
        await (progress?.Invoke("Resolving template") ?? Task.CompletedTask);
        var resolved = TemplateResolver.Resolve(template, archetypes, themes, bracket);

        // 2. Gather candidate pool from EDHREC (one call per commander; merged or partner-pair).
        await (progress?.Invoke("Gathering card pool") ?? Task.CompletedTask);
        var stageTimer = Stopwatch.StartNew();
        var rawPool = await GatherPoolAsync(commanders, isLegalPartnerPair, lockedCards, ct);
        stageTimer.Stop();
        logger.LogInformation("GatherPool: {PoolSize} cards, {ElapsedMs}ms", rawPool.Count, stageTimer.ElapsedMilliseconds);

        // 3. Filter pool: legal, CI ⊆ commander CI, not a commander card, within budget.
        await (progress?.Invoke("Filtering pool") ?? Task.CompletedTask);
        stageTimer.Restart();
        var colorIdentity   = commanders.Aggregate(Color.None, (ci, c) => ci | c.ColorIdentity);
        var commanderIds    = commanders.Select(c => c.OracleId).ToHashSet();
        var softConstraints = constraints ?? new SoftConstraints { Bracket = bracket?.Bracket ?? Bracket.Three };
        var filteredPool    = FilterPool(rawPool, commanderIds, colorIdentity, softConstraints);
        stageTimer.Stop();
        int filtered = rawPool.Count - filteredPool.Count;
        logger.LogInformation("FilterPool: {InputCount} → {OutputCount} ({FilteredCount} filtered), {ElapsedMs}ms",
            rawPool.Count, filteredPool.Count, filtered, stageTimer.ElapsedMilliseconds);

        // 4. Classify commanders → profiles → net targets.
        await (progress?.Invoke("Classifying commanders") ?? Task.CompletedTask);
        stageTimer.Restart();
        var commanderCandidates = commanders
            .Select(c => new CardCandidate(c, 1.0, "Commanders"))
            .ToList();
        var commanderClassifications = await classifier.ClassifyAsync(commanderCandidates, commanders, ct);
        var commanderProfiles = BuildCommanderProfiles(commanderClassifications, commanders);
        var netTargets = ComputeNetTargets(resolved, commanderProfiles);
        stageTimer.Stop();
        logger.LogInformation("ClassifyCommanders: {CommanderCount} commanders classified, {ElapsedMs}ms",
            commanders.Count, stageTimer.ElapsedMilliseconds);

        // 5. Build context (LockedOracleIds populated after step 6).
        int reservedLandCount  = resolved.Targets.TryGetValue(CardRole.Land, out var lt) ? lt.Ideal : 38;
        int nonCommanderCount  = 100 - commanders.Count;

        var context = new BuildContext
        {
            Commanders        = commanders,
            ColorIdentity     = colorIdentity,
            ResolvedTemplate  = resolved,
            NetTargets        = netTargets,
            CommanderProfiles = commanderProfiles,
            Constraints       = softConstraints,
            ReservedLandCount = reservedLandCount,
            NonCommanderCount = nonCommanderCount,
        };

        // 6. Classify pool → FillCandidates.
        await (progress?.Invoke("Classifying card pool") ?? Task.CompletedTask);
        stageTimer.Restart();
        var (fillPool, classifications) = await ClassifyPoolAsync(filteredPool, commanders, ct, subProgress);
        stageTimer.Stop();

        // 6b. Classify locked cards and add them to context.
        var lockedFillCandidates = await ClassifyLockedCardsAsync(
            lockedCards, commanders, commanderIds, ct, subProgress);
        if (lockedFillCandidates.Count > 0)
        {
            var ids = lockedFillCandidates.Select(c => c.Card.OracleId).ToHashSet();
            context = context with { LockedOracleIds = ids };
            logger.LogInformation("LockedCards: {Count} cards pre-committed", lockedFillCandidates.Count);
        }

        // Count breakdown by role
        var roleBreakdown = fillPool
            .GroupBy(fc => fc.Roles.Primary)
            .ToDictionary(g => g.Key, g => g.Count());
        logger.LogInformation("ClassifyPool: {TotalClassified} classified, {ElapsedMs}ms",
            fillPool.Count, stageTimer.ElapsedMilliseconds);
        foreach (var (role, count) in roleBreakdown.OrderBy(kv => kv.Key.ToString()))
        {
            logger.LogInformation("  {Role}: {Count}", role, count);
        }

        // Log fill pool entering the fill engine
        int unmatchedCount = roleBreakdown.GetValueOrDefault(CardRole.Unmatched, 0);
        int landCount      = roleBreakdown.GetValueOrDefault(CardRole.Land, 0);
        int matchedNonLand = fillPool.Count - unmatchedCount - landCount;
        logger.LogInformation(
            "FillPool entering engine: {MatchedNonLand} role-matched non-land + {Land} land candidates ({Unmatched} Unmatched)",
            matchedNonLand, landCount, unmatchedCount);

        // Log Unmatched cards with reasoning (debug mode only)
        var unmatchedCards = fillPool.Where(fc => fc.Roles.Primary == CardRole.Unmatched).ToList();
        if (unmatchedCards.Count > 0)
        {
            logger.LogInformation("Unmatched cards ({Count}):", unmatchedCards.Count);
            var classifsByOracleId = classifications.ToDictionary(c => c.OracleId);
            foreach (var unmatched in unmatchedCards.OrderBy(c => c.Card.Name))
            {
                var reasoning = classifsByOracleId.TryGetValue(unmatched.Card.OracleId, out var c)
                    ? c.Reasoning ?? "(no reasoning provided)"
                    : "(no classification found)";
                logger.LogInformation("  {CardName}: {Reasoning}", unmatched.Card.Name, reasoning);
            }
        }

        // 7. Fill engine (Passes A + B: greedy fill + reconciliation).
        await (progress?.Invoke("Filling deck") ?? Task.CompletedTask);
        stageTimer.Restart();
        var engine     = new FillEngine(selector);
        var fillResult = await engine.FillAsync(context, fillPool, ct, subProgress, lockedFillCandidates);
        stageTimer.Stop();
        logger.LogInformation("FillEngine: {FilledCount} cards committed, {ElapsedMs}ms",
            fillResult.State.Committed.Count, stageTimer.ElapsedMilliseconds);
        foreach (var (role, (input, ranked)) in fillResult.SelectorStats.OrderBy(kv => kv.Key.ToString()))
            logger.LogInformation("  Selector({Role}): {Input} candidates → {Ranked} ranked", role, input, ranked);

        // 8. Color-fixing pass (Pass C).
        await (progress?.Invoke("Applying color fixing") ?? Task.CompletedTask);
        stageTimer.Restart();
        var fixingWarnings = ColorFixingPass.Apply(context, fillResult.State, fillPool);
        stageTimer.Stop();
        logger.LogInformation("ColorFix: {RemainingCount} after fixing, {ElapsedMs}ms",
            fillResult.State.Committed.Count, stageTimer.ElapsedMilliseconds);

        // 9. Repair illegal cards (post-fill safety net).
        await (progress?.Invoke("Repairing illegal cards") ?? Task.CompletedTask);
        stageTimer.Restart();
        RepairEngine.RepairIllegalCards(context, fillResult.State, fillPool);
        RepairEngine.RepairBudgetExcess(context, fillResult.State, fillPool);
        stageTimer.Stop();
        logger.LogInformation("RepairCards: {RemainingCount} after repair, {ElapsedMs}ms",
            fillResult.State.Committed.Count, stageTimer.ElapsedMilliseconds);

        // 10. Distribute basic lands proportionally by pip demand.
        // Use ReservedLandCount - UtilityLandCount (not BasicCount) so MDFC land credits
        // don't reduce the physical card total below the required 98/99.
        await (progress?.Invoke("Distributing basic lands") ?? Task.CompletedTask);
        stageTimer.Restart();
        int basicsToDistribute = Math.Max(0, context.ReservedLandCount - fillResult.State.UtilityLandCount);
        var basicLandCounts = DistributeBasics(
            basicsToDistribute, colorIdentity, fillResult.State);
        stageTimer.Stop();
        int totalBasics = basicLandCounts.Values.Sum();
        logger.LogInformation("DistributeBasics: {BasicCount} basics distributed, {ElapsedMs}ms",
            totalBasics, stageTimer.ElapsedMilliseconds);

        // 11. Assemble result.
        await (progress?.Invoke("Assembling result") ?? Task.CompletedTask);
        stageTimer.Restart();
        var result = RepairEngine.Assemble(
            context, fillResult, fixingWarnings, fillPool, resolved, basicLandCounts);
        stageTimer.Stop();
        logger.LogInformation("Assemble: {DeckSize} cards in final deck, {ElapsedMs}ms",
            result.Deck.Count, stageTimer.ElapsedMilliseconds);

        // Log runner-up stats
        var classificationsByOracleId = classifications.ToDictionary(c => c.OracleId);
        var classifiedRunnerUps = result.RunnerUps
            .Count(ru => classificationsByOracleId.ContainsKey(ru.Card.OracleId));
        var unclassifiedRunnerUps = result.RunnerUps.Count - classifiedRunnerUps;
        logger.LogInformation("RunnerUps: {Total} total ({Classified} classified, {Unclassified} unclassified)",
            result.RunnerUps.Count, classifiedRunnerUps, unclassifiedRunnerUps);

        timer.Stop();
        logger.LogInformation("DeckBuild_Complete: {DeckSize} cards total, {TotalElapsedMs}ms elapsed",
            result.Deck.Count + result.BasicLandCounts.Values.Sum(), timer.ElapsedMilliseconds);

        return result;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<IReadOnlyList<CardCandidate>> GatherPoolAsync(
        IReadOnlyList<Card> commanders,
        bool isLegalPartnerPair,
        IReadOnlyList<Card>? lockedCards,
        CancellationToken ct)
    {
        // Launch combo pool fetch immediately so it runs in parallel with EDHREC
        var comboTask = comboCardSource.GetComboCandidatesAsync(commanders, lockedCards ?? [], ct);

        IReadOnlyList<CardCandidate> edhrecPool;
        if (isLegalPartnerPair && commanders.Count == 2)
        {
            var partnerPool = await suggestionSource.GetPartnerPairRecommendationsAsync(
                commanders[0], commanders[1], ct);
            edhrecPool = partnerPool ?? await GetIndividualPoolAsync(commanders, ct);
        }
        else
        {
            edhrecPool = await GetIndividualPoolAsync(commanders, ct);
        }

        var comboCandidates = await comboTask;
        if (comboCandidates.Count == 0)
            return edhrecPool;

        // Merge: start from EDHREC pool; combo candidates upgrade or extend it
        var merged = new Dictionary<Guid, CardCandidate>();
        foreach (var c in edhrecPool)
            merged[c.Card.OracleId] = c;
        foreach (var c in comboCandidates)
            if (!merged.TryGetValue(c.Card.OracleId, out var existing)
                || c.Inclusion > existing.Inclusion)
                merged[c.Card.OracleId] = c;

        return merged.Values.ToList();
    }

    private async Task<IReadOnlyList<CardCandidate>> GetIndividualPoolAsync(
        IReadOnlyList<Card> commanders,
        CancellationToken ct)
    {
        var tasks = commanders
            .Select(c => suggestionSource.GetRecommendationsAsync(c, ct))
            .ToList();
        var batches = await Task.WhenAll(tasks);

        var merged = new Dictionary<Guid, CardCandidate>();
        foreach (var batch in batches)
            foreach (var candidate in batch)
                if (!merged.TryGetValue(candidate.Card.OracleId, out var existing)
                    || candidate.Inclusion > existing.Inclusion)
                    merged[candidate.Card.OracleId] = candidate;

        return merged.Values.ToList();
    }

    private static IReadOnlyList<CardCandidate> FilterPool(
        IReadOnlyList<CardCandidate> pool,
        HashSet<Guid> commanderIds,
        Color colorIdentity,
        SoftConstraints constraints)
    {
        return pool
            .Where(c => !commanderIds.Contains(c.Card.OracleId)
                     && c.Card.CommanderLegality == Legality.Legal
                     && c.Card.ColorIdentity.IsWithin(colorIdentity)
                     && !(constraints.MaxCardPriceUsd.HasValue
                          && c.Card.PriceUsd.HasValue
                          && c.Card.PriceUsd > constraints.MaxCardPriceUsd))
            .ToList();
    }

    private static IReadOnlyList<RoleProfile> BuildCommanderProfiles(
        IReadOnlyList<ClassificationResult> classifications,
        IReadOnlyList<Card> commanders)
    {
        var byId = classifications.ToDictionary(r => r.OracleId);
        return commanders
            .Select(c => byId.TryGetValue(c.OracleId, out var r)
                ? r.ToRoleProfile()
                : RoleProfile.Of(CardRole.Plan))  // default: commander is the Plan
            .ToList();
    }

    private static IReadOnlyDictionary<CardRole, RoleTarget> ComputeNetTargets(
        DeckTemplate resolved,
        IReadOnlyList<RoleProfile> commanderProfiles)
    {
        // Commander coverage at 1.5× weight is subtracted from each role's ideal.
        var cmdCoverage = new Dictionary<CardRole, double>();
        foreach (var profile in commanderProfiles)
            foreach (var role in profile.AllRoles())
                cmdCoverage[role] = cmdCoverage.GetValueOrDefault(role) + profile.CoverageFor(role) * 1.5;

        var result = new Dictionary<CardRole, RoleTarget>();
        foreach (var (role, target) in resolved.Targets)
        {
            if (role == CardRole.Land) { result[role] = target; continue; }

            double contrib  = cmdCoverage.GetValueOrDefault(role);
            int netIdeal    = (int)Math.Max(0, Math.Round(target.Ideal - contrib));
            int pad         = Math.Max(1, (int)Math.Round(netIdeal * 0.2));
            result[role]    = new RoleTarget(Math.Max(0, netIdeal - pad), netIdeal, netIdeal + pad);
        }
        return result;
    }

    private async Task<IReadOnlyList<FillCandidate>> ClassifyLockedCardsAsync(
        IReadOnlyList<Card>? lockedCards,
        IReadOnlyList<Card> commanders,
        HashSet<Guid> commanderIds,
        CancellationToken ct,
        Func<string, Task>? subProgress = null)
    {
        if (lockedCards is not { Count: > 0 }) return [];

        // Exclude commanders from the locked list — they're already committed elsewhere.
        var candidates = lockedCards
            .Where(c => !commanderIds.Contains(c.OracleId))
            .Select(c => new CardCandidate(c, 1.0, "Locked"))
            .ToList();

        if (candidates.Count == 0) return [];

        if (subProgress is not null)
            await subProgress($"Locking {candidates.Count} must-include card(s)…");

        var classifications = await classifier.ClassifyAsync(candidates, commanders, ct);
        var classifiedById  = classifications.ToDictionary(r => r.OracleId);

        return candidates.Select(c =>
        {
            classifiedById.TryGetValue(c.Card.OracleId, out var r);
            return new FillCandidate
            {
                Candidate   = c,
                Roles       = r?.ToRoleProfile() ?? RoleProfile.Of(CardRole.Synergy),
                LandCredit  = r?.LandCredit ?? 0.0,
            };
        }).ToList();
    }

    private async Task<(IReadOnlyList<FillCandidate>, IReadOnlyList<ClassificationResult>)> ClassifyPoolAsync(
        IReadOnlyList<CardCandidate> pool,
        IReadOnlyList<Card> commanders,
        CancellationToken ct,
        Func<string, Task>? subProgress = null)
    {
        var classifications = await classifier.ClassifyAsync(pool, commanders, ct, subProgress);

        var poolById       = pool.ToDictionary(c => c.Card.OracleId);
        var classifiedById = classifications.ToDictionary(r => r.OracleId);

        var fillCandidates = poolById.Keys
            .Select(id => new FillCandidate
            {
                Candidate   = poolById[id],
                Roles       = classifiedById.TryGetValue(id, out var r)
                    ? r.ToRoleProfile()
                    : RoleProfile.Of(CardRole.Unmatched),
                LandCredit  = classifiedById.TryGetValue(id, out var r2) ? r2.LandCredit : 0.0,
            })
            .ToList();

        return (fillCandidates, classifications);
    }

    /// <summary>
    /// Distributes <paramref name="basicCount"/> basics across the colors in the commander's
    /// identity, proportional to pip demand from committed non-land cards.
    /// </summary>
    private static IReadOnlyDictionary<string, int> DistributeBasics(
        int basicCount,
        Color commanderIdentity,
        BuildState state)
    {
        if (basicCount <= 0) return new Dictionary<string, int>();

        var colors = Enum.GetValues<Color>()
            .Where(c => c != Color.None && commanderIdentity.HasFlag(c))
            .ToList();

        if (colors.Count == 0) return new Dictionary<string, int> { ["Wastes"] = basicCount };

        // Pip demand: how many committed non-land cards need each color.
        var demand = colors.ToDictionary(c => c, _ => 0);
        foreach (var candidate in state.CommittedCandidates.Values)
        {
            if (candidate.Card.Types.HasFlag(CardType.Land)) continue;
            foreach (var color in colors)
                if (candidate.Card.ColorIdentity.HasFlag(color))
                    demand[color]++;
        }

        int totalDemand = demand.Values.Sum();
        var result      = new Dictionary<string, int>();
        int assigned    = 0;

        for (int i = 0; i < colors.Count - 1; i++)
        {
            var color = colors[i];
            int count = totalDemand > 0
                ? Math.Max(0, (int)Math.Round((double)demand[color] / totalDemand * basicCount))
                : basicCount / colors.Count;
            result[BasicLandName(color)] = count;
            assigned += count;
        }

        // Last color absorbs any rounding remainder.
        result[BasicLandName(colors[^1])] = Math.Max(0, basicCount - assigned);

        return result;
    }

    private static string BasicLandName(Color color) => color switch
    {
        Color.White => "Plains",
        Color.Blue  => "Island",
        Color.Black => "Swamp",
        Color.Red   => "Mountain",
        Color.Green => "Forest",
        _           => throw new ArgumentOutOfRangeException(nameof(color)),
    };
}
