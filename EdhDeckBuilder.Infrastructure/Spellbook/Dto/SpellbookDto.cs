using System.Text.Json.Serialization;

namespace EdhDeckBuilder.Infrastructure.Spellbook.Dto;

internal sealed record FindMyCombosRequest
{
    [JsonPropertyName("commanders")]
    public required List<SpellbookCardRef> Commanders { get; init; }

    [JsonPropertyName("main")]
    public required List<SpellbookCardRef> Main { get; init; }
}

internal sealed record SpellbookCardRef
{
    [JsonPropertyName("card")]
    public required string Card { get; init; }
}

internal sealed record FindMyCombosResponse
{
    [JsonPropertyName("results")]
    public SpellbookResults? Results { get; init; }
}

internal sealed record SpellbookResults
{
    [JsonPropertyName("included")]
    public List<SpellbookVariant> Included { get; init; } = [];

    [JsonPropertyName("almostIncluded")]
    public List<SpellbookVariant> AlmostIncluded { get; init; } = [];
}

internal sealed record SpellbookVariant
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("uses")]
    public List<SpellbookUse> Uses { get; init; } = [];

    [JsonPropertyName("requires")]
    public List<SpellbookRequire> Requires { get; init; } = [];

    [JsonPropertyName("produces")]
    public List<SpellbookProduce> Produces { get; init; } = [];

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("bracketTag")]
    public string BracketTag { get; init; } = "";

    [JsonPropertyName("popularity")]
    public int Popularity { get; init; }

    [JsonPropertyName("manaNeeded")]
    public string ManaNeeded { get; init; } = "";

    [JsonPropertyName("notablePrerequisites")]
    public string NotablePrerequisites { get; init; } = "";

    [JsonPropertyName("identity")]
    public string Identity { get; init; } = "";
}

internal sealed record SpellbookUse
{
    [JsonPropertyName("card")]
    public SpellbookCardInfo Card { get; init; } = new();

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }
}

internal sealed record SpellbookCardInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("typeLine")]
    public string TypeLine { get; init; } = "";
}

internal sealed record SpellbookRequire
{
    [JsonPropertyName("template")]
    public SpellbookTemplate Template { get; init; } = new();

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }
}

internal sealed record SpellbookTemplate
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("scryfallQuery")]
    public string? ScryfallQuery { get; init; }
}

internal sealed record SpellbookProduce
{
    [JsonPropertyName("feature")]
    public SpellbookFeature Feature { get; init; } = new();

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }
}

internal sealed record SpellbookFeature
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
}

internal sealed record EstimateBracketResponse
{
    [JsonPropertyName("bracketTag")]
    public string BracketTag { get; init; } = "";

    [JsonPropertyName("cards")]
    public List<BracketCard> Cards { get; init; } = [];
}

internal sealed record BracketCard
{
    [JsonPropertyName("card")]
    public SpellbookCardInfo Card { get; init; } = new();

    [JsonPropertyName("gameChanger")]
    public bool GameChanger { get; init; }

    [JsonPropertyName("massLandDenial")]
    public bool MassLandDenial { get; init; }

    [JsonPropertyName("extraTurn")]
    public bool ExtraTurn { get; init; }

    [JsonPropertyName("banned")]
    public bool Banned { get; init; }
}
