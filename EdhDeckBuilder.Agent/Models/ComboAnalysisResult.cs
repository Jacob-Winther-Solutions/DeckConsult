using EdhDeckBuilder.Core.Abstractions;

namespace EdhDeckBuilder.Agent.Models;

public sealed record ComboAnalysisResult
{
    public required ComboSearchResult Combos { get; init; }
}
