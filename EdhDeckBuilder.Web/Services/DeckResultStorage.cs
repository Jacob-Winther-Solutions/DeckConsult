using System.Text.Json;
using System.Text.Json.Serialization;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Web.Services;

public sealed record StoredDeckResult(
    DeckBuildResult Result,
    IReadOnlyList<Card> Commanders,
    IReadOnlyDictionary<Archetype, double> ArchetypeWeights,
    IReadOnlyList<WeightedTheme>? Themes,
    Bracket Bracket,
    decimal? MaxCardPriceUsd,
    decimal? TotalBudgetUsd,
    DateOnly BuiltOn);

public static class DeckResultStorage
{
    /// <summary>
    /// How many deck results to keep in localStorage. Wire this up to a subscription/feature-flag
    /// service when tiered limits are needed — pass the resolved value to <c>saveDeckResult</c> in JS.
    /// </summary>
    public const int DefaultMaxSavedResults = 3;
    public const string IndexKey = "edh-deck-index";
    private const string KeyPrefix = "edh-deck-";

    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public static string LocalStorageKey(string id) => KeyPrefix + id;

    public static string? ExtractId(string key) =>
        key.StartsWith(KeyPrefix) ? key[KeyPrefix.Length..] : null;

    public static string Serialize(StoredDeckResult result) =>
        JsonSerializer.Serialize(result, Options);

    public static StoredDeckResult? Deserialize(string json) =>
        JsonSerializer.Deserialize<StoredDeckResult>(json, Options);
}
