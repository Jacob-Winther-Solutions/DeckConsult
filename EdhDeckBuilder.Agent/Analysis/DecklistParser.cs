using System.Text.RegularExpressions;

namespace EdhDeckBuilder.Agent.Analysis;

public sealed record ParsedCardEntry(string Name, int Quantity);

public sealed class DecklistParser
{
    private static readonly Regex CardLinePattern =
        new(@"^(\d+)x?\s+(.+)$", RegexOptions.Compiled);

    // Arena/Moxfield: "Sol Ring (CMR) 456" or "(CMR) 456 *F*" → "Sol Ring"
    private static readonly Regex ArenaSetCode =
        new(@"\s+\([A-Z0-9]{2,5}\)\s+\d+.*$", RegexOptions.Compiled);

    // Archidekt/MTGO: "Sol Ring [CMR]" or "[Commander Masters]" → "Sol Ring"
    private static readonly Regex BracketSetCode =
        new(@"\s+\[[^\]]+\].*$", RegexOptions.Compiled);

    // Moxfield/Archidekt section headers with card count: "Creatures (24)" → "Creatures"
    private static readonly Regex SectionCountSuffix =
        new(@"\s+\(\d+\)$", RegexOptions.Compiled);

    private static readonly HashSet<string> SectionHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Lands", "Creatures", "Artifacts", "Enchantments", "Instants", "Sorceries",
        "Planeswalkers", "Sideboard", "Commander", "Companion", "Maybeboard", "Deck",
        "Main Deck", "Other Spells", "Auras", "Battles", "Removal", "Ramp", "Draw",
        "Wipes", "Tutors", "Utility", "Finishers", "Synergy", "Support", "Spells",
        "Nonland", "Nonbasic", "Basic", "Basics", "Card Advantage", "Tokens",
    };

    /// <summary>
    /// Parses a pasted decklist. Supports:
    /// <list type="bullet">
    ///   <item>Plain format: "1 Card Name" or "1x Card Name"</item>
    ///   <item>Arena/Moxfield: "1 Card Name (SET) 123" — set code + collector number stripped</item>
    ///   <item>Archidekt/MTGO: "1 Card Name [SET]" — bracket set code stripped</item>
    ///   <item>Moxfield/Archidekt section headers: lines starting with "#" are skipped</item>
    ///   <item>Count-suffixed section headers: "Creatures (24)" treated as a section header</item>
    ///   <item>Sideboard lines prefixed with "SB:" are skipped</item>
    /// </list>
    /// Quantities are preserved so basic lands (e.g. "30 Plains") count correctly.
    /// Duplicate entries are kept; deduplication of non-basics happens during resolution.
    /// </summary>
    public IReadOnlyList<ParsedCardEntry> Parse(string text)
    {
        var entries = new List<ParsedCardEntry>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrEmpty(line)) continue;
            if (line.StartsWith("//")) continue;
            if (line.StartsWith("#")) continue;
            if (line.StartsWith("SB:", StringComparison.OrdinalIgnoreCase)) continue;

            // Skip section headers — including "Creatures (24)" style with count suffix
            var strippedCountLine = SectionCountSuffix.Replace(line, "").Trim();
            if (SectionHeaders.Contains(line) || SectionHeaders.Contains(strippedCountLine)) continue;

            var match = CardLinePattern.Match(line);
            if (match.Success)
            {
                var quantity = int.TryParse(match.Groups[1].Value, out var q) ? q : 1;
                var name    = CleanName(match.Groups[2].Value.Trim());
                if (!string.IsNullOrEmpty(name))
                    entries.Add(new ParsedCardEntry(name, quantity));
            }
            else if (line.Length > 1)
            {
                var name = CleanName(line);
                if (!string.IsNullOrEmpty(name))
                    entries.Add(new ParsedCardEntry(name, 1));
            }
        }

        return entries;
    }

    private static string CleanName(string name)
    {
        // Strip Arena/Moxfield set code first (more specific pattern), then bracket set code
        name = ArenaSetCode.Replace(name, "");
        name = BracketSetCode.Replace(name, "");
        return name.Trim();
    }
}
