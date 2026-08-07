using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Infrastructure.Edhrec;

public sealed class SuggestionSource : ISuggestionSource
{
    private readonly EdhrecClient _client;
    private readonly ICardRepository _repository;
    private readonly ILogger<SuggestionSource> _logger;

    internal SuggestionSource(EdhrecClient client, ICardRepository repository, ILogger<SuggestionSource> logger)
    {
        _client = client;
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CardCandidate>> GetRecommendationsAsync(Card commander, CancellationToken ct = default)
    {
        var slug = EdhrecSlugger.FromCard(commander);
        var page = await _client.GetCommanderPageAsync(slug, ct);
        if (page?.Container?.JsonDict?.Cardlists is not { Count: > 0 } cardlists)
        {
            _logger.LogWarning("No recommendation data from EDHREC for {Commander}", commander.Name);
            return [];
        }
        return await EdhrecMapper.ToCardCandidatesAsync(cardlists, _repository, _logger, ct);
    }

    public async Task<IReadOnlyList<CardCandidate>> GetAverageDeckAsync(Card commander, CancellationToken ct = default)
    {
        var slug = EdhrecSlugger.FromCard(commander);
        var page = await _client.GetAverageDeckPageAsync(slug, ct);
        if (page?.Container?.JsonDict?.Cardlists is not { Count: > 0 } cardlists)
        {
            _logger.LogWarning("No average deck data from EDHREC for {Commander}", commander.Name);
            return [];
        }
        return await EdhrecMapper.ToCardCandidatesAsync(cardlists, _repository, _logger, ct);
    }

    public async Task<Dictionary<string, int>> GetPartnerPopularityAsync(CancellationToken ct = default)
    {
        var page = await _client.GetPartnersPageAsync(ct);
        return EdhrecPartnerMapper.ExtractPartnerPopularity(page, _logger);
    }

    public async Task<IReadOnlyList<(string FirstCardName, string SecondCardName)>> GetPartnerWithPairsAsync(CancellationToken ct = default)
    {
        var page = await _client.GetPartnersPageAsync(ct);
        return EdhrecPartnerMapper.ExtractPartnerWithPairs(page, _logger);
    }

    public async Task<IReadOnlyList<CardCandidate>?> GetPartnerPairRecommendationsAsync(Card first, Card second, CancellationToken ct = default)
    {
        var firstSlug = EdhrecSlugger.FromCard(first);
        var secondSlug = EdhrecSlugger.FromCard(second);
        var page = await _client.GetPartnerPairRecommendationsAsync(firstSlug, secondSlug, ct);
        if (page?.Container?.JsonDict?.Cardlists is not { Count: > 0 } cardlists)
        {
            _logger.LogWarning("No partner-pair recommendation data from EDHREC for {First} + {Second}",
                first.Name, second.Name);
            return null;
        }
        return await EdhrecMapper.ToCardCandidatesAsync(cardlists, _repository, _logger, ct);
    }

    public async Task<IReadOnlyList<CardCandidate>?> GetCommanderThemeRecommendationsAsync(
        Card commander, WeightedTheme theme, CancellationToken ct = default)
    {
        var themeSlug = EdhrecThemeSlugger.GetSlug(theme);
        if (themeSlug is null) return null;

        var commanderSlug = EdhrecSlugger.FromCard(commander);
        var page = await _client.GetCommanderThemePageAsync(commanderSlug, themeSlug, ct);
        if (page?.Container?.JsonDict?.Cardlists is not { Count: > 0 } cardlists)
            return null;

        return await EdhrecMapper.ToCardCandidatesAsync(cardlists, _repository, _logger, ct);
    }

    public async Task<IReadOnlyList<(string Slug, string Name, int Count, Core.Decks.Theme? KnownTheme, Core.Decks.Archetype? KnownArchetype)>> GetPopularThemesAsync(
        Card commander, CancellationToken ct = default)
    {
        var slug = EdhrecSlugger.FromCard(commander);
        var page = await _client.GetCommanderPageAsync(slug, ct);
        if (page?.Panels?.TagLinks is not { Count: > 0 } tagLinks)
            return [];
        return tagLinks
            .OrderByDescending(t => t.Count)
            .Select(t => (t.Slug, t.Value, t.Count,
                EdhrecThemeSlugger.TryGetTheme(t.Slug),
                EdhrecThemeSlugger.TryGetArchetype(t.Slug)))
            .ToList();
    }

    public async Task<(IReadOnlyList<CardCandidate> Cards, IReadOnlyList<Card> Commanders)?> GetTagsAsync(
        WeightedTheme theme, CancellationToken ct = default)
    {
        var themeSlug = EdhrecThemeSlugger.GetSlug(theme);
        if (themeSlug is null) return null;

        var page = await _client.GetTagsPageAsync(themeSlug, ct);
        if (page?.Container?.JsonDict?.Cardlists is not { Count: > 0 } cardlists)
            return null;

        var commanderLists = cardlists
            .Where(cl => cl.Header.Contains("Commander", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var cardLists = cardlists
            .Where(cl => !cl.Header.Contains("Commander", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var cards = await EdhrecMapper.ToCardCandidatesAsync(cardLists, _repository, _logger, ct);

        var commanders = new List<Card>();
        var seenIds = new HashSet<Guid>();
        foreach (var list in commanderLists)
        foreach (var view in list.Cardviews)
        {
            Card? card = null;
            if (!string.IsNullOrEmpty(view.Id) && Guid.TryParse(view.Id, out var scryfallId))
                card = await _repository.GetByScryfallIdAsync(scryfallId, ct);
            card ??= await _repository.GetByNameAsync(view.Name, ct);
            if (card is not null && seenIds.Add(card.OracleId))
                commanders.Add(card);
        }

        return (cards, commanders);
    }
}
