using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Llm.Gemini;

/// <summary>
/// Deferred: OpenAI SDK v2.2 API surface requires additional investigation.
/// LLM adapter not yet implemented for Gemini classification.
/// Infrastructure layer complete (factory, DI, UI); implementation blocked on SDK API details.
/// </summary>
public sealed class GeminiClassifier(
    IGeminiClientFactory factory,
    ClassificationCache cache) : ILlmClassifier
{
    private UsageTracker? _usageTracker;

    public void SetUsageTracker(UsageTracker tracker) => _usageTracker = tracker;

    public async Task<IReadOnlyList<ClassificationResult>> ClassifyAsync(
        IReadOnlyList<CardCandidate> candidates,
        IReadOnlyList<Card> commanders,
        CancellationToken ct = default)
    {
        await Task.Delay(0, ct);
        throw new NotImplementedException("Gemini classifier implementation deferred pending OpenAI SDK v2.2 API details");
    }
}
