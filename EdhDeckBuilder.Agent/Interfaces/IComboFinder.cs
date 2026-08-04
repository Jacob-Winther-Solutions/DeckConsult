using EdhDeckBuilder.Agent.Models;

namespace EdhDeckBuilder.Agent.Interfaces;

public interface IComboFinder
{
    Task<ComboAnalysisResult> FindCombosAsync(
        DeckAnalysisResult analysis,
        CancellationToken ct = default);
}
