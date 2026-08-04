using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Models;

namespace EdhDeckBuilder.Agent.Interfaces;

public interface IDeckUpgrader
{
    UsageTracker? UsageTracker { get; set; }

    Task<DeckUpgradeResult> UpgradeAsync(
        DeckAnalysisResult analysis,
        string? userFeedback,
        decimal? maxCardPriceUsd,
        Func<string, Task>? progress = null,
        CancellationToken ct = default);
}
