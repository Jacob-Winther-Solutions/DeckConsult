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
}
