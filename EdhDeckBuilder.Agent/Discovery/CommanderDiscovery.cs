using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Llm;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Discovery;

/// <summary>
/// Discovers a ranked shortlist of commanders matching a strategy via color filter, archetype, theme, and bracket.
/// For large candidate pools (>150), uses a two-pass algorithm: batch selection then finalists ranking.
/// Supports usage tracking for token accounting.
/// </summary>
public sealed class CommanderDiscovery(
    ICardRepository repository,
    ICommanderSelector selector) : ICommanderDiscovery
{
    private const int SingleBatchLimit = 150;
    private const int BatchSize = 50;
    private const int TopPerBatch = 5;
    private UsageTracker? _usageTracker;

    public void SetUsageTracker(UsageTracker tracker)
    {
        _usageTracker = tracker;
        if (selector is LlmCommanderSelector llmSelector)
            llmSelector.SetUsageTracker(tracker);
    }

    public async Task<CommanderDiscoveryResult> DiscoverAsync(
        CommanderDiscoveryRequest request,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("Gathering commander candidates...");

        var candidates = await repository.GetCommandersAsync(
            request.ColorFilter,
            request.ExactColorMatch,
            ct);

        if (candidates.Count == 0)
            return new CommanderDiscoveryResult { Suggestions = [] };

        progress?.Report($"Evaluating {candidates.Count} commanders...");

        var results = await EvaluateAsync(candidates, request, ct);

        // Map results back to Card objects, build suggestions, order by rank
        var cardMap = candidates.ToDictionary(c => c.OracleId);
        var suggestions = new List<CommanderSuggestion>();

        foreach (var result in results.OrderBy(r => r.Rank))
        {
            if (cardMap.TryGetValue(result.OracleId, out var card))
            {
                suggestions.Add(new CommanderSuggestion
                {
                    Commander = card,
                    Rank = result.Rank,
                    Rationale = result.Rationale,
                });
            }
        }

        return new CommanderDiscoveryResult { Suggestions = suggestions };
    }

    private async Task<IReadOnlyList<CommanderSelectionResult>> EvaluateAsync(
        IReadOnlyList<Card> candidates,
        CommanderDiscoveryRequest request,
        CancellationToken ct = default)
    {
        if (candidates.Count <= SingleBatchLimit)
        {
            return await selector.SelectAsync(candidates, request, ct);
        }

        // Two-pass: batch, collect top 5 per batch, then final ranking on finalists
        var finalists = new List<Card>();

        for (int i = 0; i < candidates.Count; i += BatchSize)
        {
            var batch = candidates.Skip(i).Take(BatchSize).ToList();
            var batchResults = await selector.SelectAsync(batch, request, ct);

            // Keep top TopPerBatch from this batch by rank
            var topFromBatch = batchResults
                .OrderBy(r => r.Rank)
                .Take(TopPerBatch)
                .Select(r => candidates.First(c => c.OracleId == r.OracleId))
                .ToList();

            finalists.AddRange(topFromBatch);
        }

        // Final ranking on all finalists
        return await selector.SelectAsync(finalists, request, ct);
    }
}
