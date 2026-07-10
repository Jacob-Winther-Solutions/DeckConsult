namespace EdhDeckBuilder.Agent.Authentication.OpenAI;

public static class OpenAiModels
{
    public const string Gpt4oMini = "gpt-4o-mini";
    public const string Gpt4o     = "gpt-4o";
    public const string O4Mini    = "o4-mini";
    public const string O3        = "o3";

    public static readonly IReadOnlyList<(string Id, string Label)> SelectionModels =
    [
        (Gpt4oMini, "GPT-4o mini — fast, cheap (default)"),
        (Gpt4o,     "GPT-4o — balanced"),
        (O4Mini,    "o4-mini — reasoning"),
        (O3,        "o3 — highest quality"),
    ];
}
