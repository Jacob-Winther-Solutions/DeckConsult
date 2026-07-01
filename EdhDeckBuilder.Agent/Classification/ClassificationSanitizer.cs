using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Classification;

/// <summary>
/// Deterministic corrections applied to raw LLM classification results before they are
/// cached or used. Guards against structural misclassifications the prompt already forbids
/// but which low-temperature models occasionally produce anyway.
/// </summary>
public static class ClassificationSanitizer
{
    /// <summary>
    /// Corrects any result where the LLM assigned <see cref="CardRole.Land"/> to a card
    /// whose type line is not Land. The prompt already says "assign Land only to actual
    /// land cards", but mana-producing artifacts (Thought Vessel, Sol Ring variants, etc.)
    /// are occasionally misfiled. Corrects primary to <see cref="CardRole.Ramp"/> and
    /// strips Land from any secondary contributions.
    /// </summary>
    public static ClassificationResult SanitizeLandRole(ClassificationResult result, CardType cardType)
    {
        if (cardType.HasFlag(CardType.Land))
            return result;

        if (result.PrimaryRole != CardRole.Land && !result.Secondary.Any(s => s.Role == CardRole.Land))
            return result;

        var correctedPrimary = result.PrimaryRole == CardRole.Land ? CardRole.Ramp : result.PrimaryRole;
        var correctedSecondary = result.Secondary.Where(s => s.Role != CardRole.Land).ToArray();

        return result with { PrimaryRole = correctedPrimary, Secondary = correctedSecondary };
    }

    /// <summary>
    /// Zeroes out <see cref="ClassificationResult.LandCredit"/> for any card whose back face
    /// is not a Land type. Catches the case where the model assigns land credit to a DFC whose
    /// back face is a creature, planeswalker, or other non-land card type.
    /// </summary>
    public static ClassificationResult SanitizeLandCredit(ClassificationResult result, string? backFaceTypeLine)
    {
        if (result.LandCredit == 0)
            return result;

        var backFaceIsLand = backFaceTypeLine?.Contains("Land", StringComparison.OrdinalIgnoreCase) == true;
        return backFaceIsLand ? result : result with { LandCredit = 0 };
    }
}
