using EdhDeckBuilder.Agent.Authentication.Gemini;
using EdhDeckBuilder.Agent.Authentication.OpenAI;

namespace EdhDeckBuilder.Agent.Authentication.Claude;

public static class ClaudeModels
{
    public const string Haiku  = "claude-haiku-4-5-20251001";
    public const string Sonnet = "claude-sonnet-5";
    public const string Opus   = "claude-opus-4-8";

    public static readonly IReadOnlyList<(string Id, string Label)> SelectionModels =
    [
        (Haiku,  "Haiku 4.5 — fast, cheap (default)"),
        (Sonnet, "Sonnet 5 — better reasoning"),
        (Opus,   "Opus 4.8 — highest quality"),
    ];

    public static IReadOnlyList<(string Id, string Label)> GetSelectionModels(AiProvider provider) =>
        provider switch
        {
            AiProvider.Google => GeminiModels.SelectionModels,
            AiProvider.OpenAI => OpenAiModels.SelectionModels,
            _ => SelectionModels,
        };
}
