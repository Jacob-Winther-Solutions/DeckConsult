using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Tests.Agent;

public sealed class CommanderSelectionPromptTests
{
    [Fact]
    public void FormatUserMessage_IncludesArchetypes()
    {
        // Arrange
        var commanders = new[] { CreateCommander("Test") };
        var request = new CommanderDiscoveryRequest
        {
            Archetypes = [Archetype.Aggro, Archetype.Combo],
            Themes = [],
            ColorFilter = null,
        };

        // Act
        var message = CommanderSelectionPrompt.FormatUserMessage(commanders, request);

        // Assert
        Assert.Contains("Aggro", message);
        Assert.Contains("Combo", message);
    }

    [Fact]
    public void FormatUserMessage_IncludesThemes()
    {
        // Arrange
        var commanders = new[] { CreateCommander("Test") };
        var request = new CommanderDiscoveryRequest
        {
            Archetypes = [],
            Themes = [Theme.BigMana, Theme.Tokens],
            ColorFilter = null,
        };

        // Act
        var message = CommanderSelectionPrompt.FormatUserMessage(commanders, request);

        // Assert
        Assert.Contains("BigMana", message);
        Assert.Contains("Tokens", message);
    }

    [Fact]
    public void FormatUserMessage_IncludesBracket()
    {
        // Arrange
        var commanders = new[] { CreateCommander("Test") };
        var request = new CommanderDiscoveryRequest
        {
            Archetypes = [],
            Themes = [],
            ColorFilter = null,
            Bracket = Bracket.Four,
        };

        // Act
        var message = CommanderSelectionPrompt.FormatUserMessage(commanders, request);

        // Assert
        Assert.Contains("Bracket", message);
        Assert.Contains("4", message);
    }

    [Fact]
    public void FormatUserMessage_IncludesBudget()
    {
        // Arrange
        var commanders = new[] { CreateCommander("Test") };
        var request = new CommanderDiscoveryRequest
        {
            Archetypes = [],
            Themes = [],
            ColorFilter = null,
            MaxCardPriceUsd = 25.50m,
        };

        // Act
        var message = CommanderSelectionPrompt.FormatUserMessage(commanders, request);

        // Assert
        Assert.Contains("Budget", message);
    }

    [Fact]
    public void FormatUserMessage_TruncatesOracleText()
    {
        // Arrange
        var longText = new string('x', 300);
        var commander = new Card
        {
            OracleId = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid(),
            Name = "Test",
            TypeLine = "Creature",
            ColorIdentity = Color.Blue,
            OracleText = longText,
            CanBeCommander = true,
        };

        var request = new CommanderDiscoveryRequest
        {
            Archetypes = [],
            Themes = [],
            ColorFilter = null,
        };

        // Act
        var message = CommanderSelectionPrompt.FormatUserMessage(new[] { commander }, request);

        // Assert — should truncate long oracle text
        Assert.Contains("…", message);
    }

    [Fact]
    public void FormatUserMessage_NullDescription_ShowsNone()
    {
        // Arrange
        var commanders = new[] { CreateCommander("Test") };
        var request = new CommanderDiscoveryRequest
        {
            Archetypes = [],
            Themes = [],
            ColorFilter = null,
            Description = null,
        };

        // Act
        var message = CommanderSelectionPrompt.FormatUserMessage(commanders, request);

        // Assert
        Assert.Contains("None", message);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private Card CreateCommander(string name) => new()
    {
        OracleId = Guid.NewGuid(),
        ScryfallId = Guid.NewGuid(),
        Name = name,
        TypeLine = "Legendary Creature",
        ColorIdentity = Color.Green,
        CanBeCommander = true,
    };
}
