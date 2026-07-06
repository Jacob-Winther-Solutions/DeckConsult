using System.Globalization;
using System.Text.RegularExpressions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Infrastructure.Scryfall.Dto;

namespace EdhDeckBuilder.Infrastructure.Scryfall;

internal static class ScryfallMapper
{
    public static Card ToCard(ScryfallCard dto)
    {
        var face0 = dto.CardFaces?.Count > 0 ? dto.CardFaces[0] : null;
        var face1 = dto.CardFaces?.Count > 1 ? dto.CardFaces[1] : null;

        var frontTypeLine = face0?.TypeLine ?? dto.TypeLine;
        var oracleText    = dto.OracleText ?? face0?.OracleText ?? string.Empty;

        return new Card
        {
            ScryfallId        = dto.Id,
            OracleId          = dto.OracleId,
            Name              = dto.Name,
            ManaCost          = dto.ManaCost ?? face0?.ManaCost,
            ManaValue         = dto.Cmc,
            ColorIdentity     = ColorExtensions.FromScryfall(dto.ColorIdentity),
            TypeLine          = dto.TypeLine,
            Types             = ParseTypes(frontTypeLine),
            IsLegendary       = frontTypeLine.Contains("Legendary", StringComparison.OrdinalIgnoreCase),
            IsBasicLand       = frontTypeLine.Contains("Basic", StringComparison.OrdinalIgnoreCase),
            OracleText        = oracleText,
            Power             = dto.Power,
            Toughness         = dto.Toughness,
            CommanderLegality = ParseLegality(dto.Legalities.Commander),
            CanBeCommander    = IsCommanderEligible(dto.Legalities.Commander, frontTypeLine, oracleText),
            Images            = MapImages(dto.ImageUris ?? face0?.ImageUris),
            PriceUsd          = decimal.TryParse(dto.Prices?.Usd, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) ? price : null,
            BackFaceTypeLine  = face1?.TypeLine,
            BackFaceText      = face1?.OracleText,
        };
    }

    /// <summary>
    /// DEPRECATED: Use ExtractPartnershipKeywords(scryfallKeywords) with Scryfall's keywords array instead.
    /// Kept for reference but not used — partnership keywords come from Scryfall's keywords property, not oracle text.
    /// </summary>
    [Obsolete("Use Scryfall keywords property instead of oracle text parsing")]
    internal static IReadOnlySet<string> ExtractPartnershipKeywordsFromOracleText(string oracleText)
    {
        // Placeholder for backward compatibility if needed
        return new HashSet<string>();
    }

    /// <summary>
    /// Filters partnership-relevant keywords from Scryfall's keywords array.
    /// Returns only keywords related to partnerships: Partner, Partner with, Background, etc.
    /// This replaces oracle text parsing with direct keyword data from Scryfall.
    /// </summary>
    internal static IReadOnlySet<string> ExtractPartnershipKeywords(IReadOnlyList<string> scryfallKeywords)
    {
        if (scryfallKeywords == null || scryfallKeywords.Count == 0)
            return new HashSet<string>();

        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var partnershipKeywords = new[]
        {
            "partner",
            "partner with",
            "background",
            "choose a background",
            "friends forever",
            "doctor's companion",
        };

        foreach (var kw in scryfallKeywords)
        {
            // Check if this keyword starts with any partnership keyword
            foreach (var partnerKw in partnershipKeywords)
            {
                if (kw.StartsWith(partnerKw, StringComparison.OrdinalIgnoreCase))
                {
                    keywords.Add(kw);
                    break;
                }
            }
        }

        return keywords;
    }

    private static CardType ParseTypes(string typeLine)
    {
        // Only look at the part before the subtype separator
        var main = typeLine.Contains('—') ? typeLine[..typeLine.IndexOf('—')] : typeLine;

        var result = CardType.None;
        if (main.Contains("Land"))         result |= CardType.Land;
        if (main.Contains("Creature"))     result |= CardType.Creature;
        if (main.Contains("Artifact"))     result |= CardType.Artifact;
        if (main.Contains("Enchantment"))  result |= CardType.Enchantment;
        if (main.Contains("Instant"))      result |= CardType.Instant;
        if (main.Contains("Sorcery"))      result |= CardType.Sorcery;
        if (main.Contains("Planeswalker")) result |= CardType.Planeswalker;
        if (main.Contains("Battle"))       result |= CardType.Battle;
        if (main.Contains("Tribal") || main.Contains("Kindred")) result |= CardType.Kindred;
        return result;
    }

    private static Legality ParseLegality(string value) => value switch
    {
        "legal"      => Legality.Legal,
        "banned"     => Legality.Banned,
        "restricted" => Legality.Restricted,
        _            => Legality.NotLegal,
    };

    // A card can be a Commander if it is a legendary creature, OR if its oracle text explicitly
    // states "can be your commander" (covers planeswalkers like Teferi, Temporal Archmage and
    // any future special cases printed with that text).
    private static bool IsCommanderEligible(string legality, string typeLine, string oracleText)
        => legality == "legal"
        && (
            (typeLine.Contains("Legendary",  StringComparison.OrdinalIgnoreCase)
             && typeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase))
            ||
            oracleText.Contains("can be your commander", StringComparison.OrdinalIgnoreCase)
        );

    private static CardImages? MapImages(ScryfallImageUris? uris) =>
        uris is null ? null : new CardImages(uris.Small, uris.Normal, uris.Large, uris.ArtCrop);
}
