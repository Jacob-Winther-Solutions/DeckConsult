using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Partnerships;
using EdhDeckBuilder.Infrastructure.Scryfall;

namespace EdhDeckBuilder.Tests.Core;

public sealed class PartnershipEligibilityRuleTests
{
    private static readonly PartnershipEligibilityRule Rule = new();

    private static Card CreateCard(string name, Color colorIdentity = Color.None)
        => new()
        {
            ScryfallId = Guid.NewGuid(),
            OracleId = Guid.NewGuid(),
            Name = name,
            TypeLine = "Legendary Creature",
            ColorIdentity = colorIdentity,
        };

    [Fact]
    public void CanPartner_IdenticalPartnerKeyword_ReturnsTrue()
    {
        var first = CreateCard("Card A");
        var second = CreateCard("Card B");

        var result = Rule.CanPartner(first, second, "Partner", "Partner");

        Assert.True(result);
    }

    [Fact]
    public void CanPartner_OneHasPartnerOtherDoesNot_ReturnsFalse()
    {
        var first = CreateCard("Card A");
        var second = CreateCard("Card B");

        var result = Rule.CanPartner(first, second, "Partner", "");

        Assert.False(result);
    }

    [Fact]
    public void CanPartner_PartnerWithSpecificName_ValidatesPairing()
    {
        var thrasios = CreateCard("Thrasios, Triton Hero", Color.Green | Color.Blue);
        thrasios = thrasios with { OracleText = "Partner with Tymna the Weaver\n{4}: Scry 1..." };

        var tymna = CreateCard("Tymna the Weaver", Color.White | Color.Black);
        tymna = tymna with { OracleText = "Partner with Thrasios, Triton Hero\n{T}: Exile the top card..." };

        // Both have "Partner with" keyword (as it comes from Scryfall), names are in oracle text
        var result = Rule.CanPartner(thrasios, tymna, "Partner with", "Partner with");

        Assert.True(result);
    }

    [Fact]
    public void CanPartner_GenericPartnerOnly_OneDoesNot_ReturnsFalse()
    {
        var cardA = CreateCard("Card A");
        var cardB = CreateCard("Card B");

        // Only first has "Partner", second doesn't
        var result = Rule.CanPartner(cardA, cardB, "Partner", "");

        Assert.False(result);
    }

    [Fact]
    public void CanPartner_BackgroundWithLegendaryCreature_ReturnsFalse()
    {
        var background = CreateCard("Some Background", Color.White);
        var creature = CreateCard("Legendary Creature");

        // Background should not partner with a legendary creature (only with creatures that support Background)
        var result = Rule.CanPartner(background, creature, "Background", "");

        Assert.False(result);
    }

    [Fact]
    public void CanPartner_TwoBackgrounds_ReturnsFalse()
    {
        var background1 = CreateCard("Background A");
        var background2 = CreateCard("Background B");

        var result = Rule.CanPartner(background1, background2, "Background", "Background");

        Assert.False(result);
    }

    [Fact]
    public void CanPartner_FriendsForever_BothHaveKeyword_ReturnsTrue()
    {
        var first = CreateCard("Card A");
        var second = CreateCard("Card B");

        var result = Rule.CanPartner(first, second, "Friends Forever", "Friends Forever");

        Assert.True(result);
    }

    [Fact]
    public void CanPartner_DoctorsCompanion_WithAnotherDoctorsCompanion_ReturnsFalse()
    {
        var firstCompanion = CreateCard("Card A");
        var secondCompanion = CreateCard("Card B");

        // Doctor's companions can ONLY pair with Time Lord Doctor creatures, not with each other
        var result = Rule.CanPartner(firstCompanion, secondCompanion, "doctor's companion", "doctor's companion");

        Assert.False(result);
    }

    [Theory]
    [InlineData("Partner")]
    [InlineData("PARTNER")]
    [InlineData("partner")]
    [InlineData("PaRtNeR")]
    public void CanPartner_CaseInsensitiveMatching(string keyword)
    {
        var first = CreateCard("Card A");
        var second = CreateCard("Card B");

        var result = Rule.CanPartner(first, second, keyword, keyword);

        Assert.True(result);
    }

    [Fact]
    public void CanPartner_PartnerAndBackground_ReturnsFalse()
    {
        var card1 = CreateCard("Card A");
        var card2 = CreateCard("Card B");

        var result1 = Rule.CanPartner(card1, card2, "Partner", "Background");
        var result2 = Rule.CanPartner(card1, card2, "Background", "Partner");

        Assert.False(result1);
        Assert.False(result2);
    }

    [Fact]
    public void CanPartner_NullKeyword_ReturnsFalse()
    {
        var first = CreateCard("Card A");
        var second = CreateCard("Card B");

        var result = Rule.CanPartner(first, second, "Partner", null);

        Assert.False(result);
    }

    [Fact]
    public void CanPartner_EmptyKeyword_ReturnsFalse()
    {
        var first = CreateCard("Card A");
        var second = CreateCard("Card B");

        var result = Rule.CanPartner(first, second, "Partner", "");

        Assert.False(result);
    }

    [Fact]
    public void SupportedKeywords_ContainsExpectedKeywords()
    {
        var supported = Rule.SupportedKeywords;

        Assert.Contains("Partner", supported);
        Assert.Contains("Partner with", supported);
        Assert.Contains("Background", supported);
        Assert.Contains("Friends Forever", supported);
        Assert.Contains("Doctor's Companion", supported);
    }

    [Fact]
    public void CanPartner_PartnerWithRograkh_CaseInsensitive()
    {
        var rograkh = CreateCard("Rograkh, Son of Rohgadh", Color.Red);
        rograkh = rograkh with { OracleText = "Partner with Drana, Liberator of Zendikar\nHaste\n..." };

        var drana = CreateCard("Drana, Liberator of Zendikar", Color.Black);
        drana = drana with { OracleText = "Partner with Rograkh, Son of Rohgadh\nMenace\n..." };

        // Test with lowercase keyword (as Scryfall provides)
        var result1 = Rule.CanPartner(rograkh, drana, "partner with", "partner with");

        // Test with uppercase keyword (case insensitivity)
        var result2 = Rule.CanPartner(rograkh, drana, "PARTNER WITH", "PARTNER WITH");

        Assert.True(result1);
        Assert.True(result2);
    }
}
