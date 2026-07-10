using EdhDeckBuilder.Agent.Authentication.Claude;
using EdhDeckBuilder.Agent.Authentication.OpenAI;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm.Claude;
using EdhDeckBuilder.Agent.Llm.OpenAI;
using EdhDeckBuilder.Agent.Llm.Shared;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Agent.Authentication;

/// <summary>
/// Fires a minimal 1-token call to validate a key before accepting it.
/// Uses <see cref="ClaudeHttpLlmClient"/> for Anthropic and <see cref="OpenAiHttpLlmClient"/>
/// for OpenAI so both go through the same HTTP path as live calls.
/// Google keys are validated by format only (no free-tier probe endpoint).
/// </summary>
public sealed class KeyTester(
    IHttpClientFactory httpFactory,
    ILogger<ClaudeHttpLlmClient> claudeLogger,
    ILogger<OpenAiHttpLlmClient> openAiLogger) : IKeyTester
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

            if (provider == AiProvider.OpenAI)
            {
                var http   = httpFactory.CreateClient("openai");
                var client = new OpenAiHttpLlmClient(http, apiKey.Trim(), openAiLogger);

                await client.SendAsync(new LlmRequest
                {
                    Model    = OpenAiModels.Gpt4oMini,
                    MaxTokens = 1,
                    Messages  = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = "hi" }] }],
                }, ct);

                return new KeyTestResult(true, null);
            }

            var claudeHttp   = httpFactory.CreateClient("claude");
            var claudeClient = new ClaudeHttpLlmClient(claudeHttp, apiKey.Trim(), claudeLogger);

            await claudeClient.SendAsync(new LlmRequest
            {
                Model     = ClaudeModels.Haiku,
                MaxTokens = 1,
                Messages  = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = "hi" }] }],
            }, ct);

            return new KeyTestResult(true, null);
        }
        catch (ApiKeyRejectedException ex)
        {
            return new KeyTestResult(false, $"Key rejected — {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (QuotaExceededException ex)
        {
            return new KeyTestResult(false, $"Billing limit reached. Add credits to your provider account and try again. ({ex.Message})");
        }
        catch (Exception ex)
        {
            return new KeyTestResult(false, ex.Message);
        }
    }
}
