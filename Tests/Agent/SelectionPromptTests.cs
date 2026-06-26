using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Core.Rules;

namespace EdhDeckBuilder.Tests.Agent;

public sealed class SelectionPromptTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static BuildContext MakeContext(Bracket bracket)
    {
        var commander = new Card
        {
            ScryfallId        = Guid.NewGuid(),
            OracleId          = Guid.NewGuid(),
            Name              = "Test Commander",
            TypeLine          = "Legendary Creature",
            Types             = CardType.Creature,
            ColorIdentity     = Color.Green,
            CommanderLegality = Legality.Legal,
        };
        var template  = DeckTemplate.Balanced;
        var resolved  = TemplateResolver.Resolve(
            template, [new WeightedArchetype(ArchetypeLibrary.All[Archetype.Midrange], 1.0)]);

        return new BuildContext
        {
            Commanders        = [commander],
            ColorIdentity     = Color.Green,
            ResolvedTemplate  = resolved,
            NetTargets        = resolved.Targets,
            CommanderProfiles = [RoleProfile.Of(CardRole.Plan)],
            Constraints       = new SoftConstraints { Bracket = bracket },
            ReservedLandCount = 38,
        };
    }

    private static BuildState MakeState() => new(38);

    private static string GetMessage(Bracket bracket) =>
        SelectionPrompt.FormatUserMessage(CardRole.Ramp, [], MakeContext(bracket), MakeState());

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Bracket1_includes_bracket_number_name_and_description()
    {
        var msg = GetMessage(Bracket.One);
        Assert.Contains("Bracket: 1", msg);
        Assert.Contains("Casual", msg);
    }

    [Fact]
    public void Bracket1_lists_game_changers_and_instructs_to_rank_them_last()
    {
        var msg = GetMessage(Bracket.One);
        Assert.Contains("Game Changer", msg);
        // Spot-check a few well-known Game Changers are listed
        Assert.Contains("Mana Crypt", msg);
        Assert.Contains("Demonic Tutor", msg);
        Assert.Contains("Cyclonic Rift", msg);
    }

    [Fact]
    public void Bracket2_also_lists_game_changers()
    {
        var msg = GetMessage(Bracket.Two);
        Assert.Contains("Game Changer", msg);
        Assert.Contains("Mana Crypt", msg);
    }

    [Fact]
    public void Bracket3_includes_description_but_no_game_changer_avoidance()
    {
        var msg = GetMessage(Bracket.Three);
        Assert.Contains("Bracket: 3", msg);
        Assert.Contains("Optimised", msg);
        // Game Changers are expected at Bracket 3 — no avoidance text, no list
        Assert.DoesNotContain("rank them last", msg);
        Assert.DoesNotContain("Game Changers:", msg);
    }

    [Fact]
    public void Bracket4_encourages_game_changers()
    {
        var msg = GetMessage(Bracket.Four);
        Assert.Contains("Bracket: 4", msg);
        Assert.Contains("Game Changer", msg);
        Assert.Contains("rank them highly", msg);
    }

    [Fact]
    public void Bracket5_encourages_game_changers()
    {
        var msg = GetMessage(Bracket.Five);
        Assert.Contains("Bracket: 5", msg);
        Assert.Contains("rank them highly", msg);
    }

    [Fact]
    public void All_game_changers_in_list_are_present_in_bracket1_message()
    {
        var msg = GetMessage(Bracket.One);
        foreach (var card in GameChangersList.Cards)
            Assert.Contains(card, msg);
    }
}
