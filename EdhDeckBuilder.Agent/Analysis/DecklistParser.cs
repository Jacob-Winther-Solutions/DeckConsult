using System.Text.RegularExpressions;

namespace EdhDeckBuilder.Agent.Analysis;

public sealed record ParsedCardEntry(string Name, int Quantity);

public sealed class DecklistParser
{
    private static readonly Regex CardLinePattern =
        new(@"^(\d+)x?\s+(.+)$", RegexOptions.Compiled);

    private static readonly HashSet<string> SectionHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Lands", "Creatures", "Artifacts", "Enchantments", "Instants", "Sorceries",
        "Planeswalkers", "Sideboard", "Commander", "Maybeboard", "Deck", "Main Deck",
        "Other Spells", "Auras", "Battles", "Removal", "Ramp", "Draw", "Wipes",
        "Tutors", "Utility", "Finishers", "Synergy", "Support", "Spells",
        "Nonland", "Nonbasic", "Basic", "Basics", "Card Advantage",
    };

    /// <summary>
    /// Parses a pasted decklist in plain "1 Card Name" (or "1x Card Name") format.
    /// Blank lines, // comments, and known section headers are skipped.
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
            if (SectionHeaders.Contains(line)) continue;

            var match = CardLinePattern.Match(line);
            if (match.Success)
            {
                var quantity = int.TryParse(match.Groups[1].Value, out var q) ? q : 1;
                var name    = match.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(name))
                    entries.Add(new ParsedCardEntry(name, quantity));
            }
            else if (!SectionHeaders.Contains(line) && line.Length > 1)
            {
                // Bare card name without a leading quantity
                entries.Add(new ParsedCardEntry(line, 1));
            }
        }

        return entries;
    }
}
