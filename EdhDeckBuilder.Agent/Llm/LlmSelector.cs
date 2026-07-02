using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Agent.Selection;
using EdhDeckBuilder.Core.Cards;
using System.Text.Json;

namespace EdhDeckBuilder.Agent.Llm;

/// <summary>
/// Ranks candidate cards for a specific role using the user's selected Claude model
/// (moderate temperature for creative judgment). Uses a forced tool call so output is
/// always structured JSON. Selection is always context-dependent, so results are never cached.
/// </summary>
public sealed class LlmSelector(IClaudeClientFactory factory) : ICardSelector
{
    private const int MaxTokens = 2048;
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

        var systemMessage = SelectionPrompt.SystemPrompt;

        var request = new MessageCreateParams
        {
            Model      = factory.SelectionModel,
            MaxTokens  = MaxTokens,
            System     = new MessageCreateParamsSystem(systemMessage),
            Tools      = [SelectionPrompt.Tool],
            ToolChoice = new ToolChoiceTool { Name = SelectionPrompt.ToolName },
            Messages   =
            [
                new() { Role = Role.User, Content = SelectionPrompt.FormatUserMessage(role, candidates, context, state) },
            ],
        };

        try
        {
            var response = await client.Messages.Create(request, ct);
            if (_usageTracker != null)
                _usageTracker.RecordCall($"Select-{role}", factory.SelectionModel, response.Usage);
            return ParseResponse(response.Content, candidates);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            throw new ApiKeyRejectedException(ex);
        }
    }

    private static IReadOnlyList<SelectionResult> ParseResponse(
        IReadOnlyList<ContentBlock> content,
        IReadOnlyList<FillCandidate> candidates)
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

        if (!toolUse.Input.TryGetValue("selections", out var selectionsEl))
            return [];

        var dtos = selectionsEl.Deserialize<List<CardSelectionDto>>() ?? [];

        // Whitelist: only accept oracle IDs that were in the input batch.
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
}
