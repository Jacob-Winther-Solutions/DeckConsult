using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Llm.Shared;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Agent.Llm.Claude;

/// <summary>
/// Sends requests to the Anthropic Messages API via direct HTTP, replacing the C# SDK.
/// Handles retries, auth-error mapping to <see cref="ApiKeyRejectedException"/>, and
/// raw JSON logging when <see cref="InstrumentationOptions.LogRawLlmRequests"/> is enabled.
/// </summary>
public sealed class ClaudeHttpLlmClient(
    HttpClient http,
    string apiKey,
    ILogger<ClaudeHttpLlmClient> logger) : ILlmClient
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const int MaxAttempts = 3;

    private static readonly HashSet<HttpStatusCode> RetryableStatuses =
    [
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
    ];

    public async Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default)
    {
        var bodyJson = BuildRequestJson(request);

        if (InstrumentationOptions.Current.LogRawLlmRequests)
            logger.LogDebug("[Claude] Outgoing request body:\n{Body}", bodyJson);

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            httpRequest.Headers.Add("x-api-key", apiKey);
            httpRequest.Headers.Add("anthropic-version", AnthropicVersion);
            httpRequest.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(httpRequest, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (InstrumentationOptions.Current.LogRawLlmRequests)
                logger.LogDebug("[Claude] Response ({StatusCode}):\n{Body}", (int)response.StatusCode, body);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ApiKeyRejectedException(
                    new HttpRequestException($"Anthropic API rejected the key ({(int)response.StatusCode}): {ExtractErrorMessage(body)}"));

            if (RetryableStatuses.Contains(response.StatusCode) && attempt < MaxAttempts)
            {
                var delay = GetRetryDelay(response, attempt);
                logger.LogWarning(
                    "[Claude] HTTP {Status} on attempt {Attempt}/{Max}; retrying in {Delay:F1}s. {Error}",
                    (int)response.StatusCode, attempt, MaxAttempts, delay.TotalSeconds, ExtractErrorMessage(body));
                await Task.Delay(delay, ct);
                continue;
            }

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Anthropic API returned {(int)response.StatusCode} {response.ReasonPhrase}: {ExtractErrorMessage(body)}");

            return ParseResponse(body);
        }

        throw new InvalidOperationException("Claude HTTP retry loop exhausted without a result.");
    }

    private static string BuildRequestJson(LlmRequest request)
    {
        var body = new JsonObject
        {
            ["model"]      = request.Model,
            ["max_tokens"] = request.MaxTokens,
        };

        if (request.Temperature is double temp && ModelSupportsTemperature(request.Model))
            body["temperature"] = temp;

        if (request.SystemPrompt is not null)
        {
            if (request.EnableCaching)
            {
                body["system"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"]          = "text",
                        ["text"]          = request.SystemPrompt,
                        ["cache_control"] = new JsonObject { ["type"] = "ephemeral" },
                    }
                };
            }
            else
            {
                body["system"] = request.SystemPrompt;
            }
        }

        if (request.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            for (int i = 0; i < request.Tools.Count; i++)
            {
                var tool    = request.Tools[i];
                var toolObj = new JsonObject
                {
                    ["name"]         = tool.Name,
                    ["description"]  = tool.Description,
                    ["input_schema"] = tool.InputSchema.DeepClone(),
                };
                // Cache the tool definition together with the system prompt on the last entry.
                if (request.EnableCaching && i == request.Tools.Count - 1)
                    toolObj["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
                tools.Add(toolObj);
            }
            body["tools"] = tools;
        }

        if (request.ForcedToolName is not null)
        {
            body["tool_choice"] = new JsonObject
            {
                ["type"] = "tool",
                ["name"] = request.ForcedToolName,
            };
        }

        var messages = new JsonArray();
        foreach (var msg in request.Messages)
        {
            var role = msg.Role == LlmRole.User ? "user" : "assistant";
            var msgObj = new JsonObject { ["role"] = role };

            // Optimization: Anthropic accepts a plain string for single-text messages.
            if (msg.Content is [LlmTextBlock single])
            {
                msgObj["content"] = single.Text;
            }
            else
            {
                var contentArray = new JsonArray();
                foreach (var block in msg.Content)
                {
                    contentArray.Add(block switch
                    {
                        LlmTextBlock t => new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = t.Text,
                        },
                        LlmToolUseBlock tu => new JsonObject
                        {
                            ["type"]  = "tool_use",
                            ["id"]    = tu.Id,
                            ["name"]  = tu.ToolName,
                            ["input"] = tu.Input.DeepClone(),
                        },
                        LlmToolResultBlock tr => new JsonObject
                        {
                            ["type"]        = "tool_result",
                            ["tool_use_id"] = tr.ToolUseId,
                            ["content"]     = tr.Result,
                        },
                        _ => throw new InvalidOperationException($"Unknown content block: {block.GetType().Name}"),
                    });
                }
                msgObj["content"] = contentArray;
            }

            messages.Add(msgObj);
        }
        body["messages"] = messages;

        return body.ToJsonString();
    }

    private static LlmResponse ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var content = new List<LlmContentBlock>();
        if (root.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in contentEl.EnumerateArray())
            {
                var type = block.TryGetProperty("type", out var t) ? t.GetString() : null;
                switch (type)
                {
                    case "text":
                        var text = block.TryGetProperty("text", out var te) ? te.GetString() ?? "" : "";
                        content.Add(new LlmTextBlock { Text = text });
                        break;

                    case "tool_use":
                        var id    = block.TryGetProperty("id",    out var ide) ? ide.GetString() ?? "" : "";
                        var name  = block.TryGetProperty("name",  out var ne)  ? ne.GetString()  ?? "" : "";
                        var input = block.TryGetProperty("input", out var inp)
                            ? JsonNode.Parse(inp.GetRawText()) ?? new JsonObject()
                            : new JsonObject();
                        content.Add(new LlmToolUseBlock { Id = id, ToolName = name, Input = input });
                        break;
                }
            }
        }

        int inputTokens = 0, outputTokens = 0;
        int? cacheCreate = null, cacheRead = null;
        if (root.TryGetProperty("usage", out var usageEl))
        {
            if (usageEl.TryGetProperty("input_tokens",  out var it)) inputTokens  = it.GetInt32();
            if (usageEl.TryGetProperty("output_tokens", out var ot)) outputTokens = ot.GetInt32();
            if (usageEl.TryGetProperty("cache_creation_input_tokens", out var cc)
                && cc.ValueKind != JsonValueKind.Null)
                cacheCreate = cc.GetInt32();
            if (usageEl.TryGetProperty("cache_read_input_tokens", out var cr)
                && cr.ValueKind != JsonValueKind.Null)
                cacheRead = cr.GetInt32();
        }

        var stopReason = root.TryGetProperty("stop_reason", out var sr) ? sr.GetString() ?? "" : "";

        return new LlmResponse
        {
            Content    = content,
            StopReason = stopReason,
            Usage = new LlmUsage
            {
                InputTokens              = inputTokens,
                OutputTokens             = outputTokens,
                CacheCreationInputTokens = cacheCreate,
                CacheReadInputTokens     = cacheRead,
            },
        };
    }

    // Anthropic deprecated the temperature parameter for models newer than claude-opus-4-6.
    // Haiku 4.5 and claude-3 variants still accept it.
    private static bool ModelSupportsTemperature(string modelId) =>
        modelId.Contains("haiku-4-5", StringComparison.OrdinalIgnoreCase) ||
        modelId.Contains("claude-3",  StringComparison.OrdinalIgnoreCase);

    private static string ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "(empty body)";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err)
                && err.TryGetProperty("message", out var msg))
                return msg.GetString() ?? Truncate(body);
        }
        catch (JsonException) { }
        return Truncate(body);
    }

    private static string Truncate(string s) => s.Length > 500 ? s[..500] + "…" : s;

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        // Check Anthropic's retry-after-ms header first (milliseconds)
        if (response.Headers.TryGetValues("retry-after-ms", out var msValues)
            && int.TryParse(msValues.FirstOrDefault(), out var ms))
            return TimeSpan.FromMilliseconds(ms);

        // Standard Retry-After header (seconds or date)
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
            return delta;
        if (retryAfter?.Date is DateTimeOffset when)
        {
            var wait = when - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
                return wait;
        }

        // Exponential backoff: 2s, 4s
        return TimeSpan.FromSeconds(Math.Pow(2, attempt));
    }
}
