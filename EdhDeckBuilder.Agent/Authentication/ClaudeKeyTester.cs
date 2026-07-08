using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;

namespace EdhDeckBuilder.Agent.Authentication;

/// <summary>
/// Fires a minimal 1-token call to validate a key before accepting it.
/// Currently only supports Anthropic keys. GitHub Models support deferred pending SDK stabilization.
/// Keeps the SDK constructor seam in one place alongside <see cref="ClaudeClientFactory"/>.
/// </summary>
public sealed class ClaudeKeyTester : IClaudeKeyTester
{
    public async Task<KeyTestResult> TestAsync(string apiKey, AiProvider provider, CancellationToken ct = default)
    {
        try
        {
            // For now, only validate Anthropic keys. GitHub Models validation deferred.
            if (provider == AiProvider.GitHubModels)
            {
                // Placeholder: accept any ghp_ or github_pat_ token as valid
                if (apiKey.Trim().StartsWith("ghp_") || apiKey.Trim().StartsWith("github_pat_"))
                    return new KeyTestResult(true, null);
                return new KeyTestResult(false, "Invalid GitHub token format");
            }

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
