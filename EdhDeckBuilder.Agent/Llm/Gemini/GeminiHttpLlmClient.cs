using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm.Shared;
using EdhDeckBuilder.Agent.Prompts;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Agent.Llm.Gemini;

/// <summary>
/// Wraps <see cref="GeminiRestClient"/> behind <see cref="ILlmClient"/>.
/// Translates <see cref="LlmRequest"/> to Gemini's <c>generateContent</c> parameters and
/// maps the response back into provider-agnostic <see cref="LlmResponse"/> DTOs.
/// <para>
/// Gemini does not use function-call tool-use blocks in the wire format; instead, it uses a
/// <c>responseSchema</c> in <c>generationConfig</c>. This adapter looks up the correct
/// Gemini schema from <see cref="GeminiSchemas"/> by <see cref="LlmRequest.ForcedToolName"/>
/// and simulates an <see cref="LlmToolUseBlock"/> in the response so callers can treat both
/// providers uniformly.
/// </para>
/// </summary>
public sealed class GeminiHttpLlmClient(
    IGeminiClientFactory factory,
    ILogger<GeminiHttpLlmClient> logger) : ILlmClient
{
    public async Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default)
    {
        var client = factory.CreateForCurrentUser();

        var systemPrompt = request.SystemPrompt ?? "";
        var userMessage  = ExtractUserMessage(request);
        var schema       = ResolveSchema(request.ForcedToolName);
        var temperature  = request.Temperature ?? 1.0;
        var maxTokens    = request.MaxTokens;

        if (InstrumentationOptions.Current.LogRawLlmRequests)
            logger.LogDebug(
                "[Gemini] Outgoing request — model: {Model}, tool: {Tool}, temp: {Temp}, maxTokens: {Max}\nSystem: {System}\nUser: {User}",
                client.Model, request.ForcedToolName ?? "(none)", temperature, maxTokens,
                Truncate(systemPrompt, 500), Truncate(userMessage, 500));

        var response = await client.GenerateContentAsync(
            systemPrompt,
            userMessage,
            schema,
            temperature,
            maxTokens,
            ct);

        var payload = response.GetPayloadText();
        var finishReason = response.Candidates.FirstOrDefault()?.FinishReason ?? "STOP";

        if (InstrumentationOptions.Current.LogRawLlmRequests)
            logger.LogDebug(
                "[Gemini] Response — finishReason: {FinishReason}, payload ({Bytes} chars): {Payload}",
                finishReason,
                payload?.Length ?? 0,
                payload is null ? "(null)" : Truncate(payload, 1000));

        var usage = new LlmUsage
        {
            InputTokens  = response.UsageMetadata?.PromptTokenCount     ?? 0,
            OutputTokens = response.UsageMetadata?.CandidatesTokenCount ?? 0,
        };

        // Map Gemini finish reason to a common convention (mirrors Anthropic's stop_reason).
        var stopReason = finishReason switch
        {
            "STOP"       => "end_turn",
            "MAX_TOKENS" => "max_tokens",
            _            => finishReason.ToLowerInvariant(),
        };

        if (payload is null)
        {
            logger.LogError(
                "[Gemini] No usable payload from model. finish_reason: {FinishReason}", finishReason);
            return new LlmResponse { Content = [], Usage = usage, StopReason = stopReason };
        }

        // Simulate a tool-use block so callers work uniformly across both providers.
        JsonNode input;
        try
        {
            input = JsonNode.Parse(payload) ?? new JsonObject();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Gemini] Failed to parse payload as JSON. First 500 chars: {Payload}",
                Truncate(payload, 500));
            return new LlmResponse { Content = [], Usage = usage, StopReason = stopReason };
        }

        var toolBlock = new LlmToolUseBlock
        {
            Id       = "gemini-response",
            ToolName = request.ForcedToolName ?? "",
            Input    = input,
        };

        return new LlmResponse
        {
            Content    = [toolBlock],
            Usage      = usage,
            StopReason = stopReason,
        };
    }

    /// <summary>
    /// Extracts the user-turn text from the request messages.
    /// In current usage there is always exactly one user message with a single text block.
    /// </summary>
    private static string ExtractUserMessage(LlmRequest request)
    {
        foreach (var msg in request.Messages)
        {
            if (msg.Role != LlmRole.User)
                continue;

            foreach (var block in msg.Content)
            {
                if (block is LlmTextBlock text)
                    return text.Text;
            }
        }
        return "";
    }

    /// <summary>
    /// Looks up the Gemini response schema for the requested tool name.
    /// The three tool names are defined as constants in the <c>*Prompt</c> classes.
    /// </summary>
    private static JsonNode ResolveSchema(string? toolName) => toolName switch
    {
        ClassificationPrompt.ToolName           => GeminiSchemas.BuildClassificationSchema(ClassificationPrompt.IsReasoningEnabled),
        SelectionPrompt.ToolName                => GeminiSchemas.BuildSelectionSchema(),
        CommanderSelectionPrompt.ToolName       => GeminiSchemas.BuildCommanderSelectionSchema(),
        UpgradeSelectionPrompt.PrioritizationToolName  => GeminiSchemas.BuildGapPrioritizationSchema(),
        UpgradeSelectionPrompt.SelectionToolName       => GeminiSchemas.BuildUpgradeSelectionSchema(),
        ClassificationPrompt.PlanDescriptionToolName   => GeminiSchemas.BuildPlanDescriptionSchema(),
        _ => throw new InvalidOperationException($"No Gemini schema registered for tool '{toolName}'."),
    };

    private static string Truncate(string s, int max) => s.Length > max ? s[..max] + "…" : s;
}
