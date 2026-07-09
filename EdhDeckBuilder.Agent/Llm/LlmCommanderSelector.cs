using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm.Shared;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Core.Cards;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Agent.Llm;

/// <summary>
/// Ranks legendary-creature candidates for Commander Discovery using the user's selected model
/// (moderate temperature). Provider-agnostic — the factory selects the right model and ceiling.
/// </summary>
public sealed class LlmCommanderSelector(
    ILlmClientFactory factory,
    ILogger<LlmCommanderSelector> logger) : ICommanderSelector, IUsageTrackerAware
{
    private const double Temperature = 0.6;

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
        var model  = factory.SelectedModel;

        var llmRequest = new LlmRequest
        {
            Model          = model,
            MaxTokens      = factory.SelectorMaxOutputTokens,
            Temperature    = Temperature,
            SystemPrompt   = CommanderSelectionPrompt.SystemPrompt,
            Messages       = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = CommanderSelectionPrompt.FormatUserMessage(candidates, request) }] }],
            Tools          = [CommanderSelectionPrompt.ToolDefinition],
            ForcedToolName = CommanderSelectionPrompt.ToolName,
            EnableCaching  = true,
        };

        var response = await client.SendAsync(llmRequest, ct);

        if (_usageTracker is not null)
            _usageTracker.RecordCall(
                $"Commander-Selection-{candidates.Count}", model,
                response.Usage.InputTokens,
                response.Usage.OutputTokens,
                response.Usage.CacheCreationInputTokens ?? 0,
                response.Usage.CacheReadInputTokens     ?? 0);

        return ParseResponse(response, candidates);
    }

    private IReadOnlyList<CommanderSelectionResult> ParseResponse(
        LlmResponse response,
        IReadOnlyList<Card> candidates)
    {
        var toolUse = response.Content.OfType<LlmToolUseBlock>().FirstOrDefault();

        if (toolUse is null)
        {
            logger.LogError("No tool use block in LLM commander selection response");
            return [];
        }

        var rankingsNode = toolUse.Input["rankings"];
        if (rankingsNode is null)
            return [];

        // Guard against the model returning the array as a JSON-encoded string.
        if (rankingsNode is JsonValue sv && sv.TryGetValue<string>(out var embedded))
            rankingsNode = TryParseEmbedded(embedded);

        if (rankingsNode is null)
            return [];

        List<CommanderRankingDto> dtos;
        try
        {
            dtos = rankingsNode.Deserialize<List<CommanderRankingDto>>() ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning("LLM commander ranking response partially malformed; returning empty. {Error}", ex.Message);
            return [];
        }

        var batchIds = candidates.Select(c => c.OracleId).ToHashSet();
        var results  = new List<CommanderSelectionResult>(dtos.Count);

        foreach (var dto in dtos)
        {
            if (!Guid.TryParse(dto.OracleId, out var id) || !batchIds.Contains(id))
                continue;

            results.Add(new CommanderSelectionResult(id, dto.Rank, dto.Rationale));
        }

        return results;
    }

    private JsonNode? TryParseEmbedded(string json)
    {
        try { return JsonNode.Parse(json); }
        catch (JsonException) { }

        var lastClose = json.LastIndexOf("},", StringComparison.Ordinal);
        if (lastClose >= 0)
        {
            try { return JsonNode.Parse(json[..(lastClose + 1)] + "]"); }
            catch (JsonException) { }
        }

        logger.LogWarning("LLM returned rankings as an unparseable string; returning empty.");
        return null;
    }
}
