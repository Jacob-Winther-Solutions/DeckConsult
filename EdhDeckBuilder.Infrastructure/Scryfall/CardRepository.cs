using System.Text.Json;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
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

    internal CardRepository(ScryfallBulkClient bulkClient, ILogger<CardRepository> logger)
    {
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

    private static async Task<CardIndex> BuildIndexAsync(ScryfallBulkClient bulkClient, ILogger logger)
    {
        var path = await bulkClient.GetOracleCardsFileAsync();
        logger.LogInformation("Building card index from {Path}", path);

        await using var stream = File.OpenRead(path);

        var byName     = new Dictionary<string, Card>(30_000, StringComparer.OrdinalIgnoreCase);
        var byOracleId = new Dictionary<Guid, Card>(30_000);
        var all        = new List<Card>(30_000);

        await foreach (var dto in JsonSerializer.DeserializeAsyncEnumerable<ScryfallCard>(stream, JsonOptions))
        {
            if (dto is null) continue;
            var card = ScryfallMapper.ToCard(dto);
            byName.TryAdd(card.Name, card);
            if (byOracleId.TryAdd(card.OracleId, card))
            {
                all.Add(card);
            }
        }

        logger.LogInformation("Card index built: {Count} cards", byOracleId.Count);
        return new CardIndex(byName, byOracleId, all.AsReadOnly());
    }

    private sealed record CardIndex(
        IReadOnlyDictionary<string, Card> ByName,
        IReadOnlyDictionary<Guid, Card> ByOracleId,
        IReadOnlyList<Card> All);
}
