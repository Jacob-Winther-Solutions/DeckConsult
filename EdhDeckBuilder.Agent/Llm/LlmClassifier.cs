using Anthropic;
using Anthropic.Models.Messages;
using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Core.Cards;
using System.Text.Json;

namespace EdhDeckBuilder.Agent.Llm;

/// <summary>
/// Classifies a batch of card candidates using Claude Haiku (low temperature for determinism).
/// Uses a forced tool call so output is always structured JSON that matches the schema.
/// Results for non-commander-dependent roles (anything except Plan and Synergy) are cached in
/// <see cref="ClassificationCache"/> across multiple builds in the same session.
/// </summary>
public sealed class LlmClassifier(AnthropicClient client, ClassificationCache cache) : ILlmClassifier
{
    private const string Model = "claude-haiku-4-5-20251001";
    private const int MaxTokens = 4096;

    public async Task<IReadOnlyList<ClassificationResult>> ClassifyBatchAsync(
        IReadOnlyList<CardCandidate> candidates,
        IReadOnlyList<Card> commanders,
        CancellationToken ct = default)
    {
        cache.Partition(candidates, out var hits, out var misses);

        if (misses.Count == 0)
            return hits;

        var fresh = await CallLlmAsync(misses, commanders, ct);
        cache.Store(fresh);

        return [.. hits, .. fresh];
    }

    private async Task<IReadOnlyList<ClassificationResult>> CallLlmAsync(
        IReadOnlyList<CardCandidate> candidates,
        IReadOnlyList<Card> commanders,
        CancellationToken ct)
    {
        var request = new MessageCreateParams
        {
            Model = Model,
            MaxTokens = MaxTokens,
            System = new MessageCreateParamsSystem(ClassificationPrompt.SystemPrompt),
            Tools = [ClassificationPrompt.Tool],
            ToolChoice = new ToolChoiceTool { Name = ClassificationPrompt.ToolName },
            Messages =
            [
                new() { Role = Role.User, Content = ClassificationPrompt.FormatUserMessage(candidates, commanders) },
            ],
        };

        var response = await client.Messages.Create(request, ct);
        return ParseResponse(response.Content, candidates);
    }

    private static IReadOnlyList<ClassificationResult> ParseResponse(
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
            return [];

        if (!toolUse.Input.TryGetValue("classifications", out var classificationsEl))
            return [];

        var dtos = classificationsEl.Deserialize<List<CardClassificationDto>>() ?? [];

        // Whitelist: only accept oracle IDs that were in the input batch.
        var batchIds = candidates.Select(c => c.Card.OracleId).ToHashSet();
        var cardTypeById = candidates.ToDictionary(c => c.Card.OracleId, c => c.Card.Types);
        var results = new List<ClassificationResult>(dtos.Count);

        foreach (var dto in dtos)
        {
            if (!Guid.TryParse(dto.OracleId, out var id) || !batchIds.Contains(id))
                continue;

            var raw = new ClassificationResult
            {
                OracleId = id,
                PrimaryRole = ParseRole(dto.PrimaryRole),
                Secondary = dto.Secondary
                    .Select(s => new RoleContribution(
                        ParseRole(s.Role),
                        ParseRelation(s.Relation),
                        Math.Clamp(s.Weight, 0.0, 1.0)))
                    .ToArray(),
                LandCredit = Math.Clamp(dto.LandCredit, 0.0, 1.0),
            };

            results.Add(ClassificationSanitizer.SanitizeLandRole(raw, cardTypeById[id]));
        }

        return results;
    }

    private static CardRole ParseRole(string s) =>
        Enum.TryParse<CardRole>(s, ignoreCase: true, out var r) ? r : CardRole.Synergy;

    private static RoleRelation ParseRelation(string s) =>
        Enum.TryParse<RoleRelation>(s, ignoreCase: true, out var r) ? r : RoleRelation.Always;
}
