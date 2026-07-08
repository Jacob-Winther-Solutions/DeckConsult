namespace EdhDeckBuilder.Agent.Instrumentation;

/// <summary>
/// Marker interface for LLM adapters that report token usage. Implementations expose a
/// <see cref="SetUsageTracker"/> hook so the pipeline can hand out one <see cref="UsageTracker"/>
/// per build and gather per-call totals from every provider without needing to type-switch on
/// concrete adapters (Anthropic, Gemini, future providers).
/// </summary>
public interface IUsageTrackerAware
{
    void SetUsageTracker(UsageTracker tracker);
}
