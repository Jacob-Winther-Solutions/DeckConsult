using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Core.Cards;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EdhDeckBuilder.Agent.Llm.Gemini;

/// <summary>
/// Google Gemini implementation of <see cref="ICommanderSelector"/>. Mirrors
/// the Claude adapter: one call, whitelist by input OracleId.
/// </summary>
public sealed class GeminiCommanderSelector(
    IGeminiClientFactory factory,
    ILogger<GeminiCommanderSelector> logger) : ICommanderSelector, IUsageTrackerAware
{
    // Sized to match GeminiSelector: 10-commander rankings × full-sentence rationale
    // each fits comfortably under 8k output tokens.
    private const int MaxOutputTokens = 8192;
    private const double Temperature  = 0.6;

    private UsageTracker? _usageTracker;

    public void SetUsageTracker(UsageTracker tracker) => _usageTracker = tracker;

    public async Task<IReadOnlyList<CommanderSelectionResult>> SelectAsync(
        IReadOnlyList<Card> candidates,
        CommanderDiscoveryRequest request,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0)
            return [];

        var client = factory.CreateForCurrentUser();
        var userMessage = CommanderSelectionPrompt.FormatUserMessage(candidates, request);
        var schema = GeminiSchemas.BuildCommanderSelectionSchema();

        var response = await client.GenerateContentAsync(
            CommanderSelectionPrompt.SystemPrompt,
            userMessage,
            schema,
            Temperature,
            MaxOutputTokens,
            ct);

        if (_usageTracker is not null && response.UsageMetadata is not null)
        {
            _usageTracker.RecordCall(
                $"Commander-Selection-{candidates.Count}",
                client.Model,
                response.UsageMetadata.PromptTokenCount,
                response.UsageMetadata.CandidatesTokenCount);
        }

        var payload = response.GetPayloadText();
        if (payload is null)
        {
            var finish = response.Candidates.FirstOrDefault()?.FinishReason ?? "(no candidates)";
            logger.LogError("Gemini commander selection returned no usable payload. Finish reason: {FinishReason}", finish);
            return [];
        }

        return ParsePayload(payload, candidates);
    }

    private IReadOnlyList<CommanderSelectionResult> ParsePayload(
        string payload,
        IReadOnlyList<Card> candidates)
    {
        CommanderRankingBatchDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CommanderRankingBatchDto>(payload);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "Failed to parse Gemini commander selection payload. First 500 chars: {Payload}",
                payload[..Math.Min(500, payload.Length)]);
            return [];
        }

        var dtos = dto?.Rankings ?? [];

        var batchIds = candidates.Select(c => c.OracleId).ToHashSet();
        var results  = new List<CommanderSelectionResult>(dtos.Count);

        foreach (var d in dtos)
        {
            if (!Guid.TryParse(d.OracleId, out var id) || !batchIds.Contains(id))
                continue;

            results.Add(new CommanderSelectionResult(id, d.Rank, d.Rationale));
        }

        return results;
    }
}
