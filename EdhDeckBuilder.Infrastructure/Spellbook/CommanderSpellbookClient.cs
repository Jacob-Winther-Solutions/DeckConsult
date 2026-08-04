using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Infrastructure.Spellbook.Dto;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Infrastructure.Spellbook;

public sealed class CommanderSpellbookClient(
    HttpClient httpClient,
    ILogger<CommanderSpellbookClient> logger) : IComboSource
{
    private const string FindMyCombosPath  = "find-my-combos/";
    private const string EstimateBracketPath = "estimate-bracket/";

    private readonly ConcurrentDictionary<string, ComboSearchResult> _comboCache   = new();
    private readonly ConcurrentDictionary<string, string?>            _bracketCache = new();

    public async Task<ComboSearchResult> FindCombosAsync(
        IReadOnlyList<string> commanderNames,
        IReadOnlyList<string> cardNames,
        CancellationToken ct = default)
    {
        var cacheKey = ComputeCacheKey("c", commanderNames, cardNames);
        if (_comboCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var request = BuildRequest(commanderNames, cardNames);
        using var response = await httpClient.PostAsJsonAsync(FindMyCombosPath, request, ct);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<FindMyCombosResponse>(cancellationToken: ct);

        var deckCards = new HashSet<string>(
            commanderNames.Concat(cardNames), StringComparer.OrdinalIgnoreCase);

        var result = new ComboSearchResult
        {
            Included      = Map(dto?.Results?.Included, deckCards),
            AlmostIncluded = Map(dto?.Results?.AlmostIncluded, deckCards),
        };

        _comboCache.TryAdd(cacheKey, result);
        logger.LogInformation("Spellbook_FindCombos: {Included} included, {NearMiss} near-miss",
            result.Included.Count, result.AlmostIncluded.Count);
        return result;
    }

    public async Task<string?> EstimateBracketTagAsync(
        IReadOnlyList<string> commanderNames,
        IReadOnlyList<string> cardNames,
        CancellationToken ct = default)
    {
        var cacheKey = ComputeCacheKey("b", commanderNames, cardNames);
        if (_bracketCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var request = BuildRequest(commanderNames, cardNames);
        using var response = await httpClient.PostAsJsonAsync(EstimateBracketPath, request, ct);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<EstimateBracketResponse>(cancellationToken: ct);
        var tag = string.IsNullOrEmpty(dto?.BracketTag) ? null : dto.BracketTag;

        _bracketCache.TryAdd(cacheKey, tag);
        logger.LogInformation("Spellbook_EstimateBracket: tag={Tag}", tag);
        return tag;
    }

    private static FindMyCombosRequest BuildRequest(
        IReadOnlyList<string> commanders, IReadOnlyList<string> cards) =>
        new()
        {
            Commanders = commanders.Select(n => new SpellbookCardRef { Card = n }).ToList(),
            Main       = cards.Select(n => new SpellbookCardRef { Card = n }).ToList(),
        };

    private static IReadOnlyList<ComboVariant> Map(
        List<SpellbookVariant>? variants, HashSet<string> deckCards)
    {
        if (variants is null or { Count: 0 }) return [];
        return variants.Select(v => MapVariant(v, deckCards)).ToList();
    }

    private static ComboVariant MapVariant(SpellbookVariant dto, HashSet<string> deckCards)
    {
        var owned = dto.Uses
            .Where(u => !string.IsNullOrEmpty(u.Card.Name) && deckCards.Contains(u.Card.Name))
            .Select(u => new ComboPiece(u.Card.Name, u.Card.TypeLine))
            .ToList();

        var missing = dto.Uses
            .Where(u => !string.IsNullOrEmpty(u.Card.Name) && !deckCards.Contains(u.Card.Name))
            .Select(u => u.Card.Name)
            .ToList();

        var templates = dto.Requires
            .Select(r => r.Template.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        return new ComboVariant
        {
            Id                   = dto.Id,
            OwnedPieces          = owned,
            MissingCardNames     = missing,
            MissingTemplates     = templates,
            ProducedEffects      = dto.Produces.Select(p => p.Feature.Name).ToList(),
            Description          = dto.Description,
            BracketTag           = dto.BracketTag,
            Popularity           = dto.Popularity,
            ManaNeeded           = dto.ManaNeeded,
            NotablePrerequisites = dto.NotablePrerequisites,
            ColorIdentity        = dto.Identity,
        };
    }

    private static string ComputeCacheKey(
        string prefix,
        IReadOnlyList<string> commanders,
        IReadOnlyList<string> cards)
    {
        var input = commanders.Select(n => "C:" + n)
            .Concat(cards)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", input)));
        return prefix + ":" + Convert.ToHexString(hash);
    }
}
