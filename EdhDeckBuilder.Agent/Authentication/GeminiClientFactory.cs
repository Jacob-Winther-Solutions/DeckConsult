using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace EdhDeckBuilder.Agent.Authentication;

/// <summary>
/// Creates OpenAI ChatClient pointed at Google Gemini's OpenAI-compatible endpoint.
/// Mirrors ClaudeClientFactory pattern but uses Gemini as the provider.
/// </summary>
public sealed class GeminiClientFactory(IClaudeApiKeyProvider keys) : IGeminiClientFactory
{
    public ChatClient CreateForCurrentUser()
    {
        var key = keys.GetApiKey() ?? throw new MissingApiKeyException();
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/")
        };
        var client = new OpenAIClient(new ApiKeyCredential(key), options);
        return client.GetChatClient(keys.SelectedModel);
    }

    public string SelectionModel => keys.SelectedModel;
}
