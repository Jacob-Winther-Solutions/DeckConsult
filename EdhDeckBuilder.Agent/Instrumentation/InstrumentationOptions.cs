namespace EdhDeckBuilder.Agent.Instrumentation;

/// <summary>
/// Configuration options for instrumentation (logging, tracing, debugging).
/// Configured via appsettings["Instrumentation"].
/// </summary>
public sealed class InstrumentationOptions
{
    public const string Section = "Instrumentation";

    /// <summary>
    /// Enable detailed logging of LLM classification responses to disk.
    /// Each build session creates a JSON file in %LOCALAPPDATA%\EdhDeckBuilder\logs\
    /// containing per-call metrics and response details. Useful for debugging token usage.
    /// </summary>
    public bool LogClassificationResponses { get; set; } = false;

    /// <summary>
    /// Enable structured logging of deck builder pipeline stages (JSON format).
    /// Logs card counts, role breakdowns, and timing for each stage.
    /// Enabled in Production for observability; controlled per environment in appsettings.
    /// </summary>
    public bool EnableStructuredDeckBuildLogging { get; set; } = false;

    /// <summary>
    /// Include reasoning explanations in classification results (debug mode only).
    /// When enabled, the classifier returns a brief explanation for each card's role assignment,
    /// especially useful for understanding why cards are marked as Unmatched.
    /// Adds token cost; only enabled in Development.
    /// </summary>
    public bool EnableClassificationReasoning { get; set; } = false;

    /// <summary>
    /// Log the raw outgoing JSON request body and raw incoming JSON response body for every
    /// LLM call. Off by default; enable in Development to diagnose serialization issues,
    /// prompt-caching behaviour, or unexpected model responses.
    /// </summary>
    public bool LogRawLlmRequests { get; set; } = false;

    // ── Static accessor ──────────────────────────────────────────────────────────────────
    // Set once at startup by ServiceCollectionExtensions so LLM adapters can read the flags
    // without requiring constructor injection.

    private static InstrumentationOptions _current = new();

    public static InstrumentationOptions Current => _current;

    internal static void SetCurrent(InstrumentationOptions options) => _current = options;
}
