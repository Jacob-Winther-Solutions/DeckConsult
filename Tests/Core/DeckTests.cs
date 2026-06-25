using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Tests.Core;

public sealed class DeckTests
{
    // --- CoverageByRole -----------------------------------------------------

    [Fact]
    public void CoverageByRole_counts_primary_role_at_1_per_card()
    {
        var deck = BuildDeck(
            Slot(CardRole.Ramp),
            Slot(CardRole.Ramp),
            Slot(CardRole.CardAdvantage));

        var coverage = deck.CoverageByRole();
        Assert.Equal(2.0, coverage[CardRole.Ramp]);
        Assert.Equal(1.0, coverage[CardRole.CardAdvantage]);
    }

    [Fact]
    public void CoverageByRole_always_secondary_contributes_full_credit_to_both_roles()
    {
        // Black Market Connections: Ramp AND CardAdvantage at the same time
        var deck = BuildDeck(Slot(CardRole.Ramp, RoleContribution.Both(CardRole.CardAdvantage)));

        var coverage = deck.CoverageByRole();
        Assert.Equal(1.0, coverage[CardRole.Ramp]);
        Assert.Equal(1.0, coverage[CardRole.CardAdvantage]);
    }

    [Fact]
    public void CoverageByRole_modal_secondary_contributes_half_credit()
    {
        // Jeska's Will: Ramp or CardAdvantage — not both at once
        var deck = BuildDeck(Slot(CardRole.Ramp, RoleContribution.EitherOr(CardRole.CardAdvantage)));

        var coverage = deck.CoverageByRole();
        Assert.Equal(1.0, coverage[CardRole.Ramp]);
        Assert.Equal(0.5, coverage[CardRole.CardAdvantage]);
    }

    [Fact]
    public void CoverageByRole_transform_secondary_contributes_partial_credit()
    {
        // Hedron Archive: Ramp now, draw later — sequential, not simultaneous
        var deck = BuildDeck(Slot(CardRole.Ramp, RoleContribution.Switches(CardRole.CardAdvantage)));

        var coverage = deck.CoverageByRole();
        Assert.Equal(0.75, coverage[CardRole.CardAdvantage]);
    }

    [Fact]
    public void CoverageByRole_sum_exceeds_physical_slot_count_when_all_slots_overlap()
    {
        // Five cards each covering two roles → coverage sum = 10, physical slots = 5
        var slots = Enumerable.Range(0, 5)
            .Select(_ => Slot(CardRole.Ramp, RoleContribution.Both(CardRole.CardAdvantage)))
            .ToArray();
        var deck = BuildDeck(slots);

        var totalCoverage = deck.CoverageByRole().Values.Sum();
        var physicalSlots = deck.Cards.Sum(s => s.Quantity);
        Assert.True(totalCoverage > physicalSlots);
    }

    [Fact]
    public void CoverageByRole_respects_quantity_for_basic_lands()
    {
        var slot = new DeckSlot
        {
            Card     = MakeCard(isBasicLand: true),
            Quantity = 7,
            Roles    = RoleProfile.Of(CardRole.Land),
        };
        var deck = BuildDeck(slot);

        Assert.Equal(7.0, deck.CoverageByRole()[CardRole.Land]);
    }

    [Fact]
    public void CoverageByRole_does_not_include_roles_with_no_cards()
    {
        var deck = BuildDeck(Slot(CardRole.Ramp));
        Assert.False(deck.CoverageByRole().ContainsKey(CardRole.Plan));
    }

    // --- GroupByRole --------------------------------------------------------

    [Fact]
    public void GroupByRole_buckets_cards_by_primary_role()
    {
        var deck = BuildDeck(
            Slot(CardRole.Ramp),
            Slot(CardRole.Ramp),
            Slot(CardRole.CardAdvantage));

        var groups = deck.GroupByRole();
        Assert.Equal(2, groups[CardRole.Ramp].Count);
        Assert.Single(groups[CardRole.CardAdvantage]);
    }

    [Fact]
    public void GroupByRole_each_card_appears_exactly_once_regardless_of_secondary_roles()
    {
        // Cards with secondary roles still live in exactly one primary bucket
        var slots = new[]
        {
            Slot(CardRole.Ramp,         RoleContribution.Both(CardRole.CardAdvantage)),
            Slot(CardRole.Ramp,         RoleContribution.Both(CardRole.Plan)),
            Slot(CardRole.CardAdvantage),
        };
        var deck = BuildDeck(slots);

        int totalGrouped = deck.GroupByRole().Values.Sum(g => g.Count);
        Assert.Equal(deck.Cards.Count, totalGrouped);
    }

    [Fact]
    public void GroupByRole_returns_empty_dictionary_for_deck_with_no_cards()
    {
        var deck = new Deck { Name = "Empty", Commanders = [MakeCard(canBeCommander: true)], Cards = [] };
        Assert.Empty(deck.GroupByRole());
    }

    // --- ColorIdentity ------------------------------------------------------

    [Fact]
    public void ColorIdentity_equals_single_commanders_identity()
    {
        var commander = MakeCard(colorIdentity: Color.Blue | Color.White);
        var deck = new Deck { Name = "Test", Commanders = [commander], Cards = [] };
        Assert.Equal(Color.Blue | Color.White, deck.ColorIdentity);
    }

    [Fact]
    public void ColorIdentity_is_union_of_partner_commanders()
    {
        var a = MakeCard(colorIdentity: Color.Green);
        var b = MakeCard(colorIdentity: Color.Blue);
        var deck = new Deck { Name = "Test", Commanders = [a, b], Cards = [] };
        Assert.Equal(Color.Green | Color.Blue, deck.ColorIdentity);
    }

    [Fact]
    public void ColorIdentity_colorless_commander_yields_none()
    {
        var commander = MakeCard(colorIdentity: Color.None);
        var deck = new Deck { Name = "Test", Commanders = [commander], Cards = [] };
        Assert.Equal(Color.None, deck.ColorIdentity);
    }

    [Fact]
    public void ColorIdentity_overlapping_partner_colors_are_not_doubled()
    {
        // Both commanders share Blue — result should still be just Blue
        var a = MakeCard(colorIdentity: Color.Blue);
        var b = MakeCard(colorIdentity: Color.Blue);
        var deck = new Deck { Name = "Test", Commanders = [a, b], Cards = [] };
        Assert.Equal(Color.Blue, deck.ColorIdentity);
    }

    // --- TotalCards ---------------------------------------------------------

    [Fact]
    public void TotalCards_is_commander_count_plus_card_slots()
    {
        var deck = BuildDeck(Slot(CardRole.Ramp), Slot(CardRole.Ramp));
        // BuildDeck adds 1 commander; 2 card slots → total 3
        Assert.Equal(3, deck.TotalCards);
    }

    [Fact]
    public void TotalCards_counts_partners_as_two_commanders()
    {
        var a = MakeCard(canBeCommander: true);
        var b = MakeCard(canBeCommander: true);
        var deck = new Deck { Name = "Test", Commanders = [a, b], Cards = [Slot(CardRole.Ramp)] };
        Assert.Equal(3, deck.TotalCards);
    }

    [Fact]
    public void TotalCards_sums_quantity_for_basic_lands()
    {
        var slot = new DeckSlot
        {
            Card     = MakeCard(isBasicLand: true),
            Quantity = 10,
            Roles    = RoleProfile.Of(CardRole.Land),
        };
        var deck = new Deck
        {
            Name       = "Test",
            Commanders = [MakeCard(canBeCommander: true)],
            Cards      = [slot],
        };
        Assert.Equal(11, deck.TotalCards);  // 1 commander + 10 basic lands
    }

    // --- helpers ------------------------------------------------------------

    private static Card MakeCard(
        bool isBasicLand    = false,
        Color colorIdentity = Color.None,
        bool canBeCommander = false) => new()
    {
        ScryfallId     = Guid.NewGuid(),
        OracleId       = Guid.NewGuid(),
        Name           = "Test Card",
        TypeLine       = canBeCommander ? "Legendary Creature" : "Artifact",
        IsBasicLand    = isBasicLand,
        ColorIdentity  = colorIdentity,
        CanBeCommander = canBeCommander,
    };

    private static DeckSlot Slot(CardRole primary, params RoleContribution[] secondary) =>
        new()
        {
            Card  = MakeCard(),
            Roles = secondary.Length > 0
                ? RoleProfile.Of(primary).With(secondary)
                : RoleProfile.Of(primary),
        };

    private static Deck BuildDeck(params DeckSlot[] slots) => new()
    {
        Name       = "Test Deck",
        Commanders = [MakeCard(canBeCommander: true)],
        Cards      = slots.ToList(),
    };
}
