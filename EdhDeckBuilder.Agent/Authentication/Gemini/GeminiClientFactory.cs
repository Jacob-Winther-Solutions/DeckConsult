using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm.Gemini;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Agent.Authentication.Gemini;

/// <summary>
/// Constructs a <see cref="GeminiRestClient"/> bound to the current circuit's API key and
/// selected model. The <see cref="HttpClient"/> is injected via <c>IHttpClientFactory</c>
/// (registered in <c>AddAgent</c>), which handles pooling and lifetime.
/// <para>
/// The per-circuit <see cref="GeminiRateLimiter"/> is shared across every client the factory
/// hands out so pacing state persists across classifier + selector calls within a build.
/// </para>
/// </summary>
public sealed class GeminiClientFactory(
    HttpClient http,
    IClaudeApiKeyProvider keys,
    GeminiRateLimiter limiter,
    ILogger<GeminiRestClient> logger) : IGeminiClientFactory
{
    public GeminiRestClient CreateForCurrentUser()
    {
        var key = keys.GetApiKey() ?? throw new MissingApiKeyException();
        return new GeminiRestClient(http, key, keys.SelectedModel, limiter, logger);
    }

    public string SelectionModel => keys.SelectedModel;
}
