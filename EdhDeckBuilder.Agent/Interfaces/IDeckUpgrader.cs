using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Agent.Interfaces;

public interface IDeckUpgrader
{
    UsageTracker? UsageTracker { get; set; }

    Task<DeckUpgradeResult> UpgradeAsync(
        DeckAnalysisResult analysis,
        string? userFeedback,
        decimal? maxCardPriceUsd,
        IReadOnlyDictionary<CardRole, RoleTarget>? customTargets = null,
        Func<string, Task>? progress = null,
        CancellationToken ct = default);
}
