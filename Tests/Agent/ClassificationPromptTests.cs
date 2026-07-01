using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Tests.Agent;

public sealed class ClassificationPromptTests
{
    private static Card MakeCard(string? backFaceTypeLine = null, string? backFaceText = null) => new()
    {
        ScryfallId    = Guid.NewGuid(),
        OracleId      = Guid.NewGuid(),
        Name          = "Test Card",
        TypeLine      = backFaceTypeLine is null ? "Instant" : $"Instant // {backFaceTypeLine}",
        OracleText    = "Do something.",
        BackFaceTypeLine = backFaceTypeLine,
        BackFaceText  = backFaceText,
        CommanderLegality = Legality.Legal,
    };

    private static Card MakeCommander() => new()
    {
        ScryfallId    = Guid.NewGuid(),
        OracleId      = Guid.NewGuid(),
        Name          = "Test Commander",
        TypeLine      = "Legendary Creature",
        CommanderLegality = Legality.Legal,
    };

    [Fact]
    public void FormatUserMessage_includes_back_face_info_for_MDFC()
    {
        var landText = "({T}: Add {B}.)";
        var candidate = new CardCandidate(
            MakeCard(backFaceTypeLine: "Land — Swamp", backFaceText: landText), 0.5, "Test");
        var commander = MakeCommander();

        var message = ClassificationPrompt.FormatUserMessage([candidate], [commander]);

        Assert.Contains("Back face type: Land — Swamp", message);
        Assert.Contains($"Back face text: {landText}", message);
    }

    [Fact]
    public void FormatUserMessage_includes_back_face_type_without_text_when_oracle_text_is_null()
    {
        var candidate = new CardCandidate(
            MakeCard(backFaceTypeLine: "Land — Island", backFaceText: null), 0.5, "Test");
        var commander = MakeCommander();

        var message = ClassificationPrompt.FormatUserMessage([candidate], [commander]);

        Assert.Contains("Back face type: Land — Island", message);
        Assert.DoesNotContain("Back face text:", message);
    }

    [Fact]
    public void FormatUserMessage_omits_back_face_section_for_non_MDFC()
    {
        var candidate = new CardCandidate(MakeCard(), 0.5, "Test");
        var commander = MakeCommander();

        var message = ClassificationPrompt.FormatUserMessage([candidate], [commander]);

        Assert.DoesNotContain("Back face type:", message);
        Assert.DoesNotContain("Back face text:", message);
    }
}
