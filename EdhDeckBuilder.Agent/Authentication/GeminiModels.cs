namespace EdhDeckBuilder.Agent.Authentication;

public static class GeminiModels
{
    public const string Flash   = "gemini-2.0-flash";
    public const string Flash25 = "gemini-2.5-flash";

    public static readonly IReadOnlyList<(string Id, string Label)> SelectionModels =
    [
        (Flash,   "Gemini 2.0 Flash — fast, free (default)"),
        (Flash25, "Gemini 2.5 Flash — better reasoning, free"),
    ];
}
