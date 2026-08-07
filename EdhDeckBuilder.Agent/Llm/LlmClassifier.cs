using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm.Shared;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Core.Cards;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EdhDeckBuilder.Agent.Llm;

/// <summary>
/// Classifies card candidates using either Claude Haiku or the user-selected Gemini model
/// (low temperature for determinism). The factory selects the right model and token ceiling;
/// this class contains only provider-agnostic logic.
/// </summary>
public sealed class LlmClassifier(
    ILlmClientFactory factory,
    ClassificationCache cache,
    ILogger<LlmClassifier> logger) : ILlmClassifier, IUsageTrackerAware
{
    private const int BatchSize    = 30;
    private const double Temperature = 0.1;

    private UsageTracker? _usageTracker;
    public void SetUsageTracker(UsageTracker tracker) => _usageTracker = tracker;

    public async Task<IReadOnlyList<ClassificationResult>> ClassifyAsync(
        IReadOnlyList<CardCandidate> candidates,
        IReadOnlyList<Card> commanders,
        CancellationToken ct = default,
        Func<string, Task>? subProgress = null)
    {
        cache.Partition(candidates, out var hits, out var misses);

        if (misses.Count == 0)
            return hits;

        var fresh = new List<ClassificationResult>();

        if (subProgress is not null)
            await subProgress($"{hits.Count} / {candidates.Count} cards classified");

        for (int i = 0; i < misses.Count; i += BatchSize)
        {
            var batch = misses.Skip(i).Take(BatchSize).ToList();
            var results = await CallLlmAsync(batch, commanders, ct);
            fresh.AddRange(results);

            if (subProgress is not null)
                await subProgress($"{hits.Count + fresh.Count} / {candidates.Count} cards classified");
        }
        cache.Store(fresh);

        return [.. hits, .. fresh];
    }

    private async Task<IReadOnlyList<ClassificationResult>> CallLlmAsync(
        IReadOnlyList<CardCandidate> candidates,
        IReadOnlyList<Card> commanders,
        CancellationToken ct)
    {
        var client      = factory.CreateForCurrentUser();
        var model       = factory.ClassificationModel;
        var userMessage = ClassificationPrompt.FormatUserMessage(candidates, commanders);

        var request = new LlmRequest
        {
            Model          = model,
            MaxTokens      = factory.ClassifierMaxOutputTokens,
            Temperature    = Temperature,
            SystemPrompt   = ClassificationPrompt.SystemPrompt,
            Messages       = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = userMessage }] }],
            Tools          = [ClassificationPrompt.ToolDefinition],
            ForcedToolName = ClassificationPrompt.ToolName,
            EnableCaching  = true,
        };

        var response = await client.SendAsync(request, ct);

        if (_usageTracker is not null)
            _usageTracker.RecordCall(
                "ClassifyBatch", model,
                response.Usage.InputTokens,
                response.Usage.OutputTokens,
                response.Usage.CacheCreationInputTokens ?? 0,
                response.Usage.CacheReadInputTokens     ?? 0);

        ClassificationResponseLogger.LogResponse(candidates.Count, userMessage.Length, response);

        // When a forced tool call hits max_tokens, Anthropic returns input:{} (empty object)
        // instead of the partial JSON. Split and retry so no batch is silently lost.
        if (response.StopReason == "max_tokens" && candidates.Count > 1)
        {
            logger.LogWarning(
                "ClassifyBatch hit max_tokens ({Tokens} output tokens) for {Count} cards — splitting into sub-batches.",
                response.Usage.OutputTokens, candidates.Count);
            int half = candidates.Count / 2;
            var partA = await CallLlmAsync(candidates.Take(half).ToList(), commanders, ct);
            var partB = await CallLlmAsync(candidates.Skip(half).ToList(), commanders, ct);
            return [.. partA, .. partB];
        }

        return ParseResponse(response, candidates);
    }

    public async Task<string?> DescribePlanAsync(
        IReadOnlyList<Card> commanders,
        IReadOnlyList<Card> planCards,
        CancellationToken ct = default)
    {
        if (planCards.Count == 0) return null;

        var client      = factory.CreateForCurrentUser();
        var model       = factory.ClassificationModel;
        var userMessage = ClassificationPrompt.FormatPlanDescriptionMessage(commanders, planCards);

        var request = new LlmRequest
        {
            Model          = model,
            MaxTokens      = 512,
            Temperature    = Temperature,
            SystemPrompt   = ClassificationPrompt.PlanDescriptionSystemPrompt,
            Messages       = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = userMessage }] }],
            Tools          = [ClassificationPrompt.PlanDescriptionToolDefinition],
            ForcedToolName = ClassificationPrompt.PlanDescriptionToolName,
            EnableCaching  = false,
        };

        var response = await client.SendAsync(request, ct);

        if (_usageTracker is not null)
            _usageTracker.RecordCall(
                "DescribePlan", model,
                response.Usage.InputTokens,
                response.Usage.OutputTokens,
                0, 0);

        var toolUse = response.Content.OfType<LlmToolUseBlock>().FirstOrDefault();
        if (toolUse is null)
        {
            logger.LogWarning("DescribePlan: no tool use block in response");
            return null;
        }

        return toolUse.Input["description"]?.GetValue<string>();
    }

    private IReadOnlyList<ClassificationResult> ParseResponse(
        LlmResponse response,
        IReadOnlyList<CardCandidate> candidates)
    {
        var toolUse = response.Content.OfType<LlmToolUseBlock>().FirstOrDefault();

        if (toolUse is null)
        {
            logger.LogError("No tool use block in LLM classification response");
            return [];
        }

        var classificationsNode = toolUse.Input["classifications"];
        if (classificationsNode is null)
        {
            logger.LogError(
                "Tool response missing 'classifications' key. Input: {Json}",
                toolUse.Input.ToJsonString());
            return [];
        }

        var dtos = classificationsNode.Deserialize<List<CardClassificationDto>>() ?? [];

        if (dtos.Count == 0)
            logger.LogWarning(
                "Classification returned 0 results for {Count} cards (StopReason={StopReason}). Full tool input: {Json}",
                candidates.Count, response.StopReason,
                toolUse.Input.ToJsonString());

        logger.LogInformation(
            "Classification response: {InputCount} cards sent, {OutputCount} returned, {Bytes} bytes",
            candidates.Count, dtos.Count, toolUse.Input.ToJsonString().Length);

        var batchIds      = candidates.Select(c => c.Card.OracleId).ToHashSet();
        var cardsByOracle = candidates.ToDictionary(c => c.Card.OracleId, c => c.Card);

        var returnedIds = dtos
            .Select(d => Guid.TryParse(d.OracleId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var missing = candidates
            .Where(c => !returnedIds.Contains(c.Card.OracleId))
            .Select(c => c.Card.Name)
            .ToList();

        if (missing.Count > 0)
            logger.LogWarning(
                "Classification missing {Count} cards. First 10: {Examples}",
                missing.Count, string.Join(", ", missing.Take(10)));

        var results = new List<ClassificationResult>(dtos.Count);
        var seen    = new HashSet<Guid>();

        foreach (var dto in dtos)
        {
            if (!Guid.TryParse(dto.OracleId, out var id) || !batchIds.Contains(id))
                continue;

            if (!seen.Add(id))
            {
                logger.LogWarning(
                    "LLM returned duplicate oracle_id {OracleId} ({CardName}) in classification response — keeping first occurrence.",
                    id, cardsByOracle[id].Name);
                continue;
            }

            var card = cardsByOracle[id];
            var raw = new ClassificationResult
            {
                OracleId    = id,
                CardName    = card.Name,
                PrimaryRole = ParseRole(dto.PrimaryRole),
                Secondary   = dto.Secondary
                    .Select(s => new RoleContribution(
                        ParseRole(s.Role),
                        ParseRelation(s.Relation),
                        Math.Clamp(s.Weight, 0.0, 1.0)))
                    .ToArray(),
                LandCredit  = Math.Clamp(dto.LandCredit, 0.0, 1.0),
                Reasoning   = dto.Reasoning,
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
