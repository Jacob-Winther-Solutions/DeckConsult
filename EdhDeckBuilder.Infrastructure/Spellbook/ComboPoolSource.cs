using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Infrastructure.Spellbook;

/// <summary>
/// Fetches near-miss combo pieces from Commander Spellbook and converts them into pool candidates.
/// Cards completing more popular combos receive higher inclusion scores.
/// </summary>
public sealed class ComboPoolSource(
    IComboSource comboSource,
    ICardRepository cardRepository,
    ILogger<ComboPoolSource> logger) : IComboCardSource
{
    public async Task<IReadOnlyList<CardCandidate>> GetComboCandidatesAsync(
        IReadOnlyList<Card> commanders,
        IReadOnlyList<Card> lockedCards,
        CancellationToken ct = default)
    {
        var commanderNames = commanders.Select(c => c.Name).ToList();
        var lockedCardNames = lockedCards.Select(c => c.Name).ToList();

        var result = await comboSource.FindCombosAsync(commanderNames, lockedCardNames, ct);

        var almostIncluded = result.AlmostIncluded;
        if (almostIncluded.Count == 0)
        {
            logger.LogInformation("ComboPool: no near-miss combos found");
            return [];
        }

        // Normalize by the most popular combo (floor 1 to avoid division by zero)
        int maxPop = Math.Max(1, almostIncluded.Max(c => c.Popularity));

        // Accumulate per-card scores across all near-miss combos; cap the sum at 1.0
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var combo in almostIncluded)
        {
            double score = Math.Max(0.05, (double)combo.Popularity / maxPop);
            foreach (var name in combo.MissingCardNames)
            {
                scores.TryGetValue(name, out var existing);
                scores[name] = Math.Min(1.0, existing + score);
            }
        }

        if (scores.Count == 0)
        {
            logger.LogInformation("ComboPool: no named missing cards in {Count} near-miss combos (template-only?)",
                almostIncluded.Count);
            return [];
        }

        // Resolve all names in parallel; null returns mean the card isn't in the local Scryfall data
        var resolveTasks = scores.Keys
            .Select(async name =>
            {
                var card = await cardRepository.GetByNameAsync(name, ct);
                return (Name: name, Card: card, Score: scores[name]);
            })
            .ToList();
        await Task.WhenAll(resolveTasks);

        var candidates = resolveTasks
            .Select(t => t.Result)
            .Where(r => r.Card is not null)
            .Select(r => new CardCandidate(r.Card!, r.Score, "Combo Piece"))
            .ToList();

        logger.LogInformation(
            "ComboPool: {CandidateCount} combo piece candidates from {ComboCount} near-miss combos ({SkippedCount} names unresolved)",
            candidates.Count, almostIncluded.Count, scores.Count - candidates.Count);

        return candidates;
    }
}
