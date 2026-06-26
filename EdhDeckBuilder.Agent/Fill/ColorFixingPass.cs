using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Fill;

/// <summary>
/// Pass C — runs once after <see cref="FillEngine"/> finishes the full 99.
/// Swaps basic land slots for non-basic color-fixing lands from the candidate pool,
/// ordering candidates by how well they cover the colors most in demand among the
/// committed spells (pip demand score), then by EDHREC inclusion as a tiebreak.
/// <para>
/// Hard caps applied per candidate, checked before each commit:
/// <list type="bullet">
///   <item>Basic floor — never drop below 8 basics.</item>
///   <item>Non-basic cap — non-basic lands may not exceed 50 % of the total land base.</item>
/// </list>
/// </para>
/// Mutates <paramref name="state"/> in place via <see cref="BuildState.Commit"/>.
/// </summary>
public static class ColorFixingPass
{
    private const int MinBasics = 8;

    /// <summary>
    /// Applies color-fixing land swaps to <paramref name="state"/>. Returns warnings
    /// if either hard cap was hit before the eligible candidate pool was exhausted.
    /// </summary>
    public static IReadOnlyList<string> Apply(
        BuildContext context,
        BuildState state,
        IReadOnlyList<FillCandidate> pool)
    {
        int totalLandBase = state.UtilityLandCount + state.BasicCount;
        int fixingCap = (int)Math.Floor(totalLandBase * 0.5);

        var committed = state.CommittedCandidates.Keys.ToHashSet();
        var pipDemand = ComputePipDemand(state);

        var candidates = pool
            .Where(c =>
                !committed.Contains(c.Card.OracleId) &&
                c.Card.Types.HasFlag(CardType.Land) &&
                !c.Card.IsBasicLand &&
                c.Card.ColorIdentity != Color.None &&
                c.Card.ColorIdentity.IsWithin(context.ColorIdentity))
            .OrderByDescending(c => ColorScore(c.Card.ColorIdentity, pipDemand))
            .ThenByDescending(c => c.Candidate.Inclusion)
            .ThenBy(c => c.Card.OracleId)
            .ToList();

        bool floorHit = false;
        bool capHit = false;

        foreach (var candidate in candidates)
        {
            if (state.BasicCount <= MinBasics) { floorHit = true; break; }
            if (state.UtilityLandCount >= fixingCap) { capHit = true; break; }

            state.Commit(candidate);
        }

        return BuildWarnings(state, fixingCap, floorHit, capHit);
    }

    /// <summary>
    /// Counts how many committed non-land cards reference each color in their identity.
    /// A card with Blue+Black identity increments both Blue and Black.
    /// </summary>
    private static Dictionary<Color, int> ComputePipDemand(BuildState state)
    {
        var demand = new Dictionary<Color, int>();

        foreach (var candidate in state.CommittedCandidates.Values)
        {
            if (candidate.Card.Types.HasFlag(CardType.Land)) continue;

            foreach (Color color in Enum.GetValues<Color>())
            {
                if (color == Color.None) continue;
                if (candidate.Card.ColorIdentity.HasFlag(color))
                    demand[color] = demand.GetValueOrDefault(color) + 1;
            }
        }

        return demand;
    }

    /// <summary>
    /// Sum of pip demand for each color the land can produce. Higher = covers more needed colors.
    /// A dual Blue/Black land in a deck with 4 Blue spells and 1 Black spell scores 5.
    /// </summary>
    private static int ColorScore(Color landIdentity, Dictionary<Color, int> pipDemand)
    {
        int score = 0;
        foreach (var (color, count) in pipDemand)
            if (landIdentity.HasFlag(color))
                score += count;
        return score;
    }

    private static IReadOnlyList<string> BuildWarnings(
        BuildState state,
        int fixingCap,
        bool floorHit,
        bool capHit)
    {
        var warnings = new List<string>();

        if (floorHit)
            warnings.Add(
                $"Color-fixing pass stopped at the {MinBasics}-basic floor " +
                $"({state.BasicCount} basics remaining). Consider adding more color-fixing lands to the candidate pool.");

        if (capHit)
            warnings.Add(
                $"Color-fixing land cap reached ({fixingCap} non-basics, 50 % of land base) " +
                $"with {state.BasicCount} basics remaining.");

        return warnings;
    }
}
