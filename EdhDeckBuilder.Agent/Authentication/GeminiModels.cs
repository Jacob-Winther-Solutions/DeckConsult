namespace EdhDeckBuilder.Agent.Authentication;

/// <summary>
/// Gemini model IDs offered in the UI. Ordering matters — the first entry is the default
/// the settings page picks up on a fresh circuit. Labels lead with the observed free-tier
/// RPD ceiling for this project (from Google AI Studio → Rate Limits); the "needs billing"
/// entries have <c>limit: 0</c> on the free tier and require a billing account attached.
/// <para>
/// The <c>Flash3</c> / <c>Flash31Lite</c> / <c>Flash35</c> IDs are best-guess API strings
/// derived from the pattern of the confirmed 2.5 IDs — verify against
/// <see href="https://ai.google.dev/gemini-api/docs/models"/> if a call 404s.
/// </para>
/// </summary>
public static class GeminiModels
{
    /// <summary>Gemini 2.5 Flash — default. Confirmed working. 5 RPM / 250K TPM / 20 RPD.</summary>
    public const string Flash25     = "gemini-2.5-flash";
    /// <summary>Gemini 3.1 Flash Lite — 500 RPD, the iteration workhorse.</summary>
    public const string Flash31Lite = "gemini-3.1-flash-lite";
    /// <summary>Gemini 2.5 Flash Lite — 10 RPM but same 20 RPD as 2.5 Flash.</summary>
    public const string Flash25Lite = "gemini-2.5-flash-lite";
    /// <summary>Gemini 3 Flash — 20 RPD, newer series.</summary>
    public const string Flash3      = "gemini-3-flash";
    /// <summary>Gemini 3.5 Flash — 20 RPD, newest series available on free tier.</summary>
    public const string Flash35     = "gemini-3.5-flash";
    /// <summary>Gemini 2.0 Flash — limit=0 on free tier; requires billing attached.</summary>
    public const string Flash       = "gemini-2.0-flash";
    /// <summary>Gemini 2.0 Flash Lite — limit=0 on free tier; requires billing attached.</summary>
    public const string FlashLite   = "gemini-2.0-flash-lite";

    public static readonly IReadOnlyList<(string Id, string Label)> SelectionModels =
    [
        (Flash31Lite, "Gemini 3.1 Flash Lite — 500 RPD, best for iteration (default)"),
        (Flash25,     "Gemini 2.5 Flash — balanced, 20 RPD"),
        (Flash25Lite, "Gemini 2.5 Flash Lite — 20 RPD, 10 RPM"),
        (Flash3,      "Gemini 3 Flash — 20 RPD, newer series"),
        (Flash35,     "Gemini 3.5 Flash — 20 RPD, newest series"),
        (Flash,       "Gemini 2.0 Flash — needs billing enabled on the project"),
        (FlashLite,   "Gemini 2.0 Flash Lite — needs billing enabled on the project"),
    ];
}
