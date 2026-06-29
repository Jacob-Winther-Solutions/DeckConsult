using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Core.Decks;

/// <summary>
/// The thematic identity of a deck — *what* it does, as opposed to <see cref="Archetype"/>, which
/// describes *how* it wins. A theme can be paired with any archetype: Aristocrats-Combo, Voltron-
/// Aggro, or Reanimator-Control are all coherent. The two axes are independent and both expressed
/// as adjustments over the same baseline, so they compose freely via <see cref="TemplateResolver"/>.
/// </summary>
public enum Theme
{
    BigMana,
    Aristocrats,
    Voltron,
    Tokens,
    Lifegain,
    Reanimator,
    Spellslinger,
    Blink,
    Counters,
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
    Clones,
    Wheels,
    Infect,
    Artifacts,
    Superfriends,
    Chaos,
    DrawGo,
    Stompy,
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
}

/// <summary>A theme paired with how strongly it applies (1.0 = full, 0.5 = a splash of it).</summary>
public readonly record struct WeightedTheme(ThemeProfile Profile, double Weight = 1.0);

/// <summary>The built-in theme profiles. Add new entries here as data, not code.</summary>
public static class ThemeLibrary
{
    public static IReadOnlyDictionary<Theme, ThemeProfile> All { get; } =
        new Dictionary<Theme, ThemeProfile>
        {
            [Theme.BigMana] = new ThemeProfile
            {
                Theme       = Theme.BigMana,
                Name        = "Big Mana",
                Description = "Ramp hard, then overpower the table with expensive spells.",
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
            [Theme.Aristocrats] = new ThemeProfile
            {
                Theme       = Theme.Aristocrats,
                Name        = "Aristocrats",
                Description = "Sacrifice synergies and incremental drain; values recursion heavily.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,
                    [CardRole.Synergy]            = +2,
                    [CardRole.Recursion]          = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -1,
                },
            },
            [Theme.Voltron] = new ThemeProfile
            {
                Theme       = Theme.Voltron,
                Name        = "Voltron",
                Description = "Suit up a single threat (often the commander) and protect it.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Protection]         = +5,
                    [CardRole.Plan]               = +3,
                    [CardRole.TargetedDisruption] = +1,
                    [CardRole.Payoff]             = -5,
                },
            },
            [Theme.Tokens] = new ThemeProfile
            {
                Theme       = Theme.Tokens,
                Name        = "Tokens",
                Description = "Go wide with creature tokens and anthems/payoffs that scale on count.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]           = +5,
                    [CardRole.Payoff]         = +3,
                    [CardRole.MassDisruption] = -1,
                },
            },
            [Theme.Lifegain] = new ThemeProfile
            {
                Theme       = Theme.Lifegain,
                Name        = "Lifegain",
                Description = "Convert life total padding into card advantage and incremental board presence.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +3,
                    [CardRole.Synergy]            = +1,
                    [CardRole.Payoff]             = +3,
                    [CardRole.Recursion]          = +1,
                    [CardRole.TargetedDisruption] = -2,
                },
            },
            [Theme.Reanimator] = new ThemeProfile
            {
                Theme       = Theme.Reanimator,
                Name        = "Reanimator",
                Description = "Fill the graveyard cheaply, then cheat large threats back into play.",
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
            [Theme.Spellslinger] = new ThemeProfile
            {
                Theme       = Theme.Spellslinger,
                Name        = "Spellslinger",
                Description = "Cast triggers and spell density generate value; maximize instant/sorcery count.",
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
            [Theme.Blink] = new ThemeProfile
            {
                Theme       = Theme.Blink,
                Name        = "Blink",
                Description = "Flicker ETB creatures repeatedly for incremental value.",
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
            [Theme.Counters] = new ThemeProfile
            {
                Theme       = Theme.Counters,
                Name        = "Counters",
                Description = "+1/+1 counter synergies and doubling effects; grow threats and convert to wins.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Synergy]            = +4,
                    [CardRole.Plan]               = +3,
                    [CardRole.Payoff]             = +2,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                    [CardRole.Ramp]               = -1,
                },
            },
            [Theme.Enchantress] = new ThemeProfile
            {
                Theme       = Theme.Enchantress,
                Name        = "Enchantress",
                Description = "Enchantments trigger card draw; constellation and enchantment payoffs close the game.",
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
            [Theme.Tribal] = new ThemeProfile
            {
                Theme       = Theme.Tribal,
                Name        = "Tribal",
                Description = "Creature type lords and tribal payoffs reward a focused creature base.",
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
            [Theme.Lands] = new ThemeProfile
            {
                Theme       = Theme.Lands,
                Name        = "Lands",
                Description = "Extra land drops, Landfall triggers, and lands-matter payoffs dominate.",
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
            [Theme.Graveyard] = new ThemeProfile
            {
                Theme       = Theme.Graveyard,
                Name        = "Graveyard",
                Description = "Self-mill and graveyard value engines; persistent threats that ignore removal.",
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
            [Theme.Storm] = new ThemeProfile
            {
                Theme       = Theme.Storm,
                Name        = "Storm",
                Description = "Chain spells to hit critical mass; storm count wins the game in one turn.",
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
            [Theme.Proliferate] = new ThemeProfile
            {
                Theme       = Theme.Proliferate,
                Name        = "Proliferate",
                Description = "Multiply counters across all permanent types; accelerates planeswalkers and +1/+1 strategies.",
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
            [Theme.Stax] = new ThemeProfile
            {
                Theme       = Theme.Stax,
                Name        = "Stax",
                Description = "Lock down opponents' resources and tempo; win in a slowed game.",
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
            [Theme.Cycling] = new ThemeProfile
            {
                Theme       = Theme.Cycling,
                Name        = "Cycling",
                Description = "Cycle through the deck efficiently; cycling triggers and payoffs generate value.",
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
            [Theme.Clones] = new ThemeProfile
            {
                Theme       = Theme.Clones,
                Name        = "Clones",
                Description = "Copy and duplicate the best creatures on the board; scale off opponents' threats.",
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
            [Theme.Wheels] = new ThemeProfile
            {
                Theme       = Theme.Wheels,
                Name        = "Wheels",
                Description = "Wheel effects refill hands repeatedly; discard-matters payoffs convert the chaos.",
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
            [Theme.Infect] = new ThemeProfile
            {
                Theme       = Theme.Infect,
                Name        = "Infect",
                Description = "10 poison counters wins the game; protect the infect creature and pump it.",
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
            [Theme.Artifacts] = new ThemeProfile
            {
                Theme       = Theme.Artifacts,
                Name        = "Artifacts",
                Description = "Artifact synergies and mana rocks form the engine; value scales with artifact count.",
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
            [Theme.Chaos] = new ThemeProfile
            {
                Theme       = Theme.Chaos,
                Name        = "Chaos",
                Description = "Random effects and chaos permanents disrupt opponents unpredictably.",
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
            [Theme.DrawGo] = new ThemeProfile
            {
                Theme       = Theme.DrawGo,
                Name        = "Draw-Go",
                Description = "Reactive play at instant speed; counter and remove, win slowly.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.CardAdvantage]      = +5,
                    [CardRole.TargetedDisruption] = +4,
                    [CardRole.MassDisruption]     = +2,
                    [CardRole.Protection]         = +2,
                    [CardRole.Plan]               = -4,
                    [CardRole.Payoff]             = -4,
                    [CardRole.Synergy]            = -3,
                    [CardRole.Ramp]               = -2,
                },
            },
            [Theme.Stompy] = new ThemeProfile
            {
                Theme       = Theme.Stompy,
                Name        = "Stompy",
                Description = "Big threats that go tall; beat face with large creatures that need no setup.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +5,
                    [CardRole.Payoff]             = +3,
                    [CardRole.Ramp]               = +2,
                    [CardRole.Synergy]            = +1,
                    [CardRole.CardAdvantage]      = -3,
                    [CardRole.TargetedDisruption] = -3,
                    [CardRole.MassDisruption]     = -3,
                    [CardRole.Protection]         = -2,
                },
            },
        };

    public static ThemeProfile Get(Theme theme) => All[theme];
}
