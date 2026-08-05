using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Agent.Analysis;

public sealed class ComboFinder(
    IComboSource comboSource,
    ILogger<ComboFinder> logger) : IComboFinder
{
    public async Task<ComboAnalysisResult> FindCombosAsync(
        DeckAnalysisResult analysis,
        CancellationToken ct = default)
    {
        var commanderNames = analysis.Commanders.Select(c => c.Name).ToList();
        var cardNames      = analysis.Cards.Select(c => c.Card.Name).ToList();

        logger.LogInformation("ComboFinder_Start: commanders={Commanders}, cards={CardCount}",
            string.Join(", ", commanderNames), cardNames.Count);

        var combos = await comboSource.FindCombosAsync(commanderNames, cardNames, ct);

        logger.LogInformation("ComboFinder_Complete: included={Included}, nearMiss={NearMiss}",
            combos.Included.Count, combos.AlmostIncluded.Count);

        return new ComboAnalysisResult { Combos = combos };
    }
}
