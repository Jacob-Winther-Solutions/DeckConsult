using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Core.Decks;

/// <summary>
/// Turns "this deck is Aggro with a Voltron theme" into concrete, coherent role coverage targets.
/// This is the deterministic half of the system: the LLM decides archetypes and themes at what
/// weight, then this resolves them into numbers the fill engine uses as coverage objectives.
///
/// Targets are coverage, not physical slot counts. A card with secondary roles satisfies multiple
/// targets simultaneously, so resolved ideals intentionally sum above 99. The physical 99-card
/// constraint is enforced by the fill engine — not here.
/// </summary>
public static class TemplateResolver
{
    /// <param name="minLands">
    /// Minimum land coverage target after clamping. Default 30 is intentionally permissive —
    /// high-powered combo decks can run fewer. Note: MDFCs with a land backside count toward
    /// the physical land total but their front-face CardType is not Land, so they are not
    /// automatically credited here. TODO: handle MDFC land contributions before enforcing this floor.
    /// </param>
    /// <param name="maxLands">
    /// Maximum land coverage target after clamping. Default 45 allows Landfall and other
    /// land-heavy strategies.
    /// </param>
    public static DeckTemplate Resolve(
        DeckTemplate baseline,
        IReadOnlyList<WeightedArchetype> archetypes,
        IReadOnlyList<WeightedTheme>?    themes   = null,
        BracketProfile?                  bracket  = null,
        int minLands  = 30,
        int maxLands  = 45)
    {
        // 1. Union of all roles mentioned by the baseline, archetypes, themes, and bracket.
        var roles = new HashSet<CardRole>(baseline.Targets.Keys);
        foreach (var wa in archetypes)
            roles.UnionWith(wa.Profile.Adjustments.Keys);
        foreach (var wt in themes ?? [])
            roles.UnionWith(wt.Profile.Adjustments.Keys);
        if (bracket is not null)
            roles.UnionWith(bracket.Adjustments.Keys);

        // 2. Coverage ideal = baseline ideal
        //                   + Σarchetype (adj × weight)
        //                   + Σtheme (adj × weight)
        //                   + bracket adjustment (weight 1.0), ≥ 0.
        var ideals = new Dictionary<CardRole, int>();
        foreach (var role in roles)
        {
            double value = baseline.Targets.TryGetValue(role, out var t) ? t.Ideal : 0;
            foreach (var wa in archetypes)
                if (wa.Profile.Adjustments.TryGetValue(role, out var adj))
                    value += adj * wa.Weight;
            foreach (var wt in themes ?? [])
                if (wt.Profile.Adjustments.TryGetValue(role, out var adj))
                    value += adj * wt.Weight;
            if (bracket is not null && bracket.Adjustments.TryGetValue(role, out var badj))
                value += badj;
            ideals[role] = (int)Math.Round(Math.Max(0, value));
        }

        // 3. Clamp land count within the allowed range.
        ideals[CardRole.Land] = Math.Clamp(ideals.GetValueOrDefault(CardRole.Land), minLands, maxLands);

        // 4. Wrap each ideal in a tolerance band.
        var targets = ideals.ToDictionary(kv => kv.Key, kv => Band(kv.Key, kv.Value));

        var nameParts = archetypes.Select(a => a.Profile.Name)
            .Concat((themes ?? []).Select(t => t.Profile.Name))
            .Concat(bracket is not null ? [$"Bracket {(int)bracket.Bracket}"] : Array.Empty<string>())
            .ToList();

        return new DeckTemplate
        {
            Name        = nameParts.Count > 0 ? string.Join(" / ", nameParts) : baseline.Name,
            Description = $"Resolved from '{baseline.Name}'. Coverage targets may exceed physical deck size — overlap is expected.",
            Targets     = targets,
        };
    }

    /// <summary>Builds a min/ideal/max band around an ideal. Lands stay tight; spells flex more.</summary>
    private static RoleTarget Band(CardRole role, int ideal)
    {
        if (role == CardRole.Land)
            return new(Math.Max(0, ideal - 1), ideal, ideal + 1);

        int pad = Math.Max(1, (int)Math.Round(ideal * 0.2));
        return new(Math.Max(0, ideal - pad), ideal, ideal + pad);
    }
}
