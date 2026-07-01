namespace EdhDeckBuilder.Infrastructure.Scryfall.Dto;

internal sealed class BulkDataList
{
    public List<BulkDataEntry> Data { get; init; } = [];
}

internal sealed class BulkDataEntry
{
    public string Type { get; init; } = "";
    public string DownloadUri { get; init; } = "";
    public DateTimeOffset UpdatedAt { get; init; }
}

internal sealed class ScryfallCard
{
    public Guid Id { get; init; }
    public Guid OracleId { get; init; }
    public string Name { get; init; } = "";
    public string? ManaCost { get; init; }
    public decimal Cmc { get; init; }
    public List<string> ColorIdentity { get; init; } = [];
    public string TypeLine { get; init; } = "";
    public string? OracleText { get; init; }
    public string? Power { get; init; }
    public string? Toughness { get; init; }
    public ScryfallLegalities Legalities { get; init; } = new();
    public ScryfallPrices? Prices { get; init; }
    public ScryfallImageUris? ImageUris { get; init; }
    public List<ScryfallCardFace>? CardFaces { get; init; }
}

internal sealed class ScryfallPrices
{
    public string? Usd { get; init; }
}

internal sealed class ScryfallLegalities
{
    public string Commander { get; init; } = "not_legal";
}

internal sealed class ScryfallImageUris
{
    public string? Small { get; init; }
    public string? Normal { get; init; }
    public string? Large { get; init; }
    public string? ArtCrop { get; init; }
}

internal sealed class ScryfallCardFace
{
    public string? ManaCost { get; init; }
    public string? TypeLine { get; init; }
    public string? OracleText { get; init; }
    public ScryfallImageUris? ImageUris { get; init; }
}
