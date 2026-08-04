using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Decks;
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

        var combosTask     = comboSource.FindCombosAsync(commanderNames, cardNames, ct);
        var bracketTagTask = comboSource.EstimateBracketTagAsync(commanderNames, cardNames, ct);
        await Task.WhenAll(combosTask, bracketTagTask);

        var combos     = combosTask.Result;
        var bracketTag = bracketTagTask.Result;
        var bracket    = MapBracketTag(bracketTag);

        logger.LogInformation(
            "ComboFinder_Complete: included={Included}, nearMiss={NearMiss}, spellbookBracket={Tag}",
            combos.Included.Count, combos.AlmostIncluded.Count, bracketTag);

        return new ComboAnalysisResult
        {
            Combos              = combos,
            SpellbookBracketTag = bracketTag,
            SpellbookBracket    = bracket,
        };
    }

    private static Bracket? MapBracketTag(string? tag) => tag switch
    {
        "S" => Bracket.Five,
        "R" => Bracket.Four,
        "E" => Bracket.Three,
        "P" => Bracket.Two,
        "C" => Bracket.One,
        _   => null,
    };
}
