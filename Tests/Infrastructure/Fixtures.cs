using System.Net;
using System.Text;

namespace EdhDeckBuilder.Tests.Infrastructure;

/// <summary>
/// Fixture JSON strings and known IDs shared across Infrastructure tests.
/// All JSON uses snake_case to match the SnakeCaseLower JsonSerializerOptions in Infrastructure.
/// </summary>
internal static class Fixtures
{
    // Fixed oracle IDs so tests can assert exact lookups.
    public static readonly Guid SolRingId   = new("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid KorvoldId   = new("a0000000-0000-0000-0000-000000000002");
    public static readonly Guid CultivateId = new("a0000000-0000-0000-0000-000000000003");
    public static readonly Guid ForestId    = new("a0000000-0000-0000-0000-000000000004");

    // Partnership test cards
    public static readonly Guid ThrasiosId  = new("a0000000-0000-0000-0000-000000000010");
    public static readonly Guid TymnaId     = new("a0000000-0000-0000-0000-000000000011");
    public static readonly Guid RograkId    = new("a0000000-0000-0000-0000-000000000012");
    public static readonly Guid DranaId     = new("a0000000-0000-0000-0000-000000000013");

    /// <summary>
    /// A minimal Scryfall oracle_cards JSON array — exactly what the cached file looks like.
    /// Sol Ring and Korvold are included to cover: artifact, legendary creature, color identity,
    /// CanBeCommander heuristic. Cultivate covers sorcery + green. Forest covers basic land.
    /// </summary>
    public static string ScryfallOracleCards => $$"""
        [
          {
            "id":             "{{SolRingId}}",
            "oracle_id":      "{{SolRingId}}",
            "name":           "Sol Ring",
            "mana_cost":      "{1}",
            "cmc":            1.0,
            "color_identity": [],
            "type_line":      "Artifact",
            "oracle_text":    "{T}: Add {C}{C}.",
            "legalities":     { "commander": "legal" }
          },
          {
            "id":             "{{KorvoldId}}",
            "oracle_id":      "{{KorvoldId}}",
            "name":           "Korvold, Fae-Cursed King",
            "mana_cost":      "{2}{B}{R}{G}",
            "cmc":            5.0,
            "color_identity": ["B", "R", "G"],
            "type_line":      "Legendary Creature — Dragon Noble",
            "oracle_text":    "Flying, haste. Whenever Korvold enters or attacks, sacrifice another permanent.",
            "power":          "5",
            "toughness":      "5",
            "legalities":     { "commander": "legal" }
          },
          {
            "id":             "{{CultivateId}}",
            "oracle_id":      "{{CultivateId}}",
            "name":           "Cultivate",
            "mana_cost":      "{2}{G}",
            "cmc":            3.0,
            "color_identity": ["G"],
            "type_line":      "Sorcery",
            "oracle_text":    "Search your library for up to two basic land cards.",
            "legalities":     { "commander": "legal" }
          },
          {
            "id":             "{{ForestId}}",
            "oracle_id":      "{{ForestId}}",
            "name":           "Forest",
            "cmc":            0.0,
            "color_identity": [],
            "type_line":      "Basic Land — Forest",
            "legalities":     { "commander": "legal" }
          },
          {
            "id":             "{{ThrasiosId}}",
            "oracle_id":      "{{ThrasiosId}}",
            "name":           "Thrasios, Triton Hero",
            "mana_cost":      "{2}{G}{U}",
            "cmc":            4.0,
            "color_identity": ["G", "U"],
            "type_line":      "Legendary Creature — Merfolk",
            "oracle_text":    "Partner\n{T}: Draw a card, then you may put a land card from your hand onto the battlefield.",
            "keywords":       ["partner"],
            "power":          "2",
            "toughness":      "2",
            "legalities":     { "commander": "legal" }
          },
          {
            "id":             "{{TymnaId}}",
            "oracle_id":      "{{TymnaId}}",
            "name":           "Tymna the Weaver",
            "mana_cost":      "{1}{W}{B}",
            "cmc":            3.0,
            "color_identity": ["W", "B"],
            "type_line":      "Legendary Creature — Cleric",
            "oracle_text":    "Partner\n{T}: Exile the top card of your library. You may play that card this turn.",
            "keywords":       ["partner"],
            "power":          "2",
            "toughness":      "2",
            "legalities":     { "commander": "legal" }
          },
          {
            "id":             "{{RograkId}}",
            "oracle_id":      "{{RograkId}}",
            "name":           "Rograkh, Son of Rohgadh",
            "mana_cost":      "{R}",
            "cmc":            1.0,
            "color_identity": ["R"],
            "type_line":      "Legendary Creature — Goblin Warrior",
            "oracle_text":    "Partner with Drana, Liberator of Zendikar\nHaste\nRograkh, Son of Rohgadh cannot be blocked except by creatures with flying.",
            "keywords":       ["partner with"],
            "power":          "1",
            "toughness":      "1",
            "legalities":     { "commander": "legal" }
          },
          {
            "id":             "{{DranaId}}",
            "oracle_id":      "{{DranaId}}",
            "name":           "Drana, Liberator of Zendikar",
            "mana_cost":      "{2}{B}",
            "cmc":            3.0,
            "color_identity": ["B"],
            "type_line":      "Legendary Creature — Vampire Ally",
            "oracle_text":    "Partner with Rograkh, Son of Rohgadh\nMenace\nWhenever a land enters the battlefield under your control, each opponent loses 1 life.",
            "keywords":       ["partner with"],
            "power":          "2",
            "toughness":      "2",
            "legalities":     { "commander": "legal" }
          }
        ]
        """;

    /// <summary>
    /// A minimal Scryfall bulk-data manifest pointing to a configurable download URI.
    /// Used for testing the stale-cache download path in ScryfallBulkClient.
    /// </summary>
    public static string ScryfallBulkManifest(string downloadUri) => $$"""
        {
          "data": [
            {
              "type":         "oracle_cards",
              "download_uri": "{{downloadUri}}",
              "updated_at":   "2024-01-01T00:00:00Z"
            }
          ]
        }
        """;

    /// <summary>
    /// A minimal EDHREC commander page for Korvold.
    /// Sol Ring appears in both Ramp (0.80) and High Synergy (0.85) — the deduplication
    /// logic should keep the High Synergy entry since it has the higher inclusion rate.
    /// </summary>
    public static string EdhrecKorvoldPage => """
        {
          "container": {
            "json_dict": {
              "cardlists": [
                {
                  "header": "Ramp",
                  "tag": "ramp",
                  "cardviews": [
                    { "name": "Sol Ring",  "num_decks": 800, "potential_decks": 1000, "synergy": 0.10 },
                    { "name": "Cultivate", "num_decks": 600, "potential_decks": 1000, "synergy": 0.05 }
                  ]
                },
                {
                  "header": "High Synergy Cards",
                  "tag": "synergy",
                  "cardviews": [
                    { "name": "Sol Ring", "num_decks": 850, "potential_decks": 1000, "synergy": 0.30 }
                  ]
                }
              ]
            }
          }
        }
        """;
}

/// <summary>
/// Creates an isolated temp directory for a single test and deletes it on Dispose.
/// Keeps test file I/O from interfering across test runs.
/// </summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"edhtest_{Guid.NewGuid():N}");

    public TempDir() => Directory.CreateDirectory(Path);

    public string FilePath(string fileName) => System.IO.Path.Combine(Path, fileName);

    public void WriteFile(string fileName, string contents) =>
        File.WriteAllText(FilePath(fileName), contents);

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}

/// <summary>
/// HttpMessageHandler backed by a delegate — lets tests control HTTP responses without
/// hitting the network. All responses are synchronous; real latency is irrelevant here.
/// </summary>
internal sealed class FakeHttpHandler(
    Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(respond(request));
}
