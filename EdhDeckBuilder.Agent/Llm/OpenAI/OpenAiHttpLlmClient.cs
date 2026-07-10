using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Llm.Shared;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Agent.Llm.OpenAI;

/// <summary>
/// Sends requests to the OpenAI Chat Completions API via direct HTTP.
/// Handles retries, auth-error mapping to <see cref="ApiKeyRejectedException"/>, and
/// raw JSON logging when <see cref="InstrumentationOptions.LogRawLlmRequests"/> is enabled.
/// </summary>
public sealed class OpenAiHttpLlmClient(
    HttpClient http,
    string apiKey,
    ILogger<OpenAiHttpLlmClient> logger) : ILlmClient
{
    private const string ApiUrl = "https://api.openai.com/v1/chat/completions";
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
            logger.LogDebug("[OpenAI] Outgoing request body:\n{Body}", bodyJson);

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(httpRequest, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (InstrumentationOptions.Current.LogRawLlmRequests)
                logger.LogDebug("[OpenAI] Response ({StatusCode}):\n{Body}", (int)response.StatusCode, body);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ApiKeyRejectedException(
                    new HttpRequestException($"OpenAI API rejected the key ({(int)response.StatusCode}): {ExtractErrorMessage(body)}"));

            if (response.StatusCode == HttpStatusCode.TooManyRequests && IsQuotaError(body))
                throw new QuotaExceededException(ExtractErrorMessage(body));

            if (RetryableStatuses.Contains(response.StatusCode) && attempt < MaxAttempts)
            {
                var delay = GetRetryDelay(response, attempt);
                logger.LogWarning(
                    "[OpenAI] HTTP {Status} on attempt {Attempt}/{Max}; retrying in {Delay:F1}s. {Error}",
                    (int)response.StatusCode, attempt, MaxAttempts, delay.TotalSeconds, ExtractErrorMessage(body));
                await Task.Delay(delay, ct);
                continue;
            }

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"OpenAI API returned {(int)response.StatusCode} {response.ReasonPhrase}: {ExtractErrorMessage(body)}");

            return ParseResponse(body);
        }

        throw new InvalidOperationException("OpenAI HTTP retry loop exhausted without a result.");
    }

    internal static string BuildRequestJson(LlmRequest request)
    {
        var body = new JsonObject
        {
            ["model"] = request.Model,
        };

        // o-series reasoning models use max_completion_tokens and reject temperature.
        if (IsReasoningModel(request.Model))
        {
            body["max_completion_tokens"] = request.MaxTokens;
        }
        else
        {
            body["max_tokens"] = request.MaxTokens;
            if (request.Temperature is double temp)
                body["temperature"] = temp;
        }

        var messages = new JsonArray();

        if (request.SystemPrompt is not null)
        {
            messages.Add(new JsonObject
            {
                ["role"]    = "system",
                ["content"] = request.SystemPrompt,
            });
        }

        foreach (var msg in request.Messages)
        {
            var role   = msg.Role == LlmRole.User ? "user" : "assistant";
            var msgObj = new JsonObject { ["role"] = role };

            if (msg.Content is [LlmTextBlock single])
            {
                msgObj["content"] = single.Text;
            }
            else
            {
                var contentArray = new JsonArray();
                foreach (var block in msg.Content)
                {
                    if (block is LlmTextBlock t)
                        contentArray.Add(new JsonObject { ["type"] = "text", ["text"] = t.Text });
                }
                msgObj["content"] = contentArray;
            }

            messages.Add(msgObj);
        }

        body["messages"] = messages;

        if (request.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"]        = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"]  = tool.InputSchema.DeepClone(),
                    },
                });
            }
            body["tools"] = tools;
        }

        if (request.ForcedToolName is not null)
        {
            body["tool_choice"] = new JsonObject
            {
                ["type"]     = "function",
                ["function"] = new JsonObject { ["name"] = request.ForcedToolName },
            };
        }

        return body.ToJsonString();
    }

    internal static LlmResponse ParseResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var content = new List<LlmContentBlock>();
        var stopReason = "";

        if (root.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];

            if (choice.TryGetProperty("finish_reason", out var fr))
                stopReason = fr.GetString() ?? "";

            if (choice.TryGetProperty("message", out var message))
            {
                if (message.TryGetProperty("content", out var contentEl)
                    && contentEl.ValueKind == JsonValueKind.String)
                {
                    var text = contentEl.GetString() ?? "";
                    if (!string.IsNullOrEmpty(text))
                        content.Add(new LlmTextBlock { Text = text });
                }

                if (message.TryGetProperty("tool_calls", out var toolCalls)
                    && toolCalls.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tc in toolCalls.EnumerateArray())
                    {
                        var id      = tc.TryGetProperty("id",       out var i) ? i.GetString() ?? "" : "";
                        var func    = tc.TryGetProperty("function", out var f) ? f : default;
                        var name    = func.TryGetProperty("name",      out var n) ? n.GetString() ?? "" : "";
                        var argsRaw = func.TryGetProperty("arguments", out var a) ? a.GetString() ?? "{}" : "{}";

                        JsonNode input;
                        try { input = JsonNode.Parse(argsRaw) ?? new JsonObject(); }
                        catch { input = new JsonObject(); }

                        content.Add(new LlmToolUseBlock { Id = id, ToolName = name, Input = input });
                    }
                }
            }
        }

        int inputTokens = 0, outputTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens",     out var pt)) inputTokens  = pt.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var ct)) outputTokens = ct.GetInt32();
        }

        // Map OpenAI finish reasons to the common convention used across providers.
        stopReason = stopReason switch
        {
            "tool_calls" => "tool_use",
            "stop"       => "end_turn",
            "length"     => "max_tokens",
            _            => stopReason,
        };

        return new LlmResponse
        {
            Content    = content,
            StopReason = stopReason,
            Usage = new LlmUsage
            {
                InputTokens  = inputTokens,
                OutputTokens = outputTokens,
            },
        };
    }

    // A 429 is a quota/billing error when the body mentions "quota" — as opposed to a
    // transient rate-limit (also 429) which is worth retrying.
    private static bool IsQuotaError(string body) =>
        body.Contains("quota", StringComparison.OrdinalIgnoreCase);

    // o-series reasoning models (o1, o3, o4-mini, …) reject temperature and use
    // max_completion_tokens instead of max_tokens.
    internal static bool IsReasoningModel(string modelId) =>
        modelId.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
        modelId.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
        modelId.StartsWith("o4", StringComparison.OrdinalIgnoreCase);

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
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
            return delta;
        if (retryAfter?.Date is DateTimeOffset when)
        {
            var wait = when - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
                return wait;
        }
        return TimeSpan.FromSeconds(Math.Pow(2, attempt));
    }
}
