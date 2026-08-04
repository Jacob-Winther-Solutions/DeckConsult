using EdhDeckBuilder.Agent.Analysis;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Interfaces;

public interface IDeckAnalyzer
{
    UsageTracker? UsageTracker { get; set; }

    Task<DeckAnalysisResult> AnalyzeAsync(
        IReadOnlyList<Card> commanders,
        IReadOnlyList<ParsedCardEntry> entries,
        Func<string, Task>? progress = null,
        Func<string, Task>? subProgress = null,
        CancellationToken ct = default);
}
