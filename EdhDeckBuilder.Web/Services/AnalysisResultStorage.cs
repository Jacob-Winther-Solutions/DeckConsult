using System.Text.Json;
using System.Text.Json.Serialization;
using EdhDeckBuilder.Agent.Models;

namespace EdhDeckBuilder.Web.Services;

public sealed record StoredAnalysisResult(
    DeckAnalysisResult Result,
    DateOnly AnalyzedOn);

public static class AnalysisResultStorage
{
    public const int DefaultMaxSavedResults = 3;
    public const string IndexKey = "edh-analysis-index";
    private const string KeyPrefix = "edh-analysis-";

    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public static string LocalStorageKey(string id) => KeyPrefix + id;

    public static string? ExtractId(string key) =>
        key.StartsWith(KeyPrefix) ? key[KeyPrefix.Length..] : null;

    public static string Serialize(StoredAnalysisResult result) =>
        JsonSerializer.Serialize(result, Options);

    public static StoredAnalysisResult? Deserialize(string json) =>
        JsonSerializer.Deserialize<StoredAnalysisResult>(json, Options);
}
