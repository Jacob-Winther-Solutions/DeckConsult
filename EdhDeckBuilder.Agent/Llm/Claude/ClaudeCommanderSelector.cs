using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Core.Cards;
using System.Text.Json;

namespace EdhDeckBuilder.Agent.Llm.Claude;

/// <summary>
/// Ranks candidate commanders using the user's selected Claude model
/// (moderate temperature for creative judgment). Uses a forced tool call so output is
/// always structured JSON. Supports usage tracking for token accounting.
/// </summary>
public sealed class ClaudeCommanderSelector(IClaudeClientFactory factory) : ICommanderSelector, IUsageTrackerAware
{
    private const int MaxTokens = 2048;
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

        var systemMessage = CommanderSelectionPrompt.SystemPrompt;

        var requestMsg = new MessageCreateParams
        {
            Model      = factory.SelectionModel,
            MaxTokens  = MaxTokens,
            System     = new MessageCreateParamsSystem(systemMessage),
            Tools      = [CommanderSelectionPrompt.Tool],
            ToolChoice = new ToolChoiceTool { Name = CommanderSelectionPrompt.ToolName },
            Messages   =
            [
                new() { Role = Role.User, Content = CommanderSelectionPrompt.FormatUserMessage(candidates, request) },
            ],
        };

        try
        {
            var response = await client.Messages.Create(requestMsg, ct);
            if (_usageTracker != null)
            {
                var candidateCount = candidates.Count;
                _usageTracker.RecordCall($"Commander-Selection-{candidateCount}", factory.SelectionModel, response.Usage);
            }
            return ParseResponse(response.Content, candidates);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            throw new ApiKeyRejectedException(ex);
        }
    }

    private static IReadOnlyList<CommanderSelectionResult> ParseResponse(
        IReadOnlyList<ContentBlock> content,
        IReadOnlyList<Card> candidates)
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

        if (!toolUse.Input.TryGetValue("rankings", out var rankingsEl))
            return [];

        var dtos = rankingsEl.Deserialize<List<CommanderRankingDto>>() ?? [];

        // Whitelist: only accept oracle IDs that were in the input batch.
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
}
