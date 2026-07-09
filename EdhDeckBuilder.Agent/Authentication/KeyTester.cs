using EdhDeckBuilder.Agent.Authentication.Claude;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm.Claude;
using EdhDeckBuilder.Agent.Llm.Shared;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Agent.Authentication;

/// <summary>
/// Fires a minimal 1-token call to validate a key before accepting it.
/// Uses <see cref="ClaudeHttpLlmClient"/> for Anthropic so the test goes through the same
/// HTTP path as live calls. Google and GitHub keys are validated by format only.
/// </summary>
public sealed class KeyTester(
    IHttpClientFactory httpFactory,
    ILogger<ClaudeHttpLlmClient> logger) : IKeyTester
{
    public async Task<KeyTestResult> TestAsync(string apiKey, AiProvider provider, CancellationToken ct = default)
    {
        try
        {
            if (provider == AiProvider.Google)
            {
                if (apiKey.Trim().Length >= 20)
                    return new KeyTestResult(true, null);
                return new KeyTestResult(false, "Invalid Google API key format (too short)");
            }

            if (provider == AiProvider.GitHubModels)
            {
                var trimmed = apiKey.Trim();
                if (trimmed.StartsWith("ghp_") || trimmed.StartsWith("github_pat_"))
                    return new KeyTestResult(true, null);
                return new KeyTestResult(false, "Invalid GitHub token format");
            }

            var http   = httpFactory.CreateClient("claude");
            var client = new ClaudeHttpLlmClient(http, apiKey.Trim(), logger);

            await client.SendAsync(new LlmRequest
            {
                Model     = ClaudeModels.Haiku,
                MaxTokens = 1,
                Messages  = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = "hi" }] }],
            }, ct);

            return new KeyTestResult(true, null);
        }
        catch (ApiKeyRejectedException ex)
        {
            return new KeyTestResult(false, ex.InnerException?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            return new KeyTestResult(false, ex.Message);
        }
    }
}
