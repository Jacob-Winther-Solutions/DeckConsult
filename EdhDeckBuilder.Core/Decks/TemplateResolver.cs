using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Core.Decks;

/// <summary>
/// Turns "this deck is Aggro with a Voltron theme" into concrete, coherent role targets.
/// This is the deterministic half of the system: the LLM decides archetypes and themes at
/// what weight, then this resolves them into numbers that always sum to a legal deck size.
/// </summary>
public static class TemplateResolver
{
    /// <param name="deckSize">Non-commander cards: 99 for a single commander, 98 for a partner pair.</param>
    /// <param name="minLands">
    /// Minimum land count after clamping. Default 30 is intentionally permissive — high-powered combo
    /// decks can run fewer. Note: MDFCs with a land backside count toward this total but their
    /// front-face CardType is not Land, so they are not automatically credited here. TODO: handle
    /// MDFC land contributions before enforcing this floor.
    /// </param>
    /// <param name="maxLands">
    /// Maximum land count after clamping. Default 45 allows Landfall and other land-heavy strategies.
    /// </param>
    public static DeckTemplate Resolve(
        DeckTemplate baseline,
        IReadOnlyList<WeightedArchetype> archetypes,
        IReadOnlyList<WeightedTheme>?    themes   = null,
        int deckSize  = 99,
        int minLands  = 30,
        int maxLands  = 45)
    {
        // 1. Union of all roles mentioned by the baseline, selected archetypes, and themes.
        var roles = new HashSet<CardRole>(baseline.Targets.Keys);
        foreach (var wa in archetypes)
            roles.UnionWith(wa.Profile.Adjustments.Keys);
        foreach (var wt in themes ?? [])
            roles.UnionWith(wt.Profile.Adjustments.Keys);

        // 2. Raw ideal = baseline ideal + Σarchetype (adj × weight) + Σtheme (adj × weight), ≥ 0.
        var raw = new Dictionary<CardRole, double>();
        foreach (var role in roles)
        {
            double value = baseline.Targets.TryGetValue(role, out var t) ? t.Ideal : 0;
            foreach (var wa in archetypes)
                if (wa.Profile.Adjustments.TryGetValue(role, out var adj))
                    value += adj * wa.Weight;
            foreach (var wt in themes ?? [])
                if (wt.Profile.Adjustments.TryGetValue(role, out var adj))
                    value += adj * wt.Weight;
            raw[role] = Math.Max(0, value);
        }

        // 3. Lock in the land count first, then fit the rest of the budget around it.
        int lands  = Math.Clamp((int)Math.Round(raw.GetValueOrDefault(CardRole.Land)), minLands, maxLands);
        int budget = deckSize - lands;

        // 4. Scale non-land ideals proportionally so they fill the remaining budget exactly.
        var nonland    = raw.Where(kv => kv.Key != CardRole.Land).ToList();
        double rawSum  = nonland.Sum(kv => kv.Value);
        double scale   = rawSum > 0 ? budget / rawSum : 0;

        var ideals = new Dictionary<CardRole, int> { [CardRole.Land] = lands };
        foreach (var (role, value) in nonland)
            ideals[role] = (int)Math.Round(value * scale);

        // 5. Absorb rounding drift into the largest non-land bucket.
        int drift          = deckSize - ideals.Values.Sum();
        var nonlandIdeals  = ideals.Where(kv => kv.Key != CardRole.Land).ToList();
        if (drift != 0 && nonlandIdeals.Count > 0)
            ideals[nonlandIdeals.OrderByDescending(kv => kv.Value).First().Key] += drift;

        // 6. Wrap each ideal in a tolerance band.
        var targets = ideals.ToDictionary(kv => kv.Key, kv => Band(kv.Key, kv.Value));

        var nameParts = archetypes.Select(a => a.Profile.Name)
            .Concat((themes ?? []).Select(t => t.Profile.Name))
            .ToList();

        return new DeckTemplate
        {
            Name        = nameParts.Count > 0 ? string.Join(" / ", nameParts) : baseline.Name,
            Description = $"Resolved from '{baseline.Name}' normalized to {deckSize} cards.",
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
