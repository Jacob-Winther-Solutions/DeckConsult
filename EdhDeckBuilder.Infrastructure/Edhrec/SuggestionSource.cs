using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
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
}
