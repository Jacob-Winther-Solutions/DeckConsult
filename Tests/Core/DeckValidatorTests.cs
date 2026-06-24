using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Core.Rules;

namespace EdhDeckBuilder.Tests.Core;

public sealed class DeckValidatorTests
{
    // --- helpers ------------------------------------------------------------

    private static Card MakeCard(
        string name,
        Color colorIdentity  = Color.White,
        bool isBasicLand     = false,
        bool canBeCommander  = false,
        Legality legality    = Legality.Legal,
        Guid? oracleId       = null) => new()
    {
        ScryfallId         = Guid.NewGuid(),
        OracleId           = oracleId ?? Guid.NewGuid(),
        Name               = name,
        TypeLine           = isBasicLand ? "Basic Land — Plains" : "Sorcery",
        ColorIdentity      = colorIdentity,
        IsBasicLand        = isBasicLand,
        CanBeCommander     = canBeCommander,
        CommanderLegality  = legality,
    };

    private static Deck Build100CardDeck(
        Card? commander = null,
        IEnumerable<DeckSlot>? overrideCards = null)
    {
        commander ??= MakeCard("Commander", canBeCommander: true);
        var cards = overrideCards?.ToList()
            ?? Enumerable.Range(1, 99)
               .Select(i => new DeckSlot { Card = MakeCard($"Card {i}") })
               .ToList();
        return new Deck { Name = "Test Deck", Commanders = [commander], Cards = cards };
    }

    // --- DeckSizeRule -------------------------------------------------------

    [Fact]
    public void Legal_100_card_deck_passes_size_rule()
    {
        var deck = Build100CardDeck();
        var violations = new DeckSizeRule().Check(deck);
        Assert.Empty(violations);
    }

    [Theory]
    [InlineData(98)]  // 1 commander + 98 cards = 99 total
    [InlineData(100)] // 1 commander + 100 cards = 101 total
    public void Wrong_card_count_triggers_size_error(int nonCommanderCount)
    {
        var cards = Enumerable.Range(1, nonCommanderCount)
            .Select(i => new DeckSlot { Card = MakeCard($"Card {i}") });
        var deck = Build100CardDeck(overrideCards: cards);

        var violations = new DeckSizeRule().Check(deck).ToList();
        Assert.Single(violations);
        Assert.Equal(Severity.Error, violations[0].Severity);
    }

    // --- SingletonRule ------------------------------------------------------

    [Fact]
    public void Duplicate_non_basic_triggers_singleton_error()
    {
        var oracleId   = Guid.NewGuid();
        var copy1      = MakeCard("Sol Ring", oracleId: oracleId);
        var copy2      = MakeCard("Sol Ring", oracleId: oracleId);
        var cards = new[] { new DeckSlot { Card = copy1 }, new DeckSlot { Card = copy2 } }
            .Concat(Enumerable.Range(1, 97).Select(i => new DeckSlot { Card = MakeCard($"Card {i}") }));

        var deck       = Build100CardDeck(overrideCards: cards);
        var violations = new SingletonRule().Check(deck).ToList();
        Assert.NotEmpty(violations);
        Assert.All(violations, v => Assert.Equal(Severity.Error, v.Severity));
    }

    [Fact]
    public void Multiple_basic_lands_are_allowed()
    {
        var plains    = MakeCard("Plains", isBasicLand: true);
        var basicSlot = new DeckSlot { Card = plains, Quantity = 5 };
        var rest      = Enumerable.Range(1, 94)
            .Select(i => new DeckSlot { Card = MakeCard($"Card {i}") });

        var deck       = Build100CardDeck(overrideCards: rest.Prepend(basicSlot));
        var violations = new SingletonRule().Check(deck);
        Assert.Empty(violations);
    }

    // --- ColorIdentityRule --------------------------------------------------

    [Fact]
    public void Card_within_commander_identity_passes()
    {
        var commander  = MakeCard("Atraxa", Color.White | Color.Blue | Color.Black | Color.Green, canBeCommander: true);
        var card       = MakeCard("Card 1", Color.White);
        var deck       = Build100CardDeck(commander,
            Enumerable.Range(2, 98).Select(i => new DeckSlot { Card = MakeCard($"Card {i}", Color.White) })
                       .Prepend(new DeckSlot { Card = card }));

        var violations = new ColorIdentityRule().Check(deck);
        Assert.Empty(violations);
    }

    [Fact]
    public void Card_outside_commander_identity_triggers_error()
    {
        var commander  = MakeCard("Mono White Commander", Color.White, canBeCommander: true);
        var redCard    = MakeCard("Red Card", Color.Red);
        var cards      = Enumerable.Range(1, 98)
            .Select(i => new DeckSlot { Card = MakeCard($"Card {i}", Color.White) })
            .Append(new DeckSlot { Card = redCard });

        var deck       = Build100CardDeck(commander, cards);
        var violations = new ColorIdentityRule().Check(deck).ToList();
        Assert.Single(violations);
        Assert.Equal(Severity.Error, violations[0].Severity);
    }

    // --- CommanderRule ------------------------------------------------------

    [Fact]
    public void Legal_commander_passes_commander_rule()
    {
        var deck       = Build100CardDeck();
        var violations = new CommanderRule().Check(deck);
        Assert.Empty(violations);
    }

    [Fact]
    public void Card_with_CanBeCommander_false_triggers_error()
    {
        var notCommander = MakeCard("Vanilla Creature", canBeCommander: false);
        var deck         = Build100CardDeck(commander: notCommander);
        var violations   = new CommanderRule().Check(deck).ToList();
        Assert.Single(violations);
        Assert.Equal(Severity.Error, violations[0].Severity);
    }

    // --- BanlistRule --------------------------------------------------------

    [Fact]
    public void Banned_card_in_99_triggers_error()
    {
        var banned     = MakeCard("Banned Card", legality: Legality.Banned);
        var cards      = Enumerable.Range(1, 98)
            .Select(i => new DeckSlot { Card = MakeCard($"Card {i}") })
            .Append(new DeckSlot { Card = banned });

        var deck       = Build100CardDeck(overrideCards: cards);
        var violations = new BanlistRule().Check(deck).ToList();
        Assert.Single(violations);
        Assert.Equal(Severity.Error, violations[0].Severity);
    }

    [Fact]
    public void Banned_commander_triggers_error()
    {
        var banned = MakeCard("Banned Commander", legality: Legality.Banned);
        var deck   = Build100CardDeck(commander: banned);

        var violations = new BanlistRule().Check(deck).ToList();
        Assert.Single(violations);
    }

    // --- DeckValidator.Standard ---------------------------------------------

    [Fact]
    public void Standard_validator_approves_fully_legal_deck()
    {
        var deck   = Build100CardDeck();
        var result = DeckValidator.Standard.Validate(deck);
        Assert.True(result.IsLegal);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Standard_validator_collects_errors_from_multiple_rules()
    {
        // 99 cards (size error) with a banned card (banlist error)
        var banned = MakeCard("Banned Card", legality: Legality.Banned);
        var cards  = Enumerable.Range(1, 97)
            .Select(i => new DeckSlot { Card = MakeCard($"Card {i}") })
            .Append(new DeckSlot { Card = banned }); // only 98 non-commander cards → 99 total

        var deck   = Build100CardDeck(overrideCards: cards);
        var result = DeckValidator.Standard.Validate(deck);

        Assert.False(result.IsLegal);
        Assert.True(result.Violations.Count >= 2);
    }
}
