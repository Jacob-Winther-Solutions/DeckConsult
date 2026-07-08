using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Agent.Selection;
using EdhDeckBuilder.Core.Cards;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EdhDeckBuilder.Agent.Llm.Gemini;

/// <summary>
/// Google Gemini implementation of <see cref="ICardSelector"/>. Shape mirrors
/// <see cref="LlmSelector"/>: one call per role, no cache, whitelist by input OracleId.
/// </summary>
public sealed class GeminiSelector(
    IGeminiClientFactory factory,
    ILogger<GeminiSelector> logger) : ICardSelector, IUsageTrackerAware
{
    // 2048 truncated after a single rationale in the first live run — a role's response
    // covers 5–25 rankings, each with a full sentence. Sized generously; Gemini bills only
    // tokens actually emitted.
    private const int MaxOutputTokens = 8192;
    private const double Temperature  = 0.6;

    private UsageTracker? _usageTracker;

    public void SetUsageTracker(UsageTracker tracker) => _usageTracker = tracker;

    public async Task<IReadOnlyList<SelectionResult>> SelectAsync(
        CardRole role,
        IReadOnlyList<FillCandidate> candidates,
        BuildContext context,
        BuildState state,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0)
            return [];

        var client = factory.CreateForCurrentUser();
        var userMessage = SelectionPrompt.FormatUserMessage(role, candidates, context, state);
        var schema = GeminiSchemas.BuildSelectionSchema();

        var response = await client.GenerateContentAsync(
            SelectionPrompt.SystemPrompt,
            userMessage,
            schema,
            Temperature,
            MaxOutputTokens,
            ct);

        if (_usageTracker is not null && response.UsageMetadata is not null)
        {
            _usageTracker.RecordCall(
                $"Select-{role}",
                client.Model,
                response.UsageMetadata.PromptTokenCount,
                response.UsageMetadata.CandidatesTokenCount);
        }

        var payload = response.GetPayloadText();
        if (payload is null)
        {
            var finish = response.Candidates.FirstOrDefault()?.FinishReason ?? "(no candidates)";
            logger.LogError("Gemini selection returned no usable payload for role {Role}. Finish reason: {FinishReason}",
                role, finish);
            return [];
        }

        return ParsePayload(payload, candidates);
    }

    private IReadOnlyList<SelectionResult> ParsePayload(
        string payload,
        IReadOnlyList<FillCandidate> candidates)
    {
        SelectionBatchDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<SelectionBatchDto>(payload);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "Failed to parse Gemini selection payload. First 500 chars: {Payload}",
                payload[..Math.Min(500, payload.Length)]);
            return [];
        }

        var dtos = dto?.Selections ?? [];

        var batchIds = candidates.Select(c => c.Card.OracleId).ToHashSet();
        var results  = new List<SelectionResult>(dtos.Count);

        foreach (var d in dtos)
        {
            if (!Guid.TryParse(d.OracleId, out var id) || !batchIds.Contains(id))
                continue;

            results.Add(new SelectionResult
            {
                OracleId  = id,
                Rank      = d.Rank,
                Rationale = d.Rationale,
            });
        }

        return results;
    }
}
