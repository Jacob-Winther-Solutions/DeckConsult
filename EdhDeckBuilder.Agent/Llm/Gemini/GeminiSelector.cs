using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Selection;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Llm.Gemini;

/// <summary>
/// Deferred: OpenAI SDK v2.2 API surface requires additional investigation.
/// LLM adapter not yet implemented for Gemini selection.
/// Infrastructure layer complete (factory, DI, UI); implementation blocked on SDK API details.
/// </summary>
public sealed class GeminiSelector(IGeminiClientFactory factory) : ICardSelector
{
    private UsageTracker? _usageTracker;

    public void SetUsageTracker(UsageTracker tracker) => _usageTracker = tracker;

    public async Task<IReadOnlyList<SelectionResult>> SelectAsync(
        CardRole role,
        IReadOnlyList<FillCandidate> candidates,
        BuildContext context,
        BuildState state,
        CancellationToken ct = default)
    {
        await Task.Delay(0, ct);
        throw new NotImplementedException("Gemini selector implementation deferred pending OpenAI SDK v2.2 API details");
    }
}
