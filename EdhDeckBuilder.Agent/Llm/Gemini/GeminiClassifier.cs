using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Core.Cards;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EdhDeckBuilder.Agent.Llm.Gemini;

/// <summary>
/// Google Gemini implementation of <see cref="ILlmClassifier"/>. Mirrors
/// <see cref="LlmClassifier"/>: shared batching, caching, whitelist, and sanitization —
/// only the transport (REST via <see cref="GeminiRestClient"/>) and response shape differ.
/// </summary>
public sealed class GeminiClassifier(
    IGeminiClientFactory factory,
    ClassificationCache cache,
    ILogger<GeminiClassifier> logger) : ILlmClassifier, IUsageTrackerAware
{
    // Gemini bills only tokens actually emitted, not the ceiling. Set generously so verbose
    // reasoning (~150–250 output tokens per card) on a full 30-card batch has room; the
    // 4096 default truncated every real batch during the first live run.
    private const int MaxOutputTokens = 32768;
    private const int BatchSize       = 30;
    private const double Temperature  = 0.1;

    private UsageTracker? _usageTracker;

    public void SetUsageTracker(UsageTracker tracker) => _usageTracker = tracker;

    public async Task<IReadOnlyList<ClassificationResult>> ClassifyAsync(
        IReadOnlyList<CardCandidate> candidates,
        IReadOnlyList<Card> commanders,
        CancellationToken ct = default)
    {
        cache.Partition(candidates, out var hits, out var misses);

        if (misses.Count == 0)
            return hits;

        var fresh = new List<ClassificationResult>();
        for (int i = 0; i < misses.Count; i += BatchSize)
        {
            var batch = misses.Skip(i).Take(BatchSize).ToList();
            var results = await CallLlmAsync(batch, commanders, ct);
            fresh.AddRange(results);
        }
        cache.Store(fresh);

        return [.. hits, .. fresh];
    }

    private async Task<IReadOnlyList<ClassificationResult>> CallLlmAsync(
        IReadOnlyList<CardCandidate> candidates,
        IReadOnlyList<Card> commanders,
        CancellationToken ct)
    {
        var client = factory.CreateForCurrentUser();
        var userMessage = ClassificationPrompt.FormatUserMessage(candidates, commanders);
        var schema = GeminiSchemas.BuildClassificationSchema(ClassificationPrompt.IsReasoningEnabled);

        var response = await client.GenerateContentAsync(
            ClassificationPrompt.SystemPrompt,
            userMessage,
            schema,
            Temperature,
            MaxOutputTokens,
            ct);

        if (_usageTracker is not null && response.UsageMetadata is not null)
        {
            _usageTracker.RecordCall(
                "ClassifyBatch",
                client.Model,
                response.UsageMetadata.PromptTokenCount,
                response.UsageMetadata.CandidatesTokenCount);
        }

        var payload = response.GetPayloadText();
        if (payload is null)
        {
            var finish = response.Candidates.FirstOrDefault()?.FinishReason ?? "(no candidates)";
            logger.LogError("Gemini classification returned no usable payload. Finish reason: {FinishReason}", finish);
            return [];
        }

        return ParsePayload(payload, candidates);
    }

    private IReadOnlyList<ClassificationResult> ParsePayload(
        string payload,
        IReadOnlyList<CardCandidate> candidates)
    {
        ClassificationBatchDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ClassificationBatchDto>(payload);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "Failed to parse Gemini classification payload. First 500 chars: {Payload}",
                payload[..Math.Min(500, payload.Length)]);
            return [];
        }

        var dtos = dto?.Classifications ?? [];

        logger.LogInformation(
            "Gemini classification: {InputCount} cards sent, {OutputCount} returned, {PayloadBytes} bytes",
            candidates.Count, dtos.Count, payload.Length);

        var batchIds = candidates.Select(c => c.Card.OracleId).ToHashSet();
        var cardsByOracleId = candidates.ToDictionary(c => c.Card.OracleId, c => c.Card);

        var returnedIds = dtos
            .Select(d => Guid.TryParse(d.OracleId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var missing = candidates.Where(c => !returnedIds.Contains(c.Card.OracleId))
            .Select(c => c.Card.Name)
            .ToList();

        if (missing.Count > 0)
        {
            logger.LogWarning(
                "Gemini classification missing {MissingCount} cards. First 10: {Examples}",
                missing.Count, string.Join(", ", missing.Take(10)));
        }

        var results = new List<ClassificationResult>(dtos.Count);

        foreach (var d in dtos)
        {
            if (!Guid.TryParse(d.OracleId, out var id) || !batchIds.Contains(id))
                continue;

            var card = cardsByOracleId[id];
            var raw = new ClassificationResult
            {
                OracleId    = id,
                CardName    = card.Name,
                PrimaryRole = ParseRole(d.PrimaryRole),
                Secondary   = d.Secondary
                    .Select(s => new RoleContribution(
                        ParseRole(s.Role),
                        ParseRelation(s.Relation),
                        Math.Clamp(s.Weight, 0.0, 1.0)))
                    .ToArray(),
                LandCredit  = Math.Clamp(d.LandCredit, 0.0, 1.0),
                Reasoning   = d.Reasoning,
            };

            var r1 = ClassificationSanitizer.SanitizeLandRole(raw, card.Types);
            results.Add(ClassificationSanitizer.SanitizeLandCredit(r1, card.BackFaceTypeLine));
        }

        return results;
    }

    private static CardRole ParseRole(string s) =>
        Enum.TryParse<CardRole>(s, ignoreCase: true, out var r) ? r : CardRole.Unmatched;

    private static RoleRelation ParseRelation(string s) =>
        Enum.TryParse<RoleRelation>(s, ignoreCase: true, out var r) ? r : RoleRelation.Always;
}
