using System.Text.RegularExpressions;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Partnerships;

namespace EdhDeckBuilder.Infrastructure.Scryfall;

/// <summary>
/// Determines whether two cards can legally partner based on Scryfall keywords property.
/// Handles: Partner (generic), Partner with [specific card], Background, Friends Forever, Doctor's Companion.
/// Matching uses Scryfall keywords directly (no oracle text parsing) for robustness.
/// </summary>
internal sealed class PartnershipEligibilityRule : IPartnershipEligibilityRule
{
    private static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Partner",
        "Partner with",
        "Background",
        "Choose a background",
        "Friends Forever",
        "Doctor's companion",
    };

    public IReadOnlySet<string> SupportedKeywords => Supported;

    public bool CanPartner(Card first, Card second, string firstKeyword, string? secondKeyword)
    {
        // Normalize keywords for comparison (case-insensitive)
        var kw1 = Normalize(firstKeyword);
        var kw2 = Normalize(secondKeyword);

        // Case 1: Generic "Partner" — both cards must have exactly "Partner" (not "Partner with")
        // This handles the original Partner mechanic and Partner - [Variant] cards
        if (IsGenericPartner(kw1) && IsGenericPartner(kw2))
            return true;

        // Case 2: "Partner with [Name]" — must match bidirectionally
        // Both cards must have "Partner with" keyword and name each other in their oracle text
        if (IsPartnerWith(kw1) && IsPartnerWith(kw2))
        {
            var target1 = ExtractPartnerWithTargetFromOracle(first.OracleText);
            var target2 = ExtractPartnerWithTargetFromOracle(second.OracleText);
            return target1 != null && target2 != null
                && string.Equals(first.Name, target2, StringComparison.OrdinalIgnoreCase)
                && string.Equals(second.Name, target1, StringComparison.OrdinalIgnoreCase);
        }

        // Case 3: "Choose a background" ↔ Background type
        // First card has "Choose a background" keyword, second card has Background type (or vice versa)
        if ((HasKeyword(kw1, "choose a background") && IsBackground(second.TypeLine))
            || (HasKeyword(kw2, "choose a background") && IsBackground(first.TypeLine)))
            return true;

        // Case 4: "Doctor's companion" ↔ Time Lord Doctor type
        // First card has "Doctor's companion" keyword, second has Time Lord Doctor type (or vice versa)
        if ((HasKeyword(kw1, "doctor's companion") && IsDoctor(second.TypeLine))
            || (HasKeyword(kw2, "doctor's companion") && IsDoctor(first.TypeLine)))
            return true;

        // Case 5: "Friends Forever" — both cards must have exactly this keyword
        if (HasKeyword(kw1, "friends forever") && HasKeyword(kw2, "friends forever"))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a keyword is generic "Partner" (not "Partner with").
    /// Handles both the keyword "Partner" and Partner - [Variant] forms.
    /// </summary>
    private static bool IsGenericPartner(string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
            return false;

        // Exact match for "Partner" keyword
        // Note: Scryfall stores "Partner" separately from "Partner with"
        return string.Equals(keyword, "Partner", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a keyword is "Partner with" (specific pairing).
    /// </summary>
    private static bool IsPartnerWith(string keyword)
    {
        return !string.IsNullOrEmpty(keyword)
            && keyword.StartsWith("Partner with", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a keyword contains a specific substring (case-insensitive).
    /// </summary>
    private static bool HasKeyword(string keyword, string search)
    {
        return !string.IsNullOrEmpty(keyword)
            && keyword.Equals(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a type line indicates a Background creature.
    /// </summary>
    private static bool IsBackground(string typeLine)
    {
        return !string.IsNullOrEmpty(typeLine)
            && typeLine.Contains("Background", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a type line indicates a Time Lord Doctor creature.
    /// </summary>
    private static bool IsDoctor(string typeLine)
    {
        return !string.IsNullOrEmpty(typeLine)
            && typeLine.Contains("Time Lord Doctor", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the target card name from oracle text containing "Partner with [Name]".
    /// Example oracle text: "Partner with Drana, Liberator of Zendikar\nHaste\n..." → "Drana, Liberator of Zendikar"
    /// The target is the first line content after "Partner with".
    /// </summary>
    private static string? ExtractPartnerWithTargetFromOracle(string oracleText)
    {
        if (string.IsNullOrWhiteSpace(oracleText))
            return null;

        // Split by newlines and find the line starting with "Partner with"
        var lines = oracleText.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Partner with", StringComparison.OrdinalIgnoreCase))
            {
                // Extract everything after "Partner with " up to the parenthesis or end
                var match = Regex.Match(trimmed, @"Partner\s+with\s+(.+?)(?:\s*\(|$)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a keyword starts with a given base (e.g., "Partner" matches "Partner" and "Partner with X").
    /// </summary>
    private static bool IsSameKeywordBase(string keyword, string baseKeyword)
    {
        if (string.IsNullOrEmpty(keyword)) return false;
        return keyword.StartsWith(baseKeyword, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes a keyword string by trimming whitespace and converting to lowercase for comparison.
    /// </summary>
    private static string Normalize(string? keyword)
        => string.IsNullOrWhiteSpace(keyword) ? "" : keyword.Trim();
}
