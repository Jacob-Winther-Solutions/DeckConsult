using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Llm.Gemini;

/// <summary>
/// Deferred: OpenAI SDK v2.2 API surface requires additional investigation.
/// LLM adapter not yet implemented for Gemini commander selection.
/// Infrastructure layer complete (factory, DI, UI); implementation blocked on SDK API details.
/// </summary>
public sealed class GeminiCommanderSelector(IGeminiClientFactory factory) : ICommanderSelector
{
    private UsageTracker? _usageTracker;

    public void SetUsageTracker(UsageTracker tracker) => _usageTracker = tracker;

    public async Task<IReadOnlyList<CommanderSelectionResult>> SelectAsync(
        IReadOnlyList<Card> candidates,
        CommanderDiscoveryRequest request,
        CancellationToken ct = default)
    {
        await Task.Delay(0, ct);
        throw new NotImplementedException("Gemini commander selector implementation deferred pending OpenAI SDK v2.2 API details");
    }
}
