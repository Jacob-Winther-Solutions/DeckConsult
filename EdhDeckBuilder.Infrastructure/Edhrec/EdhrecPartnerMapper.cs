using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Partnerships;
using EdhDeckBuilder.Infrastructure.Edhrec.Dto;
using Microsoft.Extensions.Logging;

namespace EdhDeckBuilder.Infrastructure.Edhrec;

/// <summary>
/// Extracts partner relationships and popularity metrics from EDHREC's partner page.
/// Uses EDHREC as the source of truth for "Partner with" pairings.
/// </summary>
internal static class EdhrecPartnerMapper
{
    /// <summary>
    /// Extracts definitive "Partner with" pairs from EDHREC partner page.
    /// The "Partner with" cardlist contains pre-matched pairs that are the source of truth.
    /// Returns a list of card-name pairs in the order they appear in EDHREC.
    /// </summary>
    public static List<(string FirstCardName, string SecondCardName)> ExtractPartnerWithPairs(
        EdhrecPartnerPage? page,
        ILogger logger)
    {
        var result = new List<(string, string)>();

        if (page?.Container?.JsonDict?.Cardlists is not { Count: > 0 } cardlists)
        {
            logger.LogWarning("No partner data found in EDHREC partners page");
            return result;
        }

        // Find the "Partner with" cardlist (identified by tag "partnerwith")
        var partnerWithList = cardlists.FirstOrDefault(cl => cl.Tag == "partnerwith");
        if (partnerWithList is null)
        {
            logger.LogWarning("No 'Partner with' cardlist found in EDHREC data");
            return result;
        }

        // Pairs are listed sequentially: [A, B, C, D] means A+B and C+D
        var cards = partnerWithList.Cardviews.Select(v => v.Name).ToList();
        for (int i = 0; i < cards.Count - 1; i += 2)
        {
            result.Add((cards[i], cards[i + 1]));
        }

        logger.LogInformation("Extracted {Count} 'Partner with' pairs from EDHREC", result.Count);
        return result;
    }

    /// <summary>
    /// Scores a partner combo based on EDHREC partner page data.
    /// Returns a popularity score (0-1) where higher means more popular.
    /// If the combo is not found in EDHREC data, returns 0.
    /// </summary>
    public static double ScoreCombo(
        PartnerCombo combo,
        ICardRepository repository,
        Dictionary<string, int> partnerNameToDecks,
        ILogger logger)
    {
        // A valid pair must have both cards present in EDHREC's partner data
        var firstCard = repository.GetByOracleIdAsync(combo.FirstCardId).GetAwaiter().GetResult();
        var secondCard = repository.GetByOracleIdAsync(combo.SecondCardId).GetAwaiter().GetResult();

        if (firstCard is null || secondCard is null)
            return 0;

        var firstScore = partnerNameToDecks.GetValueOrDefault(firstCard.Name, 0);
        var secondScore = partnerNameToDecks.GetValueOrDefault(secondCard.Name, 0);

        // Average the two scores (both should be present in a well-known pair)
        return (firstScore + secondScore) / 2.0 / 100_000.0; // Normalize to rough 0-1 range
    }

    /// <summary>
    /// Extracts partner card names and their deck counts from EDHREC partner page.
    /// Flattens all cardlists into a single map of name -> deck count.
    /// </summary>
    public static Dictionary<string, int> ExtractPartnerPopularity(EdhrecPartnerPage? page, ILogger logger)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (page?.Container?.JsonDict?.Cardlists is not { Count: > 0 } cardlists)
        {
            logger.LogWarning("No partner data found in EDHREC partners page");
            return result;
        }

        foreach (var list in cardlists)
        {
            foreach (var view in list.Cardviews)
            {
                // Keep the highest deck count if a card appears in multiple categories
                if (!result.TryGetValue(view.Name, out var existing) || view.NumDecks > existing)
                    result[view.Name] = view.NumDecks;
            }
        }

        logger.LogInformation("Extracted popularity for {Count} partner cards from EDHREC", result.Count);
        return result;
    }
}
