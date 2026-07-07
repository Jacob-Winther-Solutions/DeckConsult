using System.Text.Json;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Partnerships;
using EdhDeckBuilder.Infrastructure.Edhrec;
using EdhDeckBuilder.Infrastructure.Edhrec.Dto;
using EdhDeckBuilder.Infrastructure.Scryfall.Dto;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Infrastructure.Scryfall;

public sealed class CardRepository : ICardRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Lazy<Task<CardIndex>> _index;
    private readonly IEdhrecClient? _edhrecClient;
    private readonly ILogger<CardRepository> _logger;

    internal CardRepository(
        ScryfallBulkClient bulkClient,
        ILogger<CardRepository> logger)
    {
        _logger = logger;
        _edhrecClient = null;
        _index = new Lazy<Task<CardIndex>>(() => BuildIndexAsync(bulkClient, logger));
    }

    internal CardRepository(
        ScryfallBulkClient bulkClient,
        IEdhrecClient edhrecClient,
        ILogger<CardRepository> logger)
    {
        _logger = logger;
        _edhrecClient = edhrecClient;
        _index = new Lazy<Task<CardIndex>>(() => BuildIndexAsync(bulkClient, logger));
    }

    public async Task<Card?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var index = await _index.Value;
        return index.ByName.GetValueOrDefault(name.Trim(), null);
    }

    public async Task<Card?> GetByOracleIdAsync(Guid oracleId, CancellationToken ct = default)
    {
        var index = await _index.Value;
        return index.ByOracleId.GetValueOrDefault(oracleId);
    }

    public async Task<Card?> GetByScryfallIdAsync(Guid scryfallId, CancellationToken ct = default)
    {
        var index = await _index.Value;
        return index.ByScryfallId.GetValueOrDefault(scryfallId);
    }

    public async Task<IReadOnlyList<Card>> SearchAsync(string query, CancellationToken ct = default)
    {
        var index = await _index.Value;
        var q = query.Trim();
        return [.. index.ByName.Keys
            .Where(k => k.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Select(k => index.ByName[k])
            .OrderBy(c => c.Name)];
    }

    public async Task<IReadOnlyList<Card>> GetCommandersAsync(
        Color? colorFilter = null,
        bool exactMatch = false,
        CancellationToken ct = default)
    {
        var index = await _index.Value;
        return index.All
            .Where(c => c.CanBeCommander)
            .Where(c => colorFilter is null
                || (exactMatch ? c.ColorIdentity == colorFilter.Value
                               : c.ColorIdentity.IsWithin(colorFilter.Value)))
            .ToList();
    }

    public async Task<IReadOnlyList<PartnerCombo>> GetPartnerCombosAsync(
        Color? colorFilter = null,
        bool exactMatch = false,
        CancellationToken ct = default)
    {
        var index = await _index.Value;
        return index.PartnerCombos
            .Where(pc => colorFilter is null
                || IsValidColorIdentity(pc, colorFilter.Value, exactMatch, index))
            .ToList();
    }

    private async Task<CardIndex> BuildIndexAsync(ScryfallBulkClient bulkClient, ILogger logger)
    {
        var path = await bulkClient.GetOracleCardsFileAsync();
        logger.LogInformation("Building card index from {Path}", path);

        await using var stream = File.OpenRead(path);

        var byName       = new Dictionary<string, Card>(30_000, StringComparer.OrdinalIgnoreCase);
        var byOracleId   = new Dictionary<Guid, Card>(30_000);
        var byScryfallId = new Dictionary<Guid, Card>(30_000);
        var all          = new List<Card>(30_000);

        await foreach (var dto in JsonSerializer.DeserializeAsyncEnumerable<ScryfallCard>(stream, JsonOptions))
        {
            if (dto is null) continue;
            var card = ScryfallMapper.ToCard(dto);
            byName.TryAdd(card.Name, card);
            byScryfallId.TryAdd(card.ScryfallId, card);
            if (byOracleId.TryAdd(card.OracleId, card))
            {
                all.Add(card);
            }
        }

        logger.LogInformation("Card index built: {Count} cards", byOracleId.Count);

        // Build partnership index from EDHREC data
        List<PartnerCombo> combos;
        if (_edhrecClient != null)
        {
            var edhrecPage = await _edhrecClient.GetPartnersPageAsync();
            combos = PartnershipIndexBuilder.BuildFromEdhrec(edhrecPage, byName, logger);
        }
        else
        {
            logger.LogWarning("No EDHREC client provided; partnership index will be empty");
            combos = new List<PartnerCombo>();
        }

        return new CardIndex(byName, byOracleId, byScryfallId, all.AsReadOnly(), combos);
    }


    /// <summary>
    /// Checks if a partner combo's combined color identity satisfies the filter.
    /// </summary>
    private static bool IsValidColorIdentity(
        PartnerCombo combo,
        Color filter,
        bool exactMatch,
        CardIndex index)
    {
        if (!index.ByOracleId.TryGetValue(combo.FirstCardId, out var first)
            || !index.ByOracleId.TryGetValue(combo.SecondCardId, out var second))
            return false;

        var combined = first.ColorIdentity | second.ColorIdentity;

        return exactMatch
            ? combined == filter
            : combined.IsWithin(filter);
    }

    private sealed record CardIndex(
        IReadOnlyDictionary<string, Card> ByName,
        IReadOnlyDictionary<Guid, Card> ByOracleId,
        IReadOnlyDictionary<Guid, Card> ByScryfallId,
        IReadOnlyList<Card> All,
        IReadOnlyList<PartnerCombo> PartnerCombos);
}
