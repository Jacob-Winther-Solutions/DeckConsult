using Anthropic;
using Anthropic.Core;

namespace EdhDeckBuilder.Agent.Authentication;

public sealed class ClaudeClientFactory(IClaudeApiKeyProvider keys) : IClaudeClientFactory
{
    public AnthropicClient CreateForCurrentUser()
    {
        var key = keys.GetApiKey() ?? throw new MissingApiKeyException();
        return new AnthropicClient(new ClientOptions { ApiKey = key });
    }

    public string SelectionModel => keys.SelectedModel;
}
