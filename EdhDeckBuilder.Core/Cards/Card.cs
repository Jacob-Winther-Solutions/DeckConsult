namespace EdhDeckBuilder.Core.Cards;

/// <summary>Commander-format legality, mapped directly from Scryfall's <c>legalities.commander</c> field.</summary>
public enum Legality
{
    Legal,
    NotLegal,
    Banned,
    Restricted,
}

/// <summary>
/// Card types parsed from the type line. Flags, because a card can be several at once
/// (e.g. an Artifact Creature). Supertypes such as Legendary/Basic live on <see cref="Card"/> instead.
/// </summary>
[Flags]
public enum CardType
{
    None         = 0,
    Land         = 1 << 0,
    Creature     = 1 << 1,
    Artifact     = 1 << 2,
    Enchantment  = 1 << 3,
    Instant      = 1 << 4,
    Sorcery      = 1 << 5,
    Planeswalker = 1 << 6,
    Battle       = 1 << 7,
    Kindred      = 1 << 8, // formerly "Tribal"
}

/// <summary>Sized image URLs from Scryfall's <c>image_uris</c>, for the visual deck view.</summary>
public sealed record CardImages(string? Small, string? Normal, string? Large, string? ArtCrop);

/// <summary>
/// An immutable representation of a Magic card, sourced from Scryfall. Color identity and
/// legality come straight from Scryfall rather than being derived from the mana cost or text.
/// </summary>
public sealed record Card
{
    public required Guid ScryfallId { get; init; }

    /// <summary>Stable across printings — use this as the identity key for singleton checks.</summary>
    public required Guid OracleId { get; init; }

    public required string Name { get; init; }

    /// <summary>e.g. "{2}{U}{U}". Null for lands and other costless cards.</summary>
    public string? ManaCost { get; init; }

    /// <summary>Scryfall's <c>cmc</c> (mana value). Decimal because of cards like {½}.</summary>
    public decimal ManaValue { get; init; }

    public Color ColorIdentity { get; init; } = Color.None;

    public required string TypeLine { get; init; }

    public CardType Types { get; init; } = CardType.None;

    public bool IsLegendary { get; init; }

    public bool IsBasicLand { get; init; }

    public string OracleText { get; init; } = string.Empty;

    public string? Power { get; init; }     // strings, because of "*", "1+*", etc.
    public string? Toughness { get; init; }

    public Legality CommanderLegality { get; init; } = Legality.Legal;

    /// <summary>
    /// Whether this card may be a commander. Usually a legendary creature, but also cards
    /// whose text explicitly allows it (some planeswalkers, backgrounds, etc.). Determined at ingestion.
    /// </summary>
    public bool CanBeCommander { get; init; }

    public CardImages? Images { get; init; }

    /// <summary>Non-foil USD price from Scryfall at ingestion time. Null when Scryfall has no price data.</summary>
    public decimal? PriceUsd { get; init; }
}
