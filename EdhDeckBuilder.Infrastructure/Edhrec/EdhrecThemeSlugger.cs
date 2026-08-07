using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Infrastructure.Edhrec;


/// <summary>
/// Maps our <see cref="Theme"/> enum values to the EDHREC JSON tag slug used in
/// <c>/pages/tags/{slug}.json</c> and <c>/pages/commanders/{commander}/{slug}.json</c>.
/// Returns <see langword="null"/> for themes with no EDHREC equivalent.
/// For <see cref="Theme.Tribal"/>, the slug is derived from <see cref="WeightedTheme.TribeName"/>
/// using the same slugging logic as commander names.
/// </summary>
internal static class EdhrecThemeSlugger
{
    private static readonly Dictionary<Theme, string> Slugs = new()
    {
        [Theme.BigMana]           = "big-mana",
        [Theme.Aristocrats]       = "aristocrats",
        [Theme.Voltron]           = "voltron",
        [Theme.Tokens]            = "tokens",
        [Theme.Lifegain]          = "lifegain",
        [Theme.Reanimator]        = "reanimator",
        [Theme.Spellslinger]      = "spellslinger",
        [Theme.Blink]             = "blink",
        [Theme.CountersMatter]    = "counters-matter",
        [Theme.PlusOneCounters]   = "plus-1-plus-1-counters",
        [Theme.MinusOneCounters]  = "minus-1-minus-1-counters",
        [Theme.Enchantress]       = "enchantress",
        [Theme.Equipment]         = "equipment",
        [Theme.Lands]             = "lands",
        [Theme.Graveyard]         = "graveyard",
        [Theme.Storm]             = "storm",
        [Theme.Proliferate]       = "proliferate",
        [Theme.Stax]              = "stax",
        [Theme.Pillowfort]        = "pillowfort",
        [Theme.GroupHug]          = "group-hug",
        [Theme.Mill]              = "mill",
        [Theme.Cycling]           = "cycling",
        [Theme.Wheels]            = "wheels",
        [Theme.Infect]            = "infect",
        [Theme.Artifacts]         = "artifacts",
        [Theme.Superfriends]      = "superfriends",
        [Theme.Chaos]             = "chaos",
        [Theme.Burn]              = "burn",
        [Theme.Sacrifice]         = "sacrifice",
        [Theme.Auras]             = "auras",
        [Theme.Treasure]          = "treasure",
        [Theme.Legends]           = "legends",
        [Theme.Discard]           = "discard",
        [Theme.Clones]            = "clones",
        [Theme.Landfall]          = "landfall",
        [Theme.GroupSlug]         = "group-slug",
        [Theme.Historic]          = "historic",
        [Theme.ExtraCombats]      = "extra-combats",
        [Theme.Theft]             = "theft",
        [Theme.SelfMill]          = "self-mill",
        [Theme.BirthingPod]       = "birthing-pod",
        [Theme.ForcedCombat]      = "forced-combat",
        [Theme.Vehicles]          = "vehicles",
        [Theme.XSpells]           = "x-spells",
        [Theme.CommanderMatters]  = "commander-matters",
        [Theme.Exile]             = "exile",
        [Theme.Cascade]           = "cascade",
        [Theme.Hatebears]         = "hatebears",
        [Theme.ToughnessMatter]   = "toughness-matters",
        [Theme.SpellCopy]         = "spell-copy",
        [Theme.ExtraTurns]        = "extra-turns",
        [Theme.Etb]               = "etb",
        [Theme.Energy]            = "energy",
        [Theme.Ninjutsu]          = "ninjutsu",
        [Theme.Sagas]             = "sagas",
        [Theme.AttackTriggers]    = "attack-triggers",
        [Theme.Clues]             = "clues",
        [Theme.Food]              = "food",
        [Theme.Monarch]           = "monarch",
        // Theme.Tribal: derived from TribeName at call time — not in this table.
    };

    // EDHREC sometimes uses alternative slugs for themes we know under a different name.
    // These aliases resolve to the canonical Theme but are not used for pool fetching.
    private static readonly Dictionary<string, Theme> SlugAliases = new()
    {
        ["pillow-fort"]   = Theme.Pillowfort,
        ["planeswalkers"] = Theme.Superfriends,
        ["lands-matter"]  = Theme.Lands,
    };

    private static readonly Dictionary<string, Theme> SlugToTheme =
        Slugs.ToDictionary(kv => kv.Value, kv => kv.Key)
             .Concat(SlugAliases)
             .ToDictionary(kv => kv.Key, kv => kv.Value);

    private static readonly Dictionary<string, Archetype> ArchetypeSlugs = new()
    {
        ["control"]  = Archetype.Control,
        ["aggro"]    = Archetype.Aggro,
        ["combo"]    = Archetype.Combo,
        ["midrange"] = Archetype.Midrange,
    };

    /// <summary>
    /// Returns the <see cref="Theme"/> for an EDHREC slug, or <see langword="null"/> when not recognized.
    /// Tribal themes are not in this table — they use dynamic creature-type slugs.
    /// </summary>
    public static Theme? TryGetTheme(string slug) =>
        SlugToTheme.TryGetValue(slug, out var theme) ? theme : null;

    /// <summary>
    /// Returns the <see cref="Archetype"/> for an EDHREC slug, or <see langword="null"/> when not recognized.
    /// </summary>
    public static Archetype? TryGetArchetype(string slug) =>
        ArchetypeSlugs.TryGetValue(slug, out var archetype) ? archetype : null;

    /// <summary>
    /// Returns the EDHREC slug for the given weighted theme, or <see langword="null"/> when no
    /// slug is available (custom theme, Tribal with no creature type, or unmapped built-in).
    /// </summary>
    public static string? GetSlug(WeightedTheme theme)
    {
        if (theme.Profile.Theme == Theme.Tribal)
            return string.IsNullOrWhiteSpace(theme.TribeName)
                ? null
                : EdhrecSlugger.ToSlug(theme.TribeName);

        return theme.Profile.Theme.HasValue
               && Slugs.TryGetValue(theme.Profile.Theme.Value, out var slug)
            ? slug
            : null;
    }
}
