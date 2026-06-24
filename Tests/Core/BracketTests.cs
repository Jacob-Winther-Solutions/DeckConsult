using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Core.Rules;

namespace EdhDeckBuilder.Tests.Core;

public sealed class BracketTests
{
    // --- BracketLibrary sanity -------------------------------------------

    [Fact]
    public void BracketLibrary_has_a_profile_for_every_bracket()
    {
        var brackets = Enum.GetValues<Bracket>();
        Assert.All(brackets, b => Assert.True(BracketLibrary.All.ContainsKey(b)));
    }

    [Fact]
    public void Higher_brackets_have_more_or_equal_tutors_than_lower()
    {
        int tutorsAt(Bracket b)
            => BracketLibrary.Get(b).Adjustments.GetValueOrDefault(CardRole.Tutor, 0);

        Assert.True(tutorsAt(Bracket.Five) >= tutorsAt(Bracket.Four));
        Assert.True(tutorsAt(Bracket.Four) >= tutorsAt(Bracket.Three));
        Assert.True(tutorsAt(Bracket.Three) >= tutorsAt(Bracket.Two));
        Assert.True(tutorsAt(Bracket.Two) >= tutorsAt(Bracket.One));
    }

    [Fact]
    public void Higher_brackets_have_lower_or_equal_land_adjustment_than_lower()
    {
        int landAdj(Bracket b)
            => BracketLibrary.Get(b).Adjustments.GetValueOrDefault(CardRole.Land, 0);

        Assert.True(landAdj(Bracket.Five) <= landAdj(Bracket.Four));
        Assert.True(landAdj(Bracket.Four) <= landAdj(Bracket.Three));
    }

    // --- BracketRule --------------------------------------------------------

    [Theory]
    [InlineData(Bracket.One)]
    [InlineData(Bracket.Two)]
    public void BracketRule_warns_for_game_changer_below_bracket_three(Bracket maxBracket)
    {
        var deck = BuildDeckWith("Mana Crypt");
        var violations = new BracketRule(maxBracket).Check(deck).ToList();
        Assert.Single(violations);
        Assert.Equal(Severity.Warning, violations[0].Severity);
        Assert.Contains("Mana Crypt", violations[0].Message);
    }

    [Theory]
    [InlineData(Bracket.Three)]
    [InlineData(Bracket.Four)]
    [InlineData(Bracket.Five)]
    public void BracketRule_is_silent_at_bracket_three_and_above(Bracket maxBracket)
    {
        var deck = BuildDeckWith("Mana Crypt");
        var violations = new BracketRule(maxBracket).Check(deck);
        Assert.Empty(violations);
    }

    [Fact]
    public void BracketRule_is_silent_for_non_game_changer_cards()
    {
        var deck = BuildDeckWith("Sol Ring");
        var violations = new BracketRule(Bracket.One).Check(deck);
        Assert.Empty(violations);
    }

    [Fact]
    public void BracketRule_checks_commander_slot_as_well()
    {
        var commander = MakeCard("Mana Crypt", canBeCommander: true);
        var deck      = BuildDeck(commander,
            Enumerable.Range(1, 99).Select(i => MakeCard($"Card {i}")));
        var violations = new BracketRule(Bracket.One).Check(deck).ToList();
        Assert.Single(violations);
    }

    [Fact]
    public void Multiple_game_changers_each_produce_a_violation()
    {
        var deck = BuildDeckWith("Mana Crypt", "Demonic Tutor", "Cyclonic Rift");
        var violations = new BracketRule(Bracket.One).Check(deck).ToList();
        Assert.Equal(3, violations.Count);
    }

    // --- DeckValidator.WithBracket ------------------------------------------

    [Fact]
    public void WithBracket_validator_includes_standard_hard_rules_and_bracket_check()
    {
        // Deck with a game changer AND wrong size: should get both an Error and a Warning
        var offender = MakeCard("Mana Crypt");
        var cards = Enumerable.Range(1, 97)  // 1 commander + 97 cards = 98 total (size error)
            .Select(i => MakeCard($"Card {i}"))
            .Append(offender);

        var commander = MakeCard("Commander", canBeCommander: true);
        var deck      = BuildDeck(commander, cards);

        var result = DeckValidator.WithBracket(Bracket.One).Validate(deck);

        Assert.False(result.IsLegal); // size error makes it illegal
        Assert.Contains(result.Violations, v => v.Severity == Severity.Error);    // DeckSizeRule
        Assert.Contains(result.Violations, v => v.Severity == Severity.Warning);  // BracketRule
    }

    // --- TemplateResolver with bracket -------------------------------------

    [Fact]
    public void cEDH_bracket_significantly_reduces_land_count()
    {
        var baseline = TemplateResolver.Resolve(DeckTemplate.Balanced, []);
        var cedh     = TemplateResolver.Resolve(DeckTemplate.Balanced, [],
            bracket: BracketLibrary.Get(Bracket.Five));

        Assert.True(cedh.Targets[CardRole.Land].Ideal < baseline.Targets[CardRole.Land].Ideal);
    }

    [Fact]
    public void cEDH_bracket_increases_tutor_count_above_baseline()
    {
        var baseline = TemplateResolver.Resolve(DeckTemplate.Balanced, []);
        var cedh     = TemplateResolver.Resolve(DeckTemplate.Balanced, [],
            bracket: BracketLibrary.Get(Bracket.Five));

        int baselineTutors = baseline.Targets.GetValueOrDefault(CardRole.Tutor).Ideal;
        int cedhTutors     = cedh.Targets.GetValueOrDefault(CardRole.Tutor).Ideal;
        Assert.True(cedhTutors > baselineTutors);
    }

    [Fact]
    public void Bracket_with_archetype_and_theme_still_sums_to_deck_size()
    {
        var result = TemplateResolver.Resolve(DeckTemplate.Balanced,
            archetypes: [new WeightedArchetype(ArchetypeLibrary.Get(Archetype.Combo))],
            themes:     [new WeightedTheme(ThemeLibrary.Get(Theme.BigMana))],
            bracket:    BracketLibrary.Get(Bracket.Four));

        Assert.Equal(99, result.Targets.Values.Sum(t => t.Ideal));
    }

    [Fact]
    public void Bracket_name_appears_in_resolved_template_name()
    {
        var result = TemplateResolver.Resolve(DeckTemplate.Balanced, [],
            bracket: BracketLibrary.Get(Bracket.Four));

        Assert.Contains("Bracket 4", result.Name);
    }

    // --- helpers ------------------------------------------------------------

    private static Card MakeCard(string name, bool canBeCommander = false, Legality legality = Legality.Legal) => new()
    {
        ScryfallId        = Guid.NewGuid(),
        OracleId          = Guid.NewGuid(),
        Name              = name,
        TypeLine          = canBeCommander ? "Legendary Creature" : "Artifact",
        CanBeCommander    = canBeCommander,
        CommanderLegality = legality,
    };

    private static Deck BuildDeck(Card commander, IEnumerable<Card> cards) => new()
    {
        Name       = "Test Deck",
        Commanders = [commander],
        Cards      = cards.Select(c => new DeckSlot { Card = c }).ToList(),
    };

    private static Deck BuildDeckWith(params string[] gameChangerNames)
    {
        var commander  = MakeCard("Commander", canBeCommander: true);
        var specials   = gameChangerNames.Select(n => MakeCard(n)).ToList();
        var filler     = Enumerable.Range(1, 99 - specials.Count)
                             .Select(i => MakeCard($"Card {i}"));
        return BuildDeck(commander, specials.Concat(filler));
    }
}
