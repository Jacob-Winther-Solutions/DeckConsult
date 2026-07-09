using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm.Claude;
using EdhDeckBuilder.Agent.Llm.Shared;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Agent.Authentication.Claude;

/// <summary>
/// Creates <see cref="ClaudeHttpLlmClient"/> instances bound to the current circuit's API key.
/// Registered via <c>AddHttpClient&lt;ClaudeHttpLlmClientFactory&gt;</c> so the injected
/// <see cref="HttpClient"/> is pooled by <c>IHttpClientFactory</c>.
/// </summary>
public sealed class ClaudeHttpLlmClientFactory(
    HttpClient http,
    IClaudeApiKeyProvider keys,
    ILogger<ClaudeHttpLlmClient> logger) : ILlmClientFactory
{
    // Classification always uses Haiku regardless of user's model selection.
    // 8192 tokens covers 30-card batches even with reasoning fields enabled
    // (observed peak: ~4096 tokens truncated the response at max_tokens).
    public string ClassificationModel => ClaudeModels.Haiku;
    public int ClassifierMaxOutputTokens => 8192;

    public string SelectedModel => keys.SelectedModel;
    public int SelectorMaxOutputTokens => 8192;

    public ILlmClient CreateForCurrentUser()
    {
        var key = keys.GetApiKey() ?? throw new MissingApiKeyException();
        return new ClaudeHttpLlmClient(http, key, logger);
    }
}
