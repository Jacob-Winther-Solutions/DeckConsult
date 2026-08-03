using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Core.Decks;

/// <summary>
/// A single entry in the deck. Quantity is 1 for everything except basic lands, which the
/// singleton rule exempts. Carries the full role profile — one primary bucket plus any
/// secondary roles the card overlaps into.
/// </summary>
public sealed record DeckSlot
{
    public required Card Card { get; init; }
    public int Quantity { get; init; } = 1;

    /// <summary>Primary role plus any overlapping secondary roles.</summary>
    public RoleProfile Roles { get; init; } = RoleProfile.Of(CardRole.Unclassified);

    public ClassificationSource RoleSource { get; init; } = ClassificationSource.Manual;

    /// <summary>0–1 confidence in the classification (mainly meaningful for LLM/heuristic sources).</summary>
    public double RoleConfidence { get; init; } = 1.0;

    /// <summary>True when the user explicitly locked this card into the deck before the build started.</summary>
    public bool IsLocked { get; init; } = false;

    /// <summary>The single bucket used for the physical 99-card count and the visual grouping.</summary>
    public CardRole PrimaryRole => Roles.Primary;
}

/// <summary>
/// A Commander deck: one or two commanders (partner / background) plus the rest of the cards.
/// The deck's color identity is the union of its commanders' identities.
/// </summary>
public sealed class Deck
{
    public required string Name { get; set; }

    /// <summary>One commander normally; two for partner / background pairings.</summary>
    public required IReadOnlyList<Card> Commanders { get; init; }

    public List<DeckSlot> Cards { get; init; } = [];

    /// <summary>Union of the commanders' color identities — the bound every other card must fit within.</summary>
    public Color ColorIdentity
    {
        get
        {
            var identity = Color.None;
            foreach (var c in Commanders) identity |= c.ColorIdentity;
            return identity;
        }
    }

    /// <summary>Total card count including commanders — should be exactly 100 in a legal deck.</summary>
    public int TotalCards => Commanders.Count + Cards.Sum(s => s.Quantity);

    /// <summary>Groups the non-commander cards by primary role for the visual layout (each card once).</summary>
    public IReadOnlyDictionary<CardRole, List<DeckSlot>> GroupByRole()
        => Cards.GroupBy(s => s.PrimaryRole).ToDictionary(g => g.Key, g => g.ToList());

    /// <summary>
    /// Total coverage per role, counting secondary (overlapping) contributions. Unlike the physical
    /// card count, these can — and usually do — sum to more than 99: that overlap is exactly what
    /// the templates assume. Use this, not primary-role counts, to judge "do we have enough ramp?".
    /// </summary>
    public IReadOnlyDictionary<CardRole, double> CoverageByRole()
    {
        var coverage = new Dictionary<CardRole, double>();
        foreach (var slot in Cards)
            foreach (var role in slot.Roles.AllRoles())
                coverage[role] = coverage.GetValueOrDefault(role) + slot.Roles.CoverageFor(role) * slot.Quantity;
        return coverage;
    }
}
