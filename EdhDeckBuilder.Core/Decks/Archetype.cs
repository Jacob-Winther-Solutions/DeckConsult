using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Core.Decks;

/// <summary>
/// The four primary play-style archetypes. These describe *how* a deck wins, not *what* it does
/// thematically — use <see cref="Theme"/> for thematic identity (Voltron, Aristocrats, etc.).
/// Archetypes compose: Tempo is Aggro + Control; Midrange is positioned between Aggro and Control.
/// </summary>
public enum Archetype
{
    Control,
    Aggro,
    Combo,
    Midrange,
}

/// <summary>
/// An archetype expressed as a <em>delta</em> over the neutral baseline — "more of this, less of
/// that." Modeling archetypes as adjustments lets them compose: blending two archetypes is just
/// summing their weighted adjustments. Values are shifts to a role's ideal count; roles not
/// mentioned are left at baseline.
/// </summary>
public sealed record ArchetypeProfile
{
    public required Archetype Archetype { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required IReadOnlyDictionary<CardRole, int> Adjustments { get; init; }
}

/// <summary>An archetype paired with how strongly it applies (1.0 = full, 0.5 = a splash of it).</summary>
public readonly record struct WeightedArchetype(ArchetypeProfile Profile, double Weight = 1.0);

/// <summary>The built-in archetype profiles. Add new entries here as data, not code.</summary>
public static class ArchetypeLibrary
{
    public static IReadOnlyDictionary<Archetype, ArchetypeProfile> All { get; } =
        new[]
        {
            new ArchetypeProfile
            {
                Archetype = Archetype.Control,
                Name = "Control",
                Description = "Win late through card advantage and dense interaction.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.CardAdvantage]      = +4,
                    [CardRole.TargetedDisruption] = +4,
                    [CardRole.MassDisruption]     = +2,
                    [CardRole.Protection]         = +2,
                    [CardRole.Plan]               = -2,  // reactive decks invest less in proactive plan pieces
                    [CardRole.Payoff]             = -5,
                    [CardRole.Synergy]            = -1,
                },
            },
            new ArchetypeProfile
            {
                Archetype = Archetype.Aggro,
                Name = "Aggro",
                Description = "Low curve, lots of threats, end the game before it goes long.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Land]               = -3,
                    [CardRole.Ramp]               = -3,
                    [CardRole.Plan]               = +5,  // aggro threats are the plan (Goblin Bushwhacker, etc.)
                    [CardRole.Payoff]             = +3,
                    [CardRole.Synergy]            = +1,
                    [CardRole.CardAdvantage]      = -1,
                },
            },
            new ArchetypeProfile
            {
                Archetype = Archetype.Combo,
                Name = "Combo",
                Description = "Assemble and protect a game-ending interaction.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Tutor]              = +5,
                    [CardRole.Protection]         = +3,
                    [CardRole.CardAdvantage]      = +2,
                    [CardRole.Plan]               = +4,  // combo pieces are the plan
                    [CardRole.Payoff]             = -5,
                    [CardRole.TargetedDisruption] = -2,
                },
            },
            new ArchetypeProfile
            {
                Archetype = Archetype.Midrange,
                Name = "Midrange",
                Description = "Resilient, efficient threats with enough interaction to pivot between roles.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Plan]               = +2,
                    [CardRole.Payoff]             = +2,
                    [CardRole.TargetedDisruption] = +2,
                    [CardRole.CardAdvantage]      = +2,
                    [CardRole.Ramp]               = +1,
                    [CardRole.Synergy]            = -2,
                },
            },
        }.ToDictionary(p => p.Archetype);

    public static ArchetypeProfile Get(Archetype archetype) => All[archetype];
}
