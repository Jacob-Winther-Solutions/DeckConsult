using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Agent.Fill;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

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
    ICardSelector selector) : IDeckBuilder
{
    private const int ClassificationBatchSize = 50;

    public async Task<DeckBuildResult> BuildAsync(
        IReadOnlyList<Card> commanders,
        DeckTemplate template,
        IReadOnlyList<WeightedArchetype> archetypes,
        IReadOnlyList<WeightedTheme>? themes = null,
        BracketProfile? bracket = null,
        SoftConstraints? constraints = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        // 1. Resolve template.
        progress?.Report("Resolving template");
        var resolved = TemplateResolver.Resolve(template, archetypes, themes, bracket);

        // 2. Gather candidate pool from EDHREC (one call per commander; merged).
        progress?.Report("Gathering card pool");
        var rawPool = await GatherPoolAsync(commanders, ct);

        // 3. Filter pool: legal, CI ⊆ commander CI, not a commander card, within budget.
        progress?.Report("Filtering pool");
        var colorIdentity   = commanders.Aggregate(Color.None, (ci, c) => ci | c.ColorIdentity);
        var commanderIds    = commanders.Select(c => c.OracleId).ToHashSet();
        var softConstraints = constraints ?? new SoftConstraints { Bracket = bracket?.Bracket ?? Bracket.Three };
        var filteredPool    = FilterPool(rawPool, commanderIds, colorIdentity, softConstraints);

        // 4. Classify commanders → profiles → net targets.
        progress?.Report("Classifying commanders");
        var commanderCandidates = commanders
            .Select(c => new CardCandidate(c, 1.0, "Commanders"))
            .ToList();
        var commanderClassifications = await classifier.ClassifyBatchAsync(commanderCandidates, commanders, ct);
        var commanderProfiles = BuildCommanderProfiles(commanderClassifications, commanders);
        var netTargets = ComputeNetTargets(resolved, commanderProfiles);

        // 5. Build context.
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
        progress?.Report("Classifying card pool");
        var fillPool = await ClassifyPoolAsync(filteredPool, commanders, ct);

        // 7. Fill engine (Passes A + B: greedy fill + reconciliation).
        progress?.Report("Filling deck");
        var engine     = new FillEngine(selector);
        var fillResult = await engine.FillAsync(context, fillPool, ct);

        // 8. Color-fixing pass (Pass C).
        progress?.Report("Applying color fixing");
        var fixingWarnings = ColorFixingPass.Apply(context, fillResult.State, fillPool);

        // 9. Repair illegal cards (post-fill safety net).
        progress?.Report("Repairing illegal cards");
        RepairEngine.RepairIllegalCards(context, fillResult.State, fillPool);
        RepairEngine.RepairBudgetExcess(context, fillResult.State, fillPool);

        // 10. Distribute basic lands proportionally by pip demand.
        progress?.Report("Distributing basic lands");
        var basicLandCounts = DistributeBasics(
            fillResult.State.BasicCount, colorIdentity, fillResult.State);

        // 11. Assemble result.
        progress?.Report("Assembling result");
        return RepairEngine.Assemble(
            context, fillResult, fixingWarnings, fillPool, resolved, basicLandCounts);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<IReadOnlyList<CardCandidate>> GatherPoolAsync(
        IReadOnlyList<Card> commanders,
        CancellationToken ct)
    {
        var tasks = commanders
            .Select(c => suggestionSource.GetRecommendationsAsync(c, ct))
            .ToList();
        var batches = await Task.WhenAll(tasks);

        // Merge: keep the higher inclusion when the same card appears for multiple commanders.
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

    private async Task<IReadOnlyList<FillCandidate>> ClassifyPoolAsync(
        IReadOnlyList<CardCandidate> pool,
        IReadOnlyList<Card> commanders,
        CancellationToken ct)
    {
        var classifications = new List<ClassificationResult>();

        for (int i = 0; i < pool.Count; i += ClassificationBatchSize)
        {
            var batch   = pool.Skip(i).Take(ClassificationBatchSize).ToList();
            var results = await classifier.ClassifyBatchAsync(batch, commanders, ct);
            classifications.AddRange(results);
        }

        var poolById       = pool.ToDictionary(c => c.Card.OracleId);
        var classifiedById = classifications.ToDictionary(r => r.OracleId);

        return poolById.Keys
            .Where(id => classifiedById.ContainsKey(id))
            .Select(id => new FillCandidate
            {
                Candidate   = poolById[id],
                Roles       = classifiedById[id].ToRoleProfile(),
                LandCredit  = classifiedById[id].LandCredit,
            })
            .ToList();
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
