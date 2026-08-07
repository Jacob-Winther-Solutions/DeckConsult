using System.Net.Http.Json;

namespace EdhDeckBuilder.Web.Services;

/// <summary>
/// Provides a comprehensive, pluralized list of MTG creature types suitable for displaying
/// in the Tribal theme picker. Loads from the Scryfall catalog API on first access and
/// caches for the lifetime of the application. Falls back to a built-in list if the API
/// is unreachable.
/// </summary>
internal sealed class CreatureTypeCatalog
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Lazy<Task<IReadOnlyList<string>>> _types;

    private static readonly IReadOnlyList<string> Fallback =
    [
        "Advisors", "Allies", "Angels", "Artificers", "Assassins",
        "Beasts", "Birds",
        "Cats", "Clerics",
        "Demons", "Dinosaurs", "Dragons", "Druids", "Dwarves",
        "Elementals", "Elves",
        "Faeries",
        "Giants", "Goblins",
        "Humans",
        "Knights",
        "Merfolk", "Mice", "Monks",
        "Orcs",
        "Pirates",
        "Rats", "Rogues",
        "Shapeshifters", "Shamans", "Slivers", "Snakes", "Soldiers", "Spirits",
        "Vampires",
        "Warriors", "Werewolves", "Wizards",
        "Zombies",
    ];

    internal static IReadOnlySet<string> FallbackSlugSet { get; } =
        Fallback.Select(t => t.ToLowerInvariant().Replace(' ', '-')).ToHashSet();

    public CreatureTypeCatalog(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _types = new Lazy<Task<IReadOnlyList<string>>>(LoadAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Task<IReadOnlyList<string>> GetTypesAsync() => _types.Value;

    private async Task<IReadOnlyList<string>> LoadAsync()
    {
        try
        {
            var http = _httpClientFactory.CreateClient();
            var catalog = await http.GetFromJsonAsync<ScryfallCatalog>(
                "https://api.scryfall.com/catalog/creature-types");
            if (catalog?.Data is { Count: > 0 })
                return catalog.Data.Select(Pluralize).OrderBy(s => s).ToList();
        }
        catch
        {
            // Network failure or parse error — fall back to built-in list.
        }
        return Fallback;
    }

    /// <summary>
    /// Converts Scryfall's singular creature type names to the plural form EDHREC uses for
    /// tribe slugs (e.g. "Elf" → "Elves", "Dragon" → "Dragons").
    /// </summary>
    internal static string Pluralize(string singular) => singular switch
    {
        // True irregulars
        "Merfolk" or "Sheep" or "Deer" or "Fish" or "Elk" or "Caribou" => singular,
        "Mouse" => "Mice",
        "Ox" => "Oxen",
        "Louse" => "Lice",
        "Fungus" => "Fungi",
        // Double-f words stay regular
        _ when singular.EndsWith("ff") => singular + "s",
        // -f / -fe → -ves  (Elf→Elves, Wolf→Wolves, Dwarf→Dwarves, Leaf→Leaves)
        _ when singular.EndsWith("fe") => singular[..^2] + "ves",
        _ when singular.EndsWith("f")  => singular[..^1] + "ves",
        // consonant + -y → -ies  (Harpy→Harpies, Faery→Faeries)
        _ when singular.EndsWith("y") && singular.Length > 1 && !"aeiou".Contains(char.ToLower(singular[^2])) => singular[..^1] + "ies",
        // -s / -x / -z / -ch / -sh → -es
        _ when singular.EndsWith("s") || singular.EndsWith("x") || singular.EndsWith("z") => singular + "es",
        _ when singular.EndsWith("ch") || singular.EndsWith("sh") => singular + "es",
        // Default: add -s
        _ => singular + "s",
    };

    private sealed class ScryfallCatalog
    {
        public List<string> Data { get; set; } = [];
    }
}
