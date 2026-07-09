using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm.Shared;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Agent.Selection;
using EdhDeckBuilder.Core.Cards;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Agent.Llm;

/// <summary>
/// Ranks candidate cards for a specific role using the user's selected model
/// (moderate temperature for creative judgment). Provider-agnostic — the factory
/// selects the right model and token ceiling.
/// </summary>
public sealed class LlmSelector(
    ILlmClientFactory factory,
    ILogger<LlmSelector> logger) : ICardSelector, IUsageTrackerAware
{
    private const double Temperature = 0.6;

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
        var model  = factory.SelectedModel;

        var request = new LlmRequest
        {
            Model          = model,
            MaxTokens      = factory.SelectorMaxOutputTokens,
            Temperature    = Temperature,
            SystemPrompt   = SelectionPrompt.SystemPrompt,
            Messages       = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = SelectionPrompt.FormatUserMessage(role, candidates, context, state) }] }],
            Tools          = [SelectionPrompt.ToolDefinition],
            ForcedToolName = SelectionPrompt.ToolName,
            EnableCaching  = true,
        };

        var response = await client.SendAsync(request, ct);

        if (_usageTracker is not null)
            _usageTracker.RecordCall(
                $"Select-{role}", model,
                response.Usage.InputTokens,
                response.Usage.OutputTokens,
                response.Usage.CacheCreationInputTokens ?? 0,
                response.Usage.CacheReadInputTokens     ?? 0);

        return ParseResponse(response, candidates);
    }

    private IReadOnlyList<SelectionResult> ParseResponse(
        LlmResponse response,
        IReadOnlyList<FillCandidate> candidates)
    {
        var toolUse = response.Content.OfType<LlmToolUseBlock>().FirstOrDefault();

        if (toolUse is null)
        {
            logger.LogError("No tool use block in LLM selection response");
            return [];
        }

        var selectionsNode = toolUse.Input["selections"];
        if (selectionsNode is null)
            return [];

        // Sonnet 5 occasionally returns the array as a JSON-encoded string rather than an
        // inline array. Parse the embedded string back to a node before deserializing.
        if (selectionsNode is JsonValue sv && sv.TryGetValue<string>(out var embedded))
            selectionsNode = TryParseEmbedded(embedded);

        if (selectionsNode is null)
            return [];

        List<CardSelectionDto> dtos;
        try
        {
            dtos = selectionsNode.Deserialize<List<CardSelectionDto>>() ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning("LLM selection response partially malformed; skipping this role. {Error}", ex.Message);
            return [];
        }

        var batchIds = candidates.Select(c => c.Card.OracleId).ToHashSet();
        var results  = new List<SelectionResult>(dtos.Count);

        foreach (var dto in dtos)
        {
            if (!Guid.TryParse(dto.OracleId, out var id) || !batchIds.Contains(id))
                continue;

            results.Add(new SelectionResult
            {
                OracleId  = id,
                Rank      = dto.Rank,
                Rationale = dto.Rationale,
            });
        }

        return results;
    }

    // Try to parse a JSON-string-wrapped array. If the string itself is malformed
    // (e.g. a truncated final entry), attempt partial recovery by closing the array
    // after the last complete object.
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

        logger.LogWarning("LLM returned selections as an unparseable string; skipping role.");
        return null;
    }
}
