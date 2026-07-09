using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Authentication.Claude;
using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Core.Cards;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EdhDeckBuilder.Agent.Llm.Claude;

/// <summary>
/// Classifies a batch of card candidates using Claude Haiku (low temperature for determinism).
/// Uses a forced tool call so output is always structured JSON that matches the schema.
/// Results for non-commander-dependent roles (anything except Plan and Synergy) are cached in
/// <see cref="ClassificationCache"/> across multiple builds in the same session.
/// </summary>
public sealed class ClaudeClassifier(
    IClaudeClientFactory factory,
    ClassificationCache cache,
    ILogger<ClaudeClassifier> logger) : ILlmClassifier, IUsageTrackerAware
{
    private const int MaxTokens = 4096;
    private const int BatchSize = 30;
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

        // Batch misses at full LLM efficiency: each batch goes to LLM at target size.
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

        var request = new MessageCreateParams
        {
            Model     = ClaudeModels.Haiku,   // classification always uses Haiku
            MaxTokens = MaxTokens,
            System    = new MessageCreateParamsSystem(ClassificationPrompt.SystemPrompt),
            Tools     = [ClassificationPrompt.Tool],
            ToolChoice = new ToolChoiceTool { Name = ClassificationPrompt.ToolName },
            Messages  =
            [
                new() { Role = Role.User, Content = userMessage },
            ],
        };

        try
        {
            var response = await client.Messages.Create(request, ct);
            if (_usageTracker != null)
                _usageTracker.RecordCall("ClassifyBatch", ClaudeModels.Haiku, response.Usage);

            // Log response for debugging high output token counts (if enabled in appsettings)
            ClassificationResponseLogger.LogResponse(candidates.Count, userMessage.Length, response, response.Usage.OutputTokens);

            return ParseResponse(response.Content, candidates);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            throw new ApiKeyRejectedException(ex);
        }
    }

    private IReadOnlyList<ClassificationResult> ParseResponse(
        IReadOnlyList<ContentBlock> content,
        IReadOnlyList<CardCandidate> candidates)
    {
        ToolUseBlock? toolUse = null;
        foreach (var block in content)
        {
            if (block.TryPickToolUse(out var tu))
            {
                toolUse = tu;
                break;
            }
        }

        if (toolUse is null)
        {
            logger.LogError("No tool use block found in LLM response");
            return [];
        }

        if (!toolUse.Input.TryGetValue("classifications", out var classificationsEl))
        {
            // Log detailed diagnostic info about the malformed response
            var inputJson = toolUse.Input.ToString() ?? "(null)";
            var keyCount = toolUse.Input.Keys.Count();
            logger.LogError(
                "Tool response missing 'classifications' key. Keys count: {KeyCount}, Keys: {Keys}, Full input JSON: {InputJson}",
                keyCount,
                string.Join(", ", toolUse.Input.Keys),
                inputJson);
            return [];
        }

        var dtos = classificationsEl.Deserialize<List<CardClassificationDto>>() ?? [];

        // Diagnostic logging: compare input vs output
        var jsonSize = toolUse.Input.ToString()?.Length ?? 0;
        logger.LogInformation(
            "Classification Response: {InputCount} cards sent, {OutputCount} classifications returned, {JsonSize} bytes",
            candidates.Count,
            dtos.Count,
            jsonSize);

        // Whitelist: only accept oracle IDs that were in the input batch.
        var batchIds = candidates.Select(c => c.Card.OracleId).ToHashSet();
        var cardsByOracleId = candidates.ToDictionary(c => c.Card.OracleId, c => c.Card);

        // Find missing cards (in input but not in output)
        var returnedIds = dtos.Select(d => Guid.TryParse(d.OracleId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var missingCards = candidates
            .Where(c => !returnedIds.Contains(c.Card.OracleId))
            .Select(c => c.Card.Name)
            .ToList();

        if (missingCards.Count > 0)
        {
            logger.LogWarning(
                "Missing {MissingCount} cards from classification response. First 10: {Examples}",
                missingCards.Count,
                string.Join(", ", missingCards.Take(10)));
        }

        var results = new List<ClassificationResult>(dtos.Count);

        foreach (var dto in dtos)
        {
            if (!Guid.TryParse(dto.OracleId, out var id) || !batchIds.Contains(id))
                continue;

            var card = cardsByOracleId[id];
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
