using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Agent.Fill;

/// <summary>
/// Deterministic fill engine. Given a classified candidate pool and a build context, fills the
/// 99 non-commander slots in two passes:
/// <list type="number">
///   <item>Greedy fill — works through roles in <see cref="FillOrder"/> (scarce → abundant),
///         committing the selector's top-ranked candidates until each role's ideal coverage is met.</item>
///   <item>Reconciliation — bounded monotonic swap loop that tries to bring any out-of-range
///         role back within its min/max band, accepting only swaps that strictly reduce total
///         weighted deviation.</item>
/// </list>
/// The LLM's judgment is injected via <see cref="ICardSelector"/>; everything else is deterministic.
/// </summary>
public sealed class FillEngine(ICardSelector selector)
{
    /// <summary>
    /// The order in which roles are filled: scarce / commander-specific categories first,
    /// abundant / generic categories last. Fill order determines which roles claim physical
    /// slots first when the pool is contested, so it must match the scarcity intuition.
    /// </summary>
    public static readonly IReadOnlyList<CardRole> FillOrder =
    [
        CardRole.Plan,
        CardRole.MassDisruption,
        CardRole.Tutor,
        CardRole.Protection,
        CardRole.Payoff,
        CardRole.TargetedDisruption,
        CardRole.Ramp,
        CardRole.CardAdvantage,
        CardRole.Synergy,
    ];

    private const int MaxReconciliationIterations = 50;

    public async Task<FillResult> FillAsync(
        BuildContext context,
        IReadOnlyList<FillCandidate> pool,
        CancellationToken ct = default)
    {
        var state = new BuildState(context.ReservedLandCount);
        var committed = new HashSet<Guid>();
        var rationales = new Dictionary<Guid, string>();
        var selectorStats = new Dictionary<CardRole, (int Input, int Ranked)>();
        int spellBudget = context.NonCommanderCount - context.ReservedLandCount;

        // ── Greedy fill ──────────────────────────────────────────────────────
        foreach (var role in FillOrder)
        {
            if (!context.NetTargets.TryGetValue(role, out var target)) continue;

            // Coverage may already be satisfied by overlaps committed in earlier roles.
            if (state.Coverage.GetValueOrDefault(role) >= target.Ideal) continue;

            var candidates = pool
                .Where(c => !committed.Contains(c.Card.OracleId)
                         && c.Roles.Primary == role
                         && c.Roles.Primary != CardRole.Unmatched)
                .ToList();

            if (candidates.Count == 0) continue;

            var ranked = await selector.SelectAsync(role, candidates, context, state, ct);
            selectorStats[role] = (candidates.Count, ranked.Count);

            var byId = candidates.ToDictionary(c => c.Card.OracleId);
            var validRanked = ranked
                .Where(r => byId.ContainsKey(r.OracleId))
                .OrderBy(r => r.Rank);

            foreach (var pick in validRanked)
            {
                if (state.Coverage.GetValueOrDefault(role) >= target.Ideal) break;

                var candidate = byId[pick.OracleId];
                bool isLand = candidate.Card.Types.HasFlag(CardType.Land);

                if (isLand && state.BasicCount <= 0) continue;      // no basics left to swap out
                if (!isLand && state.SpellCount >= spellBudget) continue; // spell budget exhausted

                state.Commit(candidate);
                committed.Add(candidate.Card.OracleId);
                rationales[pick.OracleId] = pick.Rationale;
            }
        }

        // ── Spillover: fill any remaining spell slots ─────────────────────────
        // Roles may have overlapped their way to ideal with fewer cards than the slot budget
        // allows; fill the remainder with the highest-inclusion uncommitted spells.
        // Skip Unmatched cards; let the fill engine decide whether they're useful.
        var spillover = pool
            .Where(c => !committed.Contains(c.Card.OracleId)
                     && !c.Card.Types.HasFlag(CardType.Land)
                     && c.Roles.Primary != CardRole.Unmatched)
            .OrderByDescending(c => c.Candidate.Inclusion)
            .ThenBy(c => c.Card.OracleId); // stable tiebreak

        foreach (var candidate in spillover)
        {
            if (state.SpellCount >= spellBudget) break;
            state.Commit(candidate);
            committed.Add(candidate.Card.OracleId);
        }

        // ── Reconciliation ───────────────────────────────────────────────────
        var warnings = Reconcile(context, pool, state, committed, spellBudget);

        return new FillResult(state, warnings, rationales, selectorStats);
    }

    // ── Reconciliation ────────────────────────────────────────────────────────

    private static IReadOnlyList<string> Reconcile(
        BuildContext context,
        IReadOnlyList<FillCandidate> pool,
        BuildState state,
        HashSet<Guid> committed,
        int spellBudget)
    {
        double deviation = ComputeDeviation(context, state);

        for (int i = 0; i < MaxReconciliationIterations; i++)
        {
            // Find the most under-covered role (by shortfall below its minimum).
            CardRole? underRole = null;
            double maxShortfall = 0;
            foreach (var role in FillOrder)
            {
                if (!context.NetTargets.TryGetValue(role, out var t)) continue;
                double shortfall = t.Min - state.Coverage.GetValueOrDefault(role);
                if (shortfall > maxShortfall) { maxShortfall = shortfall; underRole = role; }
            }

            if (underRole is null) break; // all roles are within their minimum — done

            // Best uncommitted candidate that covers the under-served role.
            // Skip Unmatched cards — they're not useful for any role.
            var toAdd = pool
                .Where(c => !committed.Contains(c.Card.OracleId)
                         && !c.Card.Types.HasFlag(CardType.Land)   // keep swap loop to spell slots
                         && c.Roles.Primary != CardRole.Unmatched
                         && c.Roles.AllRoles().Contains(underRole.Value))
                .OrderByDescending(c => c.Roles.CoverageFor(underRole.Value))
                .ThenByDescending(c => c.Candidate.Inclusion)
                .ThenBy(c => c.Card.OracleId)
                .FirstOrDefault();

            if (toAdd is null) break; // nothing in the pool can help — stop

            // Worst committed spell-slot card in an over-covered role.
            var toCut = state.CommittedCandidates.Values
                .Where(c =>
                {
                    if (c.Card.Types.HasFlag(CardType.Land)) return false; // don't cut utility lands here
                    if (!context.NetTargets.TryGetValue(c.Roles.Primary, out var t)) return false;
                    return state.Coverage.GetValueOrDefault(c.Roles.Primary) > t.Ideal;
                })
                .OrderBy(c => c.Candidate.Inclusion)
                .ThenBy(c => c.Card.OracleId)
                .FirstOrDefault();

            if (toCut is null) break; // no over-covered card to sacrifice — stop

            // Try the swap. Accept only if total deviation strictly decreases.
            state.Remove(toCut.Card.OracleId);
            state.Commit(toAdd);

            double newDeviation = ComputeDeviation(context, state);
            if (newDeviation < deviation)
            {
                committed.Remove(toCut.Card.OracleId);
                committed.Add(toAdd.Card.OracleId);
                deviation = newDeviation;
            }
            else
            {
                // Revert — this swap does not help.
                state.Remove(toAdd.Card.OracleId);
                state.Commit(toCut);
                break; // monotonicity guarantee: if the best candidate can't help, stop
            }
        }

        return BuildWarnings(context, state);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Total deviation = Σ |actualCoverage − ideal| across all roles with a target.
    /// Uniform weights (1.0 per role) — can be tuned later if needed.
    /// </summary>
    private static double ComputeDeviation(BuildContext context, BuildState state)
    {
        double total = 0;
        foreach (var (role, target) in context.NetTargets)
            total += Math.Abs(state.Coverage.GetValueOrDefault(role) - target.Ideal);
        return total;
    }

    private static IReadOnlyList<string> BuildWarnings(BuildContext context, BuildState state)
    {
        var warnings = new List<string>();
        foreach (var role in FillOrder)
        {
            if (!context.NetTargets.TryGetValue(role, out var target)) continue;
            double cov = state.Coverage.GetValueOrDefault(role);
            if (cov < target.Min)
                warnings.Add($"{role}: coverage {cov:F1} is below minimum {target.Min} (ideal {target.Ideal}). Pool may be too thin for this commander.");
            else if (cov > target.Max)
                warnings.Add($"{role}: coverage {cov:F1} exceeds maximum {target.Max} (ideal {target.Ideal}). Consider cutting the weakest {role} cards.");
        }
        return warnings;
    }
}
