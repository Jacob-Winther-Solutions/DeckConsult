using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Partnerships;

namespace EdhDeckBuilder.Agent.Discovery;

/// <summary>
/// Discovers a ranked shortlist of commanders matching a strategy via color filter, archetype, theme, and bracket.
/// Surfaces both singleton commanders and partner pairs. For large candidate pools (>150), uses a two-pass algorithm:
/// batch selection then finalists ranking. All partners are presented equally to the LLM for evaluation.
/// CardRepository provides only authoritative partnership data from EDHREC.
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
        if (selector is IUsageTrackerAware aware)
            aware.SetUsageTracker(tracker);
    }

    public async Task<CommanderDiscoveryResult> DiscoverAsync(
        CommanderDiscoveryRequest request,
        Func<DiscoveryProgress, Task>? progress = null,
        CancellationToken ct = default)
    {
        await (progress?.Invoke(new DiscoveryProgress("Gathering candidates")) ?? Task.CompletedTask);

        var (allCandidates, partnerMap) = await GatherCandidatesAsync(request, ct);

        if (allCandidates.Count == 0)
            return new CommanderDiscoveryResult { Suggestions = [] };

        await (progress?.Invoke(new DiscoveryProgress("Ranking commanders", $"{allCandidates.Count} candidates")) ?? Task.CompletedTask);

        var results = await EvaluateAsync(allCandidates, request, ct);

        await (progress?.Invoke(new DiscoveryProgress("Assembling results")) ?? Task.CompletedTask);

        var suggestions = BuildSuggestionsFromResults(results, allCandidates, partnerMap);

        return new CommanderDiscoveryResult { Suggestions = suggestions };
    }

    /// <summary>
    /// Gathers both singleton commanders and partner pair candidates matching the filter.
    /// Singletons come from GetCommandersAsync (already filtered by color identity).
    /// Partners come from GetPartnerCombosAsync (already filtered by combined color identity).
    /// All partnerships in CardRepository are authoritative and need no further validation.
    /// Individual cards are only added if they don't already appear as singletons (avoid duplicates).
    /// Returns the unified candidate list and a map tracking which cards are partnered.
    /// </summary>
    private async Task<(List<Card> Candidates, Dictionary<Guid, PartnerCombo> PartnerMap)> GatherCandidatesAsync(
        CommanderDiscoveryRequest request,
        CancellationToken ct)
    {
        var singleCandidates = await repository.GetCommandersAsync(
            request.ColorFilter,
            request.ExactColorMatch,
            ct);

        var partnerCombos = await repository.GetPartnerCombosAsync(
            request.ColorFilter,
            request.ExactColorMatch,
            ct);

        var allCandidates = new List<Card>(singleCandidates);
        var candidateIds = new HashSet<Guid>(allCandidates.Select(c => c.OracleId));
        var partnerMap = new Dictionary<Guid, PartnerCombo>();

        if (partnerCombos.Count > 0)
        {
            await AddPartnerCandidatesAsync(partnerCombos, allCandidates, candidateIds, partnerMap, ct);
        }

        return (allCandidates, partnerMap);
    }
    /// <summary>
    /// Adds partner pair candidates to the candidate list.
    /// Only adds individual cards if they're not already in singletons (to avoid duplicates when exact matching).
    /// Tracks partnerships in partnerMap for later pairing in results.
    /// </summary>
    private async Task AddPartnerCandidatesAsync(
        IReadOnlyList<PartnerCombo> combos,
        List<Card> candidates,
        HashSet<Guid> candidateIds,
        Dictionary<Guid, PartnerCombo> partnerMap,
        CancellationToken ct)
    {
        foreach (var combo in combos)
        {
            // Only add cards that aren't already in the singles list
            // (e.g., cards that match exact color filter as singles should appear, but cards that
            // only match as partners shouldn't be added individually — they should only appear as pairs)
            var firstId = combo.FirstCardId;
            var secondId = combo.SecondCardId;

            if (!candidateIds.Contains(firstId))
            {
                var first = await repository.GetByOracleIdAsync(firstId, ct);
                if (first != null)
                {
                    candidates.Add(first);
                    candidateIds.Add(firstId);
                }
            }

            if (!candidateIds.Contains(secondId))
            {
                var second = await repository.GetByOracleIdAsync(secondId, ct);
                if (second != null)
                {
                    candidates.Add(second);
                    candidateIds.Add(secondId);
                }
            }

            // Track the partnership so we can group them in results
            partnerMap[firstId] = combo;
            partnerMap[secondId] = combo;
        }
    }

    /// <summary>
    /// Converts LLM ranking results into CommanderSuggestions, grouping partner pairs together.
    /// Ensures each pair appears once (as primary + PartnerCommander), not twice.
    /// </summary>
    private List<CommanderSuggestion> BuildSuggestionsFromResults(
        IReadOnlyList<CommanderSelectionResult> results,
        IReadOnlyList<Card> allCandidates,
        Dictionary<Guid, PartnerCombo> partnerMap)
    {
        var cardMap = allCandidates.ToDictionary(c => c.OracleId);
        var suggestions = new List<CommanderSuggestion>();
        var processedIds = new HashSet<Guid>();

        // Renumber ranks to contiguous 1..N after ordering by the model's rank. Some
        // lightweight models (Gemini Flash Lite) treat rank as a rating scale and emit
        // gaps like 1, 3, 5, 6, 7; the ordering preserves the model's preference but the
        // display is always clean 1, 2, 3, …
        int displayRank = 1;
        foreach (var result in results.OrderBy(r => r.Rank))
        {
            if (processedIds.Contains(result.OracleId))
                continue;

            if (!cardMap.TryGetValue(result.OracleId, out var card))
                continue;

            var normalized = result with { Rank = displayRank };
            var suggestion = BuildSuggestion(normalized, card, cardMap, partnerMap, processedIds);
            suggestions.Add(suggestion);
            displayRank++;
        }

        return suggestions;
    }

    /// <summary>
    /// Builds a single suggestion, handling both singleton and partner pair cases.
    /// Marks both cards in a pair as processed to avoid duplication.
    /// </summary>
    private CommanderSuggestion BuildSuggestion(
        CommanderSelectionResult result,
        Card card,
        IReadOnlyDictionary<Guid, Card> cardMap,
        Dictionary<Guid, PartnerCombo> partnerMap,
        HashSet<Guid> processedIds)
    {
        if (partnerMap.TryGetValue(result.OracleId, out var combo))
        {
            var partnerId = combo.FirstCardId == result.OracleId ? combo.SecondCardId : combo.FirstCardId;
            var partnerCard = cardMap.GetValueOrDefault(partnerId);

            processedIds.Add(result.OracleId);
            processedIds.Add(partnerId);

            return new CommanderSuggestion
            {
                Commander = card,
                PartnerCommander = partnerCard,
                Rank = result.Rank,
                Rationale = result.Rationale,
            };
        }

        processedIds.Add(result.OracleId);

        return new CommanderSuggestion
        {
            Commander = card,
            Rank = result.Rank,
            Rationale = result.Rationale,
        };
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
