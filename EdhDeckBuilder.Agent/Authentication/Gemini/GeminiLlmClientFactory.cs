using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm.Gemini;
using EdhDeckBuilder.Agent.Llm.Shared;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Agent.Authentication.Gemini;

/// <summary>
/// Wraps <see cref="IGeminiClientFactory"/> behind <see cref="ILlmClientFactory"/>, creating a
/// single <see cref="GeminiHttpLlmClient"/> per circuit that handles its own per-call pacing
/// and model resolution via the inner factory.
/// </summary>
public sealed class GeminiLlmClientFactory : ILlmClientFactory
{
    private readonly IGeminiClientFactory _inner;
    private readonly GeminiHttpLlmClient _client;

    public GeminiLlmClientFactory(IGeminiClientFactory inner, ILogger<GeminiHttpLlmClient> logger)
    {
        _inner  = inner;
        _client = new GeminiHttpLlmClient(inner, logger);
    }

    // Gemini uses the user-selected model for all operations.
    public string SelectedModel         => _inner.SelectionModel;
    public string ClassificationModel   => _inner.SelectionModel;

    // Gemini bills only emitted tokens, not the ceiling. Set classifier ceiling high enough to
    // accommodate verbose reasoning (~150–250 tokens per card × 30 cards).
    public int ClassifierMaxOutputTokens => 32768;
    public int SelectorMaxOutputTokens   => 8192;

    public ILlmClient CreateForCurrentUser() => _client;
}
