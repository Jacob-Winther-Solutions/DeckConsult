using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Tests.Agent.Shared;

/// <summary>
/// Test double for <see cref="ISuggestionSource"/> that returns empty/null for every method.
/// Use in tests that construct <c>CommanderDiscovery</c> but don't exercise EDHREC behaviour.
/// </summary>
internal sealed class NoOpSuggestionSource : ISuggestionSource
{
    public Task<IReadOnlyList<CardCandidate>> GetRecommendationsAsync(Card _, CancellationToken __)
        => Task.FromResult<IReadOnlyList<CardCandidate>>([]);

    public Task<IReadOnlyList<CardCandidate>> GetAverageDeckAsync(Card _, CancellationToken __)
        => Task.FromResult<IReadOnlyList<CardCandidate>>([]);

    public Task<Dictionary<string, int>> GetPartnerPopularityAsync(CancellationToken _)
        => Task.FromResult(new Dictionary<string, int>());

    public Task<IReadOnlyList<(string FirstCardName, string SecondCardName)>> GetPartnerWithPairsAsync(CancellationToken _)
        => Task.FromResult<IReadOnlyList<(string, string)>>([]);

    public Task<IReadOnlyList<CardCandidate>?> GetPartnerPairRecommendationsAsync(Card _, Card __, CancellationToken ___)
        => Task.FromResult<IReadOnlyList<CardCandidate>?>(null);

    public Task<IReadOnlyList<CardCandidate>?> GetCommanderThemeRecommendationsAsync(Card _, WeightedTheme __, CancellationToken ___)
        => Task.FromResult<IReadOnlyList<CardCandidate>?>(null);

    public Task<(IReadOnlyList<CardCandidate> Cards, IReadOnlyList<Card> Commanders)?> GetTagsAsync(WeightedTheme _, CancellationToken __)
        => Task.FromResult<(IReadOnlyList<CardCandidate>, IReadOnlyList<Card>)?>(null);

    public Task<IReadOnlyList<(string Slug, string Name, int Count, Theme? KnownTheme, Archetype? KnownArchetype)>> GetPopularThemesAsync(Card _, CancellationToken __)
        => Task.FromResult<IReadOnlyList<(string, string, int, Theme?, Archetype?)>>([]);
}
