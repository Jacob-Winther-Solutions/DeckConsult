using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Core.Decks;

/// <summary>An inclusive target range for how many cards of a given role a deck should aim for.</summary>
public readonly record struct RoleTarget(int Min, int Ideal, int Max);

/// <summary>
/// A "stable deck" template — the soft targets that guide construction. These are guidelines, not
/// hard rules: the rules engine enforces format legality, while templates shape what a well-rounded
/// build looks like.
/// </summary>
public sealed record DeckTemplate
{
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required IReadOnlyDictionary<CardRole, RoleTarget> Targets { get; init; }

    /// <summary>
    /// A sensible midrange starting point informed by the Command Zone methodology.
    /// These are <em>coverage</em> targets, not physical slot counts: cards with secondary roles
    /// satisfy multiple targets simultaneously, so ideals intentionally sum above 99 (~107 here).
    /// The physical 99-card constraint is enforced by the fill engine, which treats these as
    /// coverage objectives. Tune per commander and strategy via archetypes, themes, and bracket.
    /// </summary>
    public static DeckTemplate Balanced { get; } = new()
    {
        Name = "Balanced Midrange",
        Description = "General-purpose template suitable for most casual-to-mid-power commanders.",
        Targets = new Dictionary<CardRole, RoleTarget>
        {
            [CardRole.Land]               = new(36, 38, 40),
            [CardRole.Ramp]               = new(8,  10, 12),
            [CardRole.CardAdvantage]      = new(8,  10, 12),
            [CardRole.TargetedDisruption] = new(8,  10, 12),
            [CardRole.MassDisruption]     = new(4,  6,  8),
            [CardRole.Protection]         = new(2,  3,  5),
            [CardRole.Tutor]              = new(0,  2,  4),
            [CardRole.Plan]               = new(10, 14, 20),
            [CardRole.Payoff]             = new(6,  9,  14),
            [CardRole.Synergy]            = new(3,  5,  10),
        },
    };
}
