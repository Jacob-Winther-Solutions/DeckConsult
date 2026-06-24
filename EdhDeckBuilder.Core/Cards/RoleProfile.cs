namespace EdhDeckBuilder.Core.Cards;

/// <summary>
/// How a card relates to a role it isn't single-mindedly dedicated to — the <em>shape</em> of an overlap.
/// </summary>
public enum RoleRelation
{
    /// <summary>
    /// Fills the role outright. Used for a card's primary role, and for cards that serve two roles
    /// at the same time — e.g. Black Market Connections is ramp AND card draw simultaneously.
    /// </summary>
    Always,

    /// <summary>
    /// Modal: a mode is chosen when the card is played, so it fills this role only some of the time
    /// — e.g. Jeska's Will (no commander on board) is a ritual OR card advantage, not both.
    /// </summary>
    Modal,

    /// <summary>
    /// Sequential: serves one role, then converts to another over the course of a game
    /// — e.g. Hedron Archive ramps early, then is sacrificed to draw later.
    /// </summary>
    Transform,
}

/// <summary>
/// A card's contribution to a single role. <see cref="Weight"/> is coverage credit in [0, 1]:
/// how much of a "dedicated" card of that role this counts as. Sensible defaults: Always ≈ 1.0,
/// Modal ≈ 0.5 (expected value of the choice), Transform ≈ 0.75 (it gets there, just not at once).
/// The classifier produces the actual values, in deck context.
/// </summary>
public readonly record struct RoleContribution(CardRole Role, RoleRelation Relation, double Weight)
{
    public static RoleContribution Both(CardRole role, double weight = 1.0)
        => new(role, RoleRelation.Always, weight);

    public static RoleContribution EitherOr(CardRole role, double weight = 0.5)
        => new(role, RoleRelation.Modal, weight);

    public static RoleContribution Switches(CardRole role, double weight = 0.75)
        => new(role, RoleRelation.Transform, weight);
}

/// <summary>
/// Every role a card fills in its deck. <see cref="Primary"/> is the single bucket used for the
/// physical 99-card count and the visual grouping — each card is shown exactly once.
/// <see cref="Secondary"/> lists the other roles it also covers; this is precisely what lets a
/// deck's role coverage legitimately add up to more than 99.
/// </summary>
public sealed record RoleProfile
{
    public required CardRole Primary { get; init; }

    /// <summary>Additional roles the card covers, beyond its primary. Should not repeat the primary.</summary>
    public IReadOnlyList<RoleContribution> Secondary { get; init; } = [];

    public static RoleProfile Of(CardRole primary) => new() { Primary = primary };

    public RoleProfile With(params RoleContribution[] secondary)
        => this with { Secondary = secondary };

    /// <summary>Coverage credit this card contributes to <paramref name="role"/> (its primary counts as 1.0).</summary>
    public double CoverageFor(CardRole role)
    {
        double credit = Primary == role ? 1.0 : 0.0;
        foreach (var c in Secondary)
            if (c.Role == role) credit += c.Weight;
        return credit;
    }

    /// <summary>The distinct set of roles this card touches at all.</summary>
    public IEnumerable<CardRole> AllRoles()
        => Secondary.Select(c => c.Role).Prepend(Primary).Distinct();
}
