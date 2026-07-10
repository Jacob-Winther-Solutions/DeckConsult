using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Partnerships;
using EdhDeckBuilder.Infrastructure.Edhrec.Dto;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Infrastructure.Edhrec;

/// <summary>
/// Builds a definitive partnership index from EDHREC's partner page data.
/// Maps all partnership types (Partner with, Background, Friends Forever, etc.)
/// to their corresponding card pairs using EDHREC as the authoritative source.
/// </summary>
internal static class PartnershipIndexBuilder
{
    /// <summary>
    /// Builds partnership combos from EDHREC partner page data.
    /// Returns a list of valid PartnerCombo records mapped by card names to oracle IDs.
    /// </summary>
    public static List<PartnerCombo> BuildFromEdhrec(
        EdhrecPartnerPage? page,
        IReadOnlyDictionary<string, Card> cardsByName,
        ILogger logger)
    {
        var combos = new List<PartnerCombo>();

        if (page?.Container?.JsonDict?.Cardlists is not { Count: > 0 } cardlists)
        {
            logger.LogWarning("No EDHREC partner data available");
            return combos;
        }

        foreach (var cardlist in cardlists)
        {
            if (cardlist.Tag is null || cardlist.Cardviews.Count == 0)
                continue;

            var tag = cardlist.Tag.ToLowerInvariant();
            var comboPairs = ExtractPairsFromCardlist(cardlist, tag, cardlists);

            foreach (var (firstName, secondName) in comboPairs)
            {
                if (!cardsByName.TryGetValue(firstName, out var firstCard)
                    || !cardsByName.TryGetValue(secondName, out var secondCard))
                {
                    logger.LogDebug("Partner pair not found in card repository: {FirstName} + {SecondName}",
                        firstName, secondName);
                    continue;
                }

                var (type, firstKeyword, secondKeyword) = DeterminePartnershipType(tag);
                var combo = new PartnerCombo(
                    firstCard.OracleId,
                    secondCard.OracleId,
                    type,
                    firstKeyword,
                    secondKeyword);

                combos.Add(combo);
            }
        }

        logger.LogInformation("Built partnership index from EDHREC: {Count} combos", combos.Count);
        return combos;
    }

    /// <summary>
    /// Extracts card name pairs from a cardlist based on its tag type.
    /// Different cardlist types have different pairing patterns.
    /// </summary>
    private static List<(string FirstCardName, string SecondCardName)> ExtractPairsFromCardlist(
        EdhrecPartnerCardlist cardlist,
        string tag,
        IReadOnlyList<EdhrecPartnerCardlist> allCardlists)
    {
        var pairs = new List<(string, string)>();
        var cardNames = cardlist.Cardviews.Select(v => v.Name).ToList();

        return tag switch
        {
            // Partner with: each entry is already a complete pair "Card1 // Card2"
            "partnerwith" => ExtractSplitNamePairs(cardNames),

            // Generic Partner: all cards can pair with each other
            "partners" => ExtractAllPairs(cardNames),

            // Background: creatures with "Choose a background" pair with ALL background cards
            // Need to get the backgrounds list
            "chooseabackground" => ExtractBackgroundCreaturePairs(
                cardNames,
                allCardlists.FirstOrDefault(cl => cl.Tag == "backgrounds")?.Cardviews.Select(v => v.Name).ToList() ?? []),

            // Backgrounds: paired with creatures (handled by chooseabackground case)
            "backgrounds" => pairs,

            // Friends Forever: all cards can pair with each other
            "friendsforever" => ExtractAllPairs(cardNames),

            // Doctor Who: first card is "The Doctor", rest are companions
            "doctors" => ExtractDoctorPairs(cardNames),

            // Survivors: all pairs with each other
            "survivors" => ExtractAllPairs(cardNames),

            // Character Select (TMNT): all pairs with each other
            "characterselect" => ExtractAllPairs(cardNames),

            // Father & Son: all pairs with each other
            "father&son" => ExtractAllPairs(cardNames),

            _ => pairs,
        };
    }

    /// <summary>
    /// Extracts pairs where each entry name encodes both halves as "Card1 // Card2".
    /// Used for "Partner with" where EDHREC lists each pair as a single combined entry.
    /// </summary>
    private static List<(string, string)> ExtractSplitNamePairs(List<string> cardNames)
    {
        var pairs = new List<(string, string)>();
        foreach (var name in cardNames)
        {
            var parts = name.Split(" // ", 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
                pairs.Add((parts[0], parts[1]));
        }
        return pairs;
    }

    /// <summary>
    /// Extracts all possible pairs: every card can pair with every other card.
    /// Used for generic Partner, Friends Forever, Survivors, etc.
    /// </summary>
    private static List<(string, string)> ExtractAllPairs(List<string> cardNames)
    {
        var pairs = new List<(string, string)>();
        for (int i = 0; i < cardNames.Count; i++)
        {
            for (int j = i + 1; j < cardNames.Count; j++)
            {
                pairs.Add((cardNames[i], cardNames[j]));
            }
        }
        return pairs;
    }

    /// <summary>
    /// Extracts background pairs: creatures with "Choose a background" pair with background cards.
    /// Each creature pairs with each background.
    /// </summary>
    private static List<(string, string)> ExtractBackgroundCreaturePairs(List<string> creatures, List<string> backgrounds)
    {
        var pairs = new List<(string, string)>();

        foreach (var creature in creatures)
        {
            foreach (var background in backgrounds)
            {
                pairs.Add((creature, background));
            }
        }
        return pairs;
    }

    /// <summary>
    /// Extracts Doctor Who pairs: first card is "The Doctor", rest are companions.
    /// The Doctor pairs with each companion.
    /// </summary>
    private static List<(string, string)> ExtractDoctorPairs(List<string> cardNames)
    {
        var pairs = new List<(string, string)>();
        if (cardNames.Count < 2)
            return pairs;

        var doctor = cardNames[0];
        for (int i = 1; i < cardNames.Count; i++)
        {
            pairs.Add((doctor, cardNames[i]));
        }
        return pairs;
    }

    /// <summary>
    /// Determines the partnership type and keywords based on the EDHREC cardlist tag.
    /// </summary>
    private static (PartnershipType Type, string FirstKeyword, string? SecondKeyword) DeterminePartnershipType(string tag)
    {
        return tag switch
        {
            "partnerwith" => (PartnershipType.PartnerWith, "partner with", "partner with"),
            "partners" => (PartnershipType.Partner, "partner", "partner"),
            "chooseabackground" or "backgrounds" => (PartnershipType.Background, "choose a background", "background"),
            "friendsforever" => (PartnershipType.FriendsForever, "friends forever", "friends forever"),
            "doctors" => (PartnershipType.DoctorsCompanion, "doctor's companion", "time lord doctor"),
            "survivors" => (PartnershipType.PartnerSurvivors, "partner - survivors", "partner - survivors"),
            "characterselect" => (PartnershipType.Custom, "character select", "character select"),
            "father&son" => (PartnershipType.Custom, "father & son", "father & son"),
            _ => (PartnershipType.Custom, tag, tag),
        };
    }
}
