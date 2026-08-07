using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Core.Decks;

/// <summary>
/// Display category for grouping themes in the UI. Declaration order determines render order.
/// </summary>
public enum ThemeGroup
{
    Permanents,
    Graveyard,
    Spells,
    Lands,
    Counters,
    Combat,
    PoliticsAndControl,
    Synergy,
    Tribal,
}

/// <summary>
/// The thematic identity of a deck — *what* it does, as opposed to <see cref="Archetype"/>, which
/// describes *how* it wins. A theme can be paired with any archetype: Aristocrats-Combo, Voltron-
/// Aggro, or Reanimator-Control are all coherent. The two axes are independent and both expressed
/// as adjustments over the same baseline, so they compose freely via <see cref="TemplateResolver"/>.
/// </summary>
public enum Theme
{
    // existing values — do NOT reorder (serialised by integer in saved results)
    BigMana,
    Aristocrats,
    Voltron,
    Tokens,
    Lifegain,
    Reanimator,
    Spellslinger,
    Blink,
    CountersMatter,
    PlusOneCounters,
    MinusOneCounters,
    Enchantress,
    Equipment,
    Tribal,
    Lands,
    Graveyard,
    Storm,
    Proliferate,
    Stax,
    Pillowfort,
    GroupHug,
    Mill,
    Cycling,
    Wheels,
    Infect,
    Artifacts,
    Superfriends,
    Chaos,

    // new values (appended)
    Burn,
    Sacrifice,
    Auras,
    Treasure,
    Legends,
    Discard,
    Clones,
    Landfall,
    GroupSlug,
    Historic,
    ExtraCombats,
    Theft,
    SelfMill,
    BirthingPod,
    ForcedCombat,
    Vehicles,
    XSpells,
    CommanderMatters,
    Exile,
    Cascade,
    Hatebears,
    ToughnessMatter,
    SpellCopy,
    ExtraTurns,
    Etb,
    Energy,
    Ninjutsu,
    Sagas,
    AttackTriggers,
    Clues,
    Food,
    Monarch,
}

/// <summary>
/// A theme expressed as a <em>delta</em> over the neutral baseline — parallel to
/// <see cref="ArchetypeProfile"/> but representing thematic identity rather than play-style.
/// <see cref="Theme"/> is nullable so that user-constructed runtime profiles (tuned presets or
/// fully custom themes) are not required to bind to an enum value.
/// </summary>
public sealed record ThemeProfile
{
    public Theme? Theme { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required IReadOnlyDictionary<CardRole, int> Adjustments { get; init; }
    public ThemeGroup Group { get; init; }
}

/// <summary>
/// A theme paired with how strongly it applies (1.0 = full, 0.5 = a splash of it).
/// <see cref="TribeName"/> is only meaningful when <see cref="ThemeProfile.Theme"/> is
/// <see cref="Theme.Tribal"/>; it holds the creature type (e.g. "Elves", "Dragons") and
/// drives the EDHREC slug for pool enrichment.
/// </summary>
public readonly record struct WeightedTheme(ThemeProfile Profile, double Weight = 1.0, string? TribeName = null);

/// <summary>The built-in theme profiles. Add new entries here as data, not code.</summary>
public static class ThemeLibrary
{
    public static IReadOnlyDictionary<Theme, ThemeProfile> All { get; } =
        new Dictionary<Theme, ThemeProfile>
        {
            // ── Permanents ────────────────────────────────────────────────────
            [Theme.Tokens] = new ThemeProfile
            {
                Theme       = Theme.Tokens,
                Name        = "Tokens",
                Description = "Go wide with creature tokens and anthems/payoffs that scale on count.",
                Group       = ThemeGroup.Permanents,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]           = +5,
                    [CardRole.Payoff]         = +3,
                    [CardRole.MassDisruption] = -1,
                },
            },
            [Theme.Enchantress] = new ThemeProfile
            {
                Theme       = Theme.Enchantress,
                Name        = "Enchantress",
                Description = "Enchantments trigger card draw; constellation and enchantment payoffs close the game.",
                Group       = ThemeGroup.Permanents,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.CardAdvantage]      = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Ramp]               = -2,
                    [CardRole.Payoff]             = -2,
                },
            },
            [Theme.Equipment] = new ThemeProfile
            {
                Theme       = Theme.Equipment,
                Name        = "Equipment",
                Description = "High equipment density suits up key creatures and protects the strategy.",
                Group       = ThemeGroup.Permanents,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.Protection]         = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Payoff]             = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.CardAdvantage]      = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Artifacts] = new ThemeProfile
            {
                Theme       = Theme.Artifacts,
                Name        = "Artifacts",
                Description = "Artifact synergies and mana rocks form the engine; value scales with artifact count.",
                Group       = ThemeGroup.Permanents,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +3,
                    [CardRole.Ramp]               = +2,
                    [CardRole.CardAdvantage]      = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Payoff]             = -2,
                    [CardRole.Protection]         = -2,
                },
            },
            [Theme.Superfriends] = new ThemeProfile
            {
                Theme       = Theme.Superfriends,
                Name        = "Superfriends",
                Description = "Planeswalkers as the main permanents; protect them and ultimate for wins.",
                Group       = ThemeGroup.Permanents,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.Protection]         = +4,
                    [CardRole.Payoff]             = +2,
                    [CardRole.Synergy]            = +1,
                    [CardRole.MassDisruption]     = -4,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.CardAdvantage]      = -3,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Auras] = new ThemeProfile
            {
                Theme       = Theme.Auras,
                Name        = "Auras",
                Description = "Suit up a creature with enchantment auras; totem armor and hexproof keep the threat alive.",
                Group       = ThemeGroup.Permanents,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.Synergy]            = +3,
                    [CardRole.Protection]         = +1,
                    [CardRole.Payoff]             = +1,
                    [CardRole.MassDisruption]     = -4,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.Ramp]               = -3,
                },
            },
            [Theme.Treasure] = new ThemeProfile
            {
                Theme       = Theme.Treasure,
                Name        = "Treasure",
                Description = "Generate treasure tokens as mana acceleration and artifact payoff triggers.",
                Group       = ThemeGroup.Permanents,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +3,
                    [CardRole.Ramp]               = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Payoff]             = +2,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.CardAdvantage]      = -2,
                },
            },
            [Theme.Vehicles] = new ThemeProfile
            {
                Theme       = Theme.Vehicles,
                Name        = "Vehicles",
                Description = "Crew vehicles with cheap creatures; vehicle payoffs and artifact synergies close the game.",
                Group       = ThemeGroup.Permanents,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.Synergy]            = +3,
                    [CardRole.Payoff]             = +2,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.CardAdvantage]      = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Sagas] = new ThemeProfile
            {
                Theme       = Theme.Sagas,
                Name        = "Sagas",
                Description = "Saga enchantments provide multi-chapter value; saga-matters payoffs compound the advantage.",
                Group       = ThemeGroup.Permanents,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +4,
                    [CardRole.CardAdvantage]      = +2,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.Ramp]               = -2,
                },
            },

            // ── Graveyard ─────────────────────────────────────────────────────
            [Theme.Aristocrats] = new ThemeProfile
            {
                Theme       = Theme.Aristocrats,
                Name        = "Aristocrats",
                Description = "Sacrifice synergies and incremental drain; values recursion heavily.",
                Group       = ThemeGroup.Graveyard,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Recursion]          = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -1,
                },
            },
            [Theme.Reanimator] = new ThemeProfile
            {
                Theme       = Theme.Reanimator,
                Name        = "Reanimator",
                Description = "Fill the graveyard cheaply, then cheat large threats back into play.",
                Group       = ThemeGroup.Graveyard,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Recursion]          = +4,
                    [CardRole.Plan]               = +3,
                    [CardRole.Synergy]            = +1,
                    [CardRole.Payoff]             = +2,
                    [CardRole.Ramp]               = -2,
                    [CardRole.TargetedDisruption] = -1,
                },
            },
            [Theme.Graveyard] = new ThemeProfile
            {
                Theme       = Theme.Graveyard,
                Name        = "Graveyard",
                Description = "Self-mill and graveyard value engines; persistent threats that ignore removal.",
                Group       = ThemeGroup.Graveyard,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Synergy]            = +4,
                    [CardRole.Plan]               = +3,
                    [CardRole.Recursion]          = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.Protection]         = -3,
                    [CardRole.MassDisruption]     = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Sacrifice] = new ThemeProfile
            {
                Theme       = Theme.Sacrifice,
                Name        = "Sacrifice",
                Description = "Sacrifice outlets and death triggers generate value; not drain-focused like Aristocrats.",
                Group       = ThemeGroup.Graveyard,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +4,
                    [CardRole.Payoff]             = +2,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.CardAdvantage]      = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.SelfMill] = new ThemeProfile
            {
                Theme       = Theme.SelfMill,
                Name        = "Self-Mill",
                Description = "Mill yourself to fill the graveyard as a resource; distinct from opponent-targeting Mill.",
                Group       = ThemeGroup.Graveyard,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Recursion]          = +3,
                    [CardRole.TargetedDisruption] = -4,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.CardAdvantage]      = -1,
                },
            },
            [Theme.BirthingPod] = new ThemeProfile
            {
                Theme       = Theme.BirthingPod,
                Name        = "Birthing Pod",
                Description = "Sacrifice creatures to tutor up the chain; ETB value compounds at each step.",
                Group       = ThemeGroup.Graveyard,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +3,
                    [CardRole.Recursion]          = +2,
                    [CardRole.Tutor]              = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.CardAdvantage]      = -2,
                    [CardRole.Ramp]               = -2,
                },
            },

            // ── Spells ────────────────────────────────────────────────────────
            [Theme.Spellslinger] = new ThemeProfile
            {
                Theme       = Theme.Spellslinger,
                Name        = "Spellslinger",
                Description = "Cast triggers and spell density generate value; maximize instant/sorcery count.",
                Group       = ThemeGroup.Spells,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.Synergy]            = +3,
                    [CardRole.CardAdvantage]      = +2,
                    [CardRole.Ramp]               = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Payoff]             = -2,
                    [CardRole.Protection]         = -2,
                },
            },
            [Theme.Storm] = new ThemeProfile
            {
                Theme       = Theme.Storm,
                Name        = "Storm",
                Description = "Chain spells to hit critical mass; storm count wins the game in one turn.",
                Group       = ThemeGroup.Spells,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.CardAdvantage]      = +4,
                    [CardRole.Ramp]               = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.TargetedDisruption] = -4,
                    [CardRole.MassDisruption]     = -4,
                    [CardRole.Payoff]             = -3,
                    [CardRole.Protection]         = -3,
                },
            },
            [Theme.Wheels] = new ThemeProfile
            {
                Theme       = Theme.Wheels,
                Name        = "Wheels",
                Description = "Wheel effects refill hands repeatedly; discard-matters payoffs convert the chaos.",
                Group       = ThemeGroup.Spells,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.CardAdvantage]      = +5,
                    [CardRole.Plan]               = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -4,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Ramp]               = -2,
                    [CardRole.Protection]         = -2,
                },
            },
            [Theme.Cycling] = new ThemeProfile
            {
                Theme       = Theme.Cycling,
                Name        = "Cycling",
                Description = "Cycle through the deck efficiently; cycling triggers and payoffs generate value.",
                Group       = ThemeGroup.Spells,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.CardAdvantage]      = +4,
                    [CardRole.Plan]               = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Ramp]               = -2,
                    [CardRole.Protection]         = -2,
                },
            },
            [Theme.Discard] = new ThemeProfile
            {
                Theme       = Theme.Discard,
                Name        = "Discard",
                Description = "Discard your own cards for madness, looter value, and hand-emptying payoffs.",
                Group       = ThemeGroup.Spells,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +3,
                    [CardRole.CardAdvantage]      = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Ramp]               = -2,
                    [CardRole.Protection]         = -2,
                },
            },
            [Theme.XSpells] = new ThemeProfile
            {
                Theme       = Theme.XSpells,
                Name        = "X Spells",
                Description = "Ramp to overwhelming mana, then spend it on large-X instants and sorceries.",
                Group       = ThemeGroup.Spells,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Ramp]               = +4,
                    [CardRole.CardAdvantage]      = +2,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Payoff]             = -2,
                    [CardRole.Protection]         = -2,
                },
            },
            [Theme.Cascade] = new ThemeProfile
            {
                Theme       = Theme.Cascade,
                Name        = "Cascade",
                Description = "Chain cascade triggers to cast spells for free; optimize the mana-value curve.",
                Group       = ThemeGroup.Spells,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.CardAdvantage]      = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Ramp]               = -2,
                    [CardRole.Protection]         = -2,
                },
            },
            [Theme.SpellCopy] = new ThemeProfile
            {
                Theme       = Theme.SpellCopy,
                Name        = "Spell Copy",
                Description = "Fork and copy spells for double (or more) value; scales well with high-impact instants.",
                Group       = ThemeGroup.Spells,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.CardAdvantage]      = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.ExtraTurns] = new ThemeProfile
            {
                Theme       = Theme.ExtraTurns,
                Name        = "Extra Turns",
                Description = "Take additional turns to untap, draw, and attack repeatedly.",
                Group       = ThemeGroup.Spells,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.CardAdvantage]      = +3,
                    [CardRole.Payoff]             = +2,
                    [CardRole.TargetedDisruption] = -4,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.Ramp]               = -1,
                },
            },

            // ── Lands ─────────────────────────────────────────────────────────
            [Theme.BigMana] = new ThemeProfile
            {
                Theme       = Theme.BigMana,
                Name        = "Big Mana",
                Description = "Ramp hard, then overpower the table with expensive spells.",
                Group       = ThemeGroup.Lands,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Land]               = +2,
                    [CardRole.Ramp]               = +5,
                    [CardRole.Plan]               = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -2,
                    [CardRole.Synergy]            = -3,
                },
            },
            [Theme.Lands] = new ThemeProfile
            {
                Theme       = Theme.Lands,
                Name        = "Lands",
                Description = "Extra land drops, Landfall triggers, and lands-matter payoffs dominate.",
                Group       = ThemeGroup.Lands,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Ramp]               = +4,
                    [CardRole.Plan]               = +3,
                    [CardRole.Land]               = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -4,
                    [CardRole.Protection]         = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Tutor]              = -2,
                },
            },
            [Theme.Landfall] = new ThemeProfile
            {
                Theme       = Theme.Landfall,
                Name        = "Landfall",
                Description = "Trigger landfall abilities each time a land enters; extra land drops multiply value.",
                Group       = ThemeGroup.Lands,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Land]               = +2,
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +3,
                    [CardRole.Ramp]               = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -4,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -3,
                    [CardRole.CardAdvantage]      = -2,
                },
            },

            // ── Counters ──────────────────────────────────────────────────────
            [Theme.CountersMatter] = new ThemeProfile
            {
                Theme       = Theme.CountersMatter,
                Name        = "Counters Matter",
                Description = "All counter types matter — loyalty, experience, charge, and +1/+1; build synergies across the board.",
                Group       = ThemeGroup.Counters,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Synergy]            = +4,
                    [CardRole.Plan]               = +3,
                    [CardRole.Payoff]             = +2,
                    [CardRole.CardAdvantage]      = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.PlusOneCounters] = new ThemeProfile
            {
                Theme       = Theme.PlusOneCounters,
                Name        = "+1/+1 Counters",
                Description = "Grow creatures with +1/+1 counters; doubling effects and proliferate scale threats and convert to wins.",
                Group       = ThemeGroup.Counters,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Synergy]            = +5,
                    [CardRole.Plan]               = +3,
                    [CardRole.Payoff]             = +3,
                    [CardRole.CardAdvantage]      = +1,
                    [CardRole.MassDisruption]     = -4,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.Protection]         = -3,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.MinusOneCounters] = new ThemeProfile
            {
                Theme       = Theme.MinusOneCounters,
                Name        = "-1/-1 Counters",
                Description = "Wither, Persist, and -1/-1 counter synergies shrink opposing creatures and trigger recursive value.",
                Group       = ThemeGroup.Counters,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.Synergy]            = +4,
                    [CardRole.Payoff]             = +2,
                    [CardRole.MassDisruption]     = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.Protection]         = -3,
                    [CardRole.Ramp]               = -3,
                    [CardRole.CardAdvantage]      = -3,
                },
            },
            [Theme.Proliferate] = new ThemeProfile
            {
                Theme       = Theme.Proliferate,
                Name        = "Proliferate",
                Description = "Multiply counters across all permanent types; accelerates planeswalkers and +1/+1 strategies.",
                Group       = ThemeGroup.Counters,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Synergy]            = +4,
                    [CardRole.Plan]               = +3,
                    [CardRole.Payoff]             = +2,
                    [CardRole.CardAdvantage]      = +1,
                    [CardRole.MassDisruption]     = -4,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.Ramp]               = -1,
                },
            },
            [Theme.Infect] = new ThemeProfile
            {
                Theme       = Theme.Infect,
                Name        = "Infect",
                Description = "10 poison counters wins the game; protect the infect creature and pump it.",
                Group       = ThemeGroup.Counters,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Protection]         = +4,
                    [CardRole.Plan]               = +4,
                    [CardRole.Payoff]             = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.MassDisruption]     = -4,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.Ramp]               = -3,
                    [CardRole.CardAdvantage]      = -3,
                },
            },
            [Theme.Energy] = new ThemeProfile
            {
                Theme       = Theme.Energy,
                Name        = "Energy",
                Description = "Generate energy counters and spend them on powerful payoff abilities.",
                Group       = ThemeGroup.Counters,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +4,
                    [CardRole.Payoff]             = +2,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.CardAdvantage]      = -2,
                    [CardRole.Ramp]               = -2,
                },
            },

            // ── Combat ────────────────────────────────────────────────────────
            [Theme.Voltron] = new ThemeProfile
            {
                Theme       = Theme.Voltron,
                Name        = "Voltron",
                Description = "Suit up a single threat (often the commander) and protect it.",
                Group       = ThemeGroup.Combat,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Protection]         = +5,
                    [CardRole.Plan]               = +3,
                    [CardRole.TargetedDisruption] = +1,
                    [CardRole.Payoff]             = -5,
                },
            },
            [Theme.Burn] = new ThemeProfile
            {
                Theme       = Theme.Burn,
                Name        = "Burn",
                Description = "Deal direct damage with spells and creatures; accumulate small hits until opponents fall.",
                Group       = ThemeGroup.Combat,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.CardAdvantage]      = +2,
                    [CardRole.TargetedDisruption] = +1,
                    [CardRole.Synergy]            = +1,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -3,
                    [CardRole.Recursion]          = -2,
                    [CardRole.Ramp]               = -1,
                },
            },
            [Theme.ExtraCombats] = new ThemeProfile
            {
                Theme       = Theme.ExtraCombats,
                Name        = "Extra Combats",
                Description = "Take multiple combat phases per turn; attack-trigger payoffs multiply with each step.",
                Group       = ThemeGroup.Combat,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.Payoff]             = +3,
                    [CardRole.Protection]         = +2,
                    [CardRole.Synergy]            = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.CardAdvantage]      = -3,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.AttackTriggers] = new ThemeProfile
            {
                Theme       = Theme.AttackTriggers,
                Name        = "Attack Triggers",
                Description = "Creatures and enchantments that trigger on attack generate sustained value each combat.",
                Group       = ThemeGroup.Combat,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Payoff]             = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Protection]         = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.CardAdvantage]      = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.ForcedCombat] = new ThemeProfile
            {
                Theme       = Theme.ForcedCombat,
                Name        = "Forced Combat",
                Description = "Goad opponents' creatures and force attacks; let enemies weaken each other.",
                Group       = ThemeGroup.Combat,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.MassDisruption]     = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -2,
                    [CardRole.Protection]         = -3,
                    [CardRole.Ramp]               = -3,
                    [CardRole.CardAdvantage]      = -2,
                },
            },
            [Theme.Theft] = new ThemeProfile
            {
                Theme       = Theme.Theft,
                Name        = "Theft",
                Description = "Steal opponents' permanents and spells; turn their best cards against them.",
                Group       = ThemeGroup.Combat,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.TargetedDisruption] = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.Protection]         = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Recursion]          = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Ninjutsu] = new ThemeProfile
            {
                Theme       = Theme.Ninjutsu,
                Name        = "Ninjutsu",
                Description = "Return unblocked attackers to sneak in ninjas; repeat ninjutsu for value each combat.",
                Group       = ThemeGroup.Combat,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +3,
                    [CardRole.CardAdvantage]      = +3,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.Ramp]               = -2,
                },
            },

            // ── Politics & Control ────────────────────────────────────────────
            [Theme.Stax] = new ThemeProfile
            {
                Theme       = Theme.Stax,
                Name        = "Stax",
                Description = "Lock down opponents' resources and tempo; win in a slowed game.",
                Group       = ThemeGroup.PoliticsAndControl,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.MassDisruption]     = +5,
                    [CardRole.TargetedDisruption] = +3,
                    [CardRole.Protection]         = +2,
                    [CardRole.CardAdvantage]      = +1,
                    [CardRole.Payoff]             = -4,
                    [CardRole.Plan]               = -3,
                    [CardRole.Synergy]            = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Pillowfort] = new ThemeProfile
            {
                Theme       = Theme.Pillowfort,
                Name        = "Pillowfort",
                Description = "Deter attacks with damage prevention and taxing effects; win behind the fort.",
                Group       = ThemeGroup.PoliticsAndControl,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Protection]         = +5,
                    [CardRole.MassDisruption]     = +2,
                    [CardRole.CardAdvantage]      = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.Plan]               = -3,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.Synergy]            = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.GroupHug] = new ThemeProfile
            {
                Theme       = Theme.GroupHug,
                Name        = "Group Hug",
                Description = "Share resources politically; win off the excess created by helping opponents.",
                Group       = ThemeGroup.PoliticsAndControl,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.CardAdvantage]      = +4,
                    [CardRole.Ramp]               = +3,
                    [CardRole.Plan]               = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -4,
                    [CardRole.MassDisruption]     = -4,
                    [CardRole.Synergy]            = -2,
                },
            },
            [Theme.Mill] = new ThemeProfile
            {
                Theme       = Theme.Mill,
                Name        = "Mill",
                Description = "Deck out opponents with targeted mill; graveyard synergies as a bonus.",
                Group       = ThemeGroup.PoliticsAndControl,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.Synergy]            = +3,
                    [CardRole.CardAdvantage]      = +1,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Chaos] = new ThemeProfile
            {
                Theme       = Theme.Chaos,
                Name        = "Chaos",
                Description = "Random effects and chaos permanents disrupt opponents unpredictably.",
                Group       = ThemeGroup.PoliticsAndControl,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +3,
                    [CardRole.MassDisruption]     = +2,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.CardAdvantage]      = +1,
                    [CardRole.Protection]         = -4,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.GroupSlug] = new ThemeProfile
            {
                Theme       = Theme.GroupSlug,
                Name        = "Group Slug",
                Description = "Everyone takes damage or loses life each turn; stay alive while the table bleeds out.",
                Group       = ThemeGroup.PoliticsAndControl,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.Payoff]             = +3,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Protection]         = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -2,
                    [CardRole.CardAdvantage]      = -3,
                    [CardRole.Ramp]               = -3,
                },
            },
            [Theme.Hatebears] = new ThemeProfile
            {
                Theme       = Theme.Hatebears,
                Name        = "Hatebears",
                Description = "Creatures that tax, restrict, or punish opponents' strategies; win while disrupting.",
                Group       = ThemeGroup.PoliticsAndControl,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.MassDisruption]     = +4,
                    [CardRole.TargetedDisruption] = +3,
                    [CardRole.Protection]         = +2,
                    [CardRole.CardAdvantage]      = +1,
                    [CardRole.Payoff]             = -4,
                    [CardRole.Plan]               = -2,
                    [CardRole.Synergy]            = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Monarch] = new ThemeProfile
            {
                Theme       = Theme.Monarch,
                Name        = "Monarch",
                Description = "Become the monarch to draw an extra card each turn; defend the crown or retake it.",
                Group       = ThemeGroup.PoliticsAndControl,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +3,
                    [CardRole.CardAdvantage]      = +3,
                    [CardRole.Protection]         = +2,
                    [CardRole.Synergy]            = +2,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Payoff]             = -2,
                    [CardRole.Ramp]               = -2,
                },
            },

            // ── Synergy ───────────────────────────────────────────────────────
            [Theme.Lifegain] = new ThemeProfile
            {
                Theme       = Theme.Lifegain,
                Name        = "Lifegain",
                Description = "Convert life total padding into card advantage and incremental board presence.",
                Group       = ThemeGroup.Synergy,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +3,
                    [CardRole.Synergy]            = +1,
                    [CardRole.Payoff]             = +3,
                    [CardRole.Recursion]          = +1,
                    [CardRole.TargetedDisruption] = -2,
                },
            },
            [Theme.Blink] = new ThemeProfile
            {
                Theme       = Theme.Blink,
                Name        = "Blink",
                Description = "Flicker ETB creatures repeatedly for incremental value.",
                Group       = ThemeGroup.Synergy,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Synergy]            = +4,
                    [CardRole.Plan]               = +3,
                    [CardRole.Recursion]          = +2,
                    [CardRole.CardAdvantage]      = +1,
                    [CardRole.Payoff]             = -4,
                    [CardRole.Ramp]               = -2,
                    [CardRole.TargetedDisruption] = -2,
                    [CardRole.MassDisruption]     = -2,
                },
            },
            [Theme.Legends] = new ThemeProfile
            {
                Theme       = Theme.Legends,
                Name        = "Legends",
                Description = "Legendary permanents trigger payoffs; density of legends matters.",
                Group       = ThemeGroup.Synergy,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +3,
                    [CardRole.CardAdvantage]      = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Clones] = new ThemeProfile
            {
                Theme       = Theme.Clones,
                Name        = "Clones",
                Description = "Copy the best creature on any battlefield; value scales with the power of available targets.",
                Group       = ThemeGroup.Synergy,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +3,
                    [CardRole.CardAdvantage]      = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -2,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Ramp]               = -3,
                    [CardRole.Protection]         = -2,
                },
            },
            [Theme.Historic] = new ThemeProfile
            {
                Theme       = Theme.Historic,
                Name        = "Historic",
                Description = "Artifacts, legends, and sagas matter together; any historic permanent triggers payoffs.",
                Group       = ThemeGroup.Synergy,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +3,
                    [CardRole.Synergy]            = +4,
                    [CardRole.CardAdvantage]      = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Ramp]               = -2,
                    [CardRole.Protection]         = -2,
                },
            },
            [Theme.CommanderMatters] = new ThemeProfile
            {
                Theme       = Theme.CommanderMatters,
                Name        = "Commander Matters",
                Description = "Build around the commander's specific mechanic; protection and tutors keep it online.",
                Group       = ThemeGroup.Synergy,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Protection]         = +3,
                    [CardRole.Synergy]            = +3,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -4,
                    [CardRole.CardAdvantage]      = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Exile] = new ThemeProfile
            {
                Theme       = Theme.Exile,
                Name        = "Exile",
                Description = "Cast cards from exile via impulse draw and exile-matters payoffs; Prosper and Gonti style.",
                Group       = ThemeGroup.Synergy,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +3,
                    [CardRole.CardAdvantage]      = +3,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -3,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.ToughnessMatter] = new ThemeProfile
            {
                Theme       = Theme.ToughnessMatter,
                Name        = "Toughness Matters",
                Description = "Creatures fight with toughness as power (Doran, Assault Formation); scale payoffs on toughness.",
                Group       = ThemeGroup.Synergy,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +4,
                    [CardRole.Payoff]             = +2,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.CardAdvantage]      = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Etb] = new ThemeProfile
            {
                Theme       = Theme.Etb,
                Name        = "ETB",
                Description = "Pack the deck with powerful enter-the-battlefield effects; trigger them repeatedly.",
                Group       = ThemeGroup.Synergy,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +3,
                    [CardRole.CardAdvantage]      = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Clues] = new ThemeProfile
            {
                Theme       = Theme.Clues,
                Name        = "Clues",
                Description = "Investigate to create Clue tokens; sacrifice them for card draw and artifact payoffs.",
                Group       = ThemeGroup.Synergy,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +3,
                    [CardRole.CardAdvantage]      = +4,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Food] = new ThemeProfile
            {
                Theme       = Theme.Food,
                Name        = "Food",
                Description = "Generate Food tokens for life gain and artifact payoffs; sacrifice for value beyond healing.",
                Group       = ThemeGroup.Synergy,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +3,
                    [CardRole.Synergy]            = +3,
                    [CardRole.Protection]         = +2,
                    [CardRole.Payoff]             = +2,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.CardAdvantage]      = -2,
                    [CardRole.Ramp]               = -2,
                },
            },

            // ── Tribal ────────────────────────────────────────────────────────
            [Theme.Tribal] = new ThemeProfile
            {
                Theme       = Theme.Tribal,
                Name        = "Tribal / Typal",
                Description = "Creature type lords and tribal payoffs reward a focused creature base. Specify a creature type for EDHREC enrichment.",
                Group       = ThemeGroup.Tribal,
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +3,
                    [CardRole.Payoff]             = +2,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Ramp]               = -2,
                    [CardRole.CardAdvantage]      = -1,
                },
            },
        };

    public static ThemeProfile Get(Theme theme) => All[theme];
}
