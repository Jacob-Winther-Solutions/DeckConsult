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
}

/// <summary>
/// A theme expressed as a <em>delta</em> over the neutral baseline — parallel to
/// <see cref="ArchetypeProfile"/> but representing thematic identity rather than play-style.
/// </summary>
public sealed record ThemeProfile
{
    public required Theme Theme { get; init; }
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
        new[]
        {
            new ThemeProfile
            {
                Theme = Theme.BigMana,
                Name = "Big Mana",
                Description = "Ramp hard, then overpower the table with expensive spells.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Land]               = +2,
                    [CardRole.Ramp]               = +5,
                    [CardRole.Plan]               = +2,  // the expensive spells themselves are the plan
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -2,
                    [CardRole.Synergy]            = -3,
                },
            },
            new ThemeProfile
            {
                Theme = Theme.Aristocrats,
                Name = "Aristocrats",
                Description = "Sacrifice synergies and incremental drain; values recursion heavily.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +4,  // sacrifice outlets and aristocrat creatures are the plan
                    [CardRole.Synergy]            = +2,  // trigger effects and death-matters engines
                    [CardRole.Recursion]          = +2,
                    [CardRole.Payoff]             = +1,
                    [CardRole.TargetedDisruption] = -1,
                },
            },
            new ThemeProfile
            {
                Theme = Theme.Voltron,
                Name = "Voltron",
                Description = "Suit up a single threat (often the commander) and protect it.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Protection]         = +5,
                    [CardRole.Plan]               = +3,  // equipment and auras are the plan
                    [CardRole.TargetedDisruption] = +1,
                    [CardRole.Payoff]             = -5,
                },
            },
            new ThemeProfile
            {
                Theme = Theme.Tokens,
                Name = "Tokens",
                Description = "Go wide with creature tokens and anthems/payoffs that scale on count.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]           = +5,  // token-making spells are the plan (Raise the Alarm, etc.)
                    [CardRole.Payoff]         = +3,
                    [CardRole.MassDisruption] = -1,
                },
            },
            new ThemeProfile
            {
                Theme = Theme.Lifegain,
                Name = "Lifegain",
                Description = "Convert life total padding into card advantage and incremental board presence.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +3,  // life-gaining spells are the plan
                    [CardRole.Synergy]            = +1,  // "whenever you gain life" triggers
                    [CardRole.Payoff]             = +3,
                    [CardRole.Recursion]          = +1,
                    [CardRole.TargetedDisruption] = -2,
                },
            },
            new ThemeProfile
            {
                Theme = Theme.Reanimator,
                Name = "Reanimator",
                Description = "Fill the graveyard cheaply, then cheat large threats back into play.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Recursion]          = +4,  // reanimation spells
                    [CardRole.Plan]               = +3,  // self-mill and discard enablers set up the graveyard
                    [CardRole.Synergy]            = +1,
                    [CardRole.Payoff]             = +2,
                    [CardRole.Ramp]               = -2,
                    [CardRole.TargetedDisruption] = -1,
                },
            },
        }.ToDictionary(p => p.Theme);

    public static ThemeProfile Get(Theme theme) => All[theme];
}
