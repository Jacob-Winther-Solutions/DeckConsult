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
    /// A sensible midrange starting point informed by the Command Zone methodology. Ideals sum to
    /// exactly 99 so the resolver applies no scaling in the unmodified case. Tune per commander
    /// and strategy via archetypes and themes.
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
            [CardRole.Payoff]             = new(8,  12, 18),
            [CardRole.Synergy]            = new(5,  10, 18),
        },
    };
}
