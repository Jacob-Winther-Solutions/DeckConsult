using System.Text;
using System.Text.RegularExpressions;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Infrastructure.Edhrec;

internal static partial class EdhrecSlugger
{
    public static string FromCard(Card commander)
    {
        // DFC names are "Front // Back" — EDHREC uses only the front face
        var name = commander.Name.Contains(" // ")
            ? commander.Name[..commander.Name.IndexOf(" // ")]
            : commander.Name;
        return ToSlug(name);
    }

    internal static string ToSlug(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is ' ' or '-')   sb.Append('-');
            // apostrophes, commas, periods and other punctuation are dropped
        }
        return CollapseHyphens().Replace(sb.ToString(), "-").Trim('-');
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex CollapseHyphens();
}
