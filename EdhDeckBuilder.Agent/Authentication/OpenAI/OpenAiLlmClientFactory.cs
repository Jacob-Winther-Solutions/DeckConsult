using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm.OpenAI;
using EdhDeckBuilder.Agent.Llm.Shared;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Agent.Authentication.OpenAI;

/// <summary>
/// Creates <see cref="OpenAiHttpLlmClient"/> instances bound to the current circuit's API key.
/// Registered via <c>AddHttpClient&lt;OpenAiLlmClientFactory&gt;</c> so the injected
/// <see cref="HttpClient"/> is pooled by <c>IHttpClientFactory</c>.
/// </summary>
public sealed class OpenAiLlmClientFactory(
    HttpClient http,
    IClaudeApiKeyProvider keys,
    ILogger<OpenAiHttpLlmClient> logger) : ILlmClientFactory
{
    // Classification always uses gpt-4o-mini regardless of user's model selection
    // (fast + cheap, analogous to Haiku on the Anthropic path).
    public string ClassificationModel        => OpenAiModels.Gpt4oMini;
    public int    ClassifierMaxOutputTokens  => 8192;

    public string SelectedModel              => keys.SelectedModel;
    public int    SelectorMaxOutputTokens    => 8192;

    public ILlmClient CreateForCurrentUser()
    {
        var key = keys.GetApiKey() ?? throw new MissingApiKeyException();
        return new OpenAiHttpLlmClient(http, key, logger);
    }
}
