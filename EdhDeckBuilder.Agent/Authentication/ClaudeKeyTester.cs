using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;

namespace EdhDeckBuilder.Agent.Authentication;

/// <summary>
/// Fires a minimal 1-token Haiku call to validate a key before accepting it.
/// Keeps the SDK constructor seam in one place alongside <see cref="ClaudeClientFactory"/>.
/// </summary>
public sealed class ClaudeKeyTester : IClaudeKeyTester
{
    public async Task<KeyTestResult> TestAsync(string apiKey, CancellationToken ct = default)
    {
        try
        {
            var client = new AnthropicClient(new ClientOptions { ApiKey = apiKey.Trim() });
            await client.Messages.Create(new MessageCreateParams
            {
                Model     = ClaudeModels.Haiku,
                MaxTokens = 1,
                Messages  = [new() { Role = Role.User, Content = "hi" }],
            }, ct);
            return new KeyTestResult(true, null);
        }
        catch (Exception ex)
        {
            return new KeyTestResult(false, ex.Message);
        }
    }
}
