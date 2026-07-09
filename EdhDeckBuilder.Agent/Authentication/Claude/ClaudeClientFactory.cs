using Anthropic;
using Anthropic.Core;
using EdhDeckBuilder.Agent.Interfaces;

namespace EdhDeckBuilder.Agent.Authentication.Claude;

public sealed class ClaudeClientFactory(IClaudeApiKeyProvider keys) : IClaudeClientFactory
{
    public AnthropicClient CreateForCurrentUser()
    {
        var key = keys.GetApiKey() ?? throw new MissingApiKeyException();
        return new AnthropicClient(new ClientOptions { ApiKey = key });
    }

    public string SelectionModel => keys.SelectedModel;
}
