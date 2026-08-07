using System.Globalization;
using System.Text;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Web.Services;

public static class DeckReportExporter
{
    public static string Export(
        DeckBuildResult result,
        IReadOnlyList<Card> commanders,
        IReadOnlyDictionary<Archetype, double> archetypeWeights,
        IReadOnlyList<WeightedTheme>? themes,
        Bracket bracket,
        decimal? maxCardPriceUsd,
        decimal? totalBudgetUsd,
        DateOnly buildDate)
    {
        var sb = new StringBuilder();
        var commanderNames = string.Join(" + ", commanders.Select(c => c.Name));

        sb.AppendLine($"# Build Report — {commanderNames}");
        sb.AppendLine();
        sb.AppendLine($"**Date:** {buildDate:yyyy-MM-dd}  ");
        sb.AppendLine($"**Bracket:** {(int)bracket} — {BracketLibrary.All[bracket].Name}  ");

        if (archetypeWeights.Count > 0)
        {
            var archetypeText = string.Join(", ", archetypeWeights.Select(kv =>
                kv.Value < 1.0
                    ? $"{ArchetypeLibrary.All[kv.Key].Name} (half)"
                    : ArchetypeLibrary.All[kv.Key].Name));
            sb.AppendLine($"**Archetype:** {archetypeText}  ");
        }

        if (themes?.Count > 0)
        {
            var themesText = string.Join(", ", themes.Select(wt =>
                wt.Weight < 1.0 ? $"{wt.Profile.Name} (half)" : wt.Profile.Name));
            sb.AppendLine($"**Themes:** {themesText}  ");
        }

        if (maxCardPriceUsd.HasValue || totalBudgetUsd.HasValue)
        {
            var parts = new List<string>();
            if (maxCardPriceUsd.HasValue) parts.Add($"Max per card: ${maxCardPriceUsd.Value.ToString("F2", CultureInfo.InvariantCulture)}");
            if (totalBudgetUsd.HasValue)  parts.Add($"Total deck: ${totalBudgetUsd.Value.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"**Budget:** {string.Join(" | ", parts)}  ");
        }

        if (result.TotalPriceUsd > 0)
            sb.AppendLine($"**Total price:** ${result.TotalPriceUsd.ToString("F2", CultureInfo.InvariantCulture)}  ");

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // ── Role buckets ───────────────────────────────────────────────────

        sb.AppendLine("## Deck");
        sb.AppendLine();

        foreach (var role in RoleDisplayOrder)
        {
            var cards = result.Deck
                .Where(c => c.Roles.Primary == role)
                .OrderBy(c => c.Rank)
                .ToList();
            if (cards.Count == 0) continue;

            sb.AppendLine($"### {RoleName(role)} ({cards.Count} cards)");
            sb.AppendLine();

            foreach (var s in cards)
            {
                var priceTag = s.Card.PriceUsd.HasValue
                    ? $" [${s.Card.PriceUsd.Value.ToString("F2", CultureInfo.InvariantCulture)}]"
                    : "";
                sb.AppendLine($"{s.Rank}. **{s.Card.Name}**{priceTag} — {s.Reason}");

                if (s.Roles.Secondary.Count > 0)
                {
                    var contribs = string.Join(", ", s.Roles.Secondary.Select(c =>
                        $"{RoleName(c.Role)} ({RelationLabel(c.Relation)})"));
                    sb.AppendLine($"   *Also: {contribs}*");
                }
            }

            sb.AppendLine();
        }

        if (result.BasicLandCounts.Count > 0)
        {
            sb.AppendLine("### Basic Lands");
            sb.AppendLine();
            foreach (var (land, count) in result.BasicLandCounts.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"- {count}× {land}");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();

        // ── Coverage summary ───────────────────────────────────────────────

        sb.AppendLine("## Coverage Summary");
        sb.AppendLine();
        sb.AppendLine("| Role | Target (min–ideal–max) | Actual | Status |");
        sb.AppendLine("|------|------------------------|--------|--------|");

        foreach (var role in RoleDisplayOrder)
        {
            if (!result.PlannedTemplate.Targets.TryGetValue(role, out var target)) continue;
            var actual = result.ActualCoverage.GetValueOrDefault(role, 0.0);
            var status = actual >= target.Min ? "OK" : "LOW";
            sb.AppendLine($"| {RoleName(role)} | {target.Min}–{target.Ideal}–{target.Max} | {actual:0.#} | {status} |");
        }

        sb.AppendLine();

        if (result.CoverageWarnings.Count > 0)
        {
            sb.AppendLine("**Coverage warnings:**");
            foreach (var w in result.CoverageWarnings)
                sb.AppendLine($"- {w}");
            sb.AppendLine();
        }

        if (result.BudgetWarnings.Count > 0)
        {
            sb.AppendLine("**Budget warnings:**");
            foreach (var w in result.BudgetWarnings)
                sb.AppendLine($"- {w}");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();

        // ── Runner-ups ─────────────────────────────────────────────────────

        if (result.RunnerUps.Count > 0)
        {
            sb.AppendLine("## Runner-Ups");
            sb.AppendLine();

            foreach (var group in result.RunnerUps.GroupBy(r => r.Section).OrderBy(g => g.Key))
            {
                sb.AppendLine($"### {group.Key}");
                sb.AppendLine(string.Join(", ", group
                    .OrderByDescending(c => c.Inclusion)
                    .Take(12)
                    .Select(c => $"{c.Card.Name} ({c.Inclusion:P0})")));
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        // ── Raw decklist ───────────────────────────────────────────────────

        sb.AppendLine("## Raw Decklist");
        sb.AppendLine();
        sb.AppendLine("*(Paste directly into your favorite deck builder)*");
        sb.AppendLine();

        sb.AppendLine(ExportDecklist(result, commanders));

        return sb.ToString();
    }

    public static string ExportDecklist(DeckBuildResult result, IReadOnlyList<Card> commanders)
    {
        var sb = new StringBuilder();
        foreach (var c in commanders)
            sb.AppendLine($"1 {c.Name}");
        sb.AppendLine();
        foreach (var s in result.Deck.OrderBy(s => s.Card.Name))
            sb.AppendLine($"1 {s.Card.Name}");
        foreach (var (land, count) in result.BasicLandCounts.OrderByDescending(kv => kv.Value))
            sb.AppendLine($"{count} {land}");
        return sb.ToString().TrimEnd();
    }

    public static string SlugifyFilename(IReadOnlyList<Card> commanders)
    {
        var name = string.Join("-and-", commanders.Select(c => c.Name));
        var sb = new StringBuilder();
        var lastWasDash = false;
        foreach (var ch in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                sb.Append('-');
                lastWasDash = true;
            }
        }
        return sb.ToString().Trim('-');
    }

    public static string ExportAnalysis(DeckAnalysisResult result, DateOnly analysisDate)
    {
        var sb = new StringBuilder();
        var commanderNames = string.Join(" + ", result.Commanders.Select(c => c.Name));

        sb.AppendLine($"# Analysis Report — {commanderNames}");
        sb.AppendLine();
        sb.AppendLine($"**Date:** {analysisDate:yyyy-MM-dd}  ");
        if (result.SpellbookBracket is not null)
            sb.AppendLine($"**Estimated Bracket:** {(int)result.SpellbookBracket} — {BracketLibrary.All[result.SpellbookBracket.Value].Name}  ");
        if (result.TotalPriceUsd > 0)
            sb.AppendLine($"**Total price:** ${result.TotalPriceUsd.ToString("F2", CultureInfo.InvariantCulture)}  ");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(result.PlanDescription))
        {
            sb.AppendLine("## Deck Strategy");
            sb.AppendLine();
            sb.AppendLine(result.PlanDescription);
            sb.AppendLine();
        }

        if (result.RoleGaps.Count > 0)
        {
            sb.AppendLine("## Role Gaps");
            sb.AppendLine();
            foreach (var gap in result.RoleGaps)
                sb.AppendLine($"- **{RoleName(gap.Role)}**: {gap.ActualCoverage:0.#} / {gap.IdealTarget} ideal ({gap.Shortfall:0.#} short)");
            sb.AppendLine();
        }

        sb.AppendLine("## Cards by Role");
        sb.AppendLine();
        var allCards = result.CommanderCards.Concat(result.Cards).ToList();
        foreach (var role in RoleDisplayOrder)
        {
            var primary = allCards.Where(c => c.Roles.Primary == role).OrderBy(c => c.Card.Name).ToList();
            if (primary.Count == 0) continue;
            sb.AppendLine($"### {RoleName(role)} ({primary.Count})");
            foreach (var card in primary)
            {
                var priceTag = card.Card.PriceUsd.HasValue
                    ? $" [${card.Card.PriceUsd.Value.ToString("F2", CultureInfo.InvariantCulture)}]"
                    : "";
                var cmdTag = card.IsCommander ? " *(Commander)*" : "";
                sb.AppendLine($"- **{card.Card.Name}**{priceTag}{cmdTag}");
            }
            sb.AppendLine();
        }

        if (result.BasicLandCounts.Count > 0)
        {
            sb.AppendLine("### Basic Lands");
            foreach (var (land, count) in result.BasicLandCounts.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"- {count}× {land}");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Coverage vs. Balanced Baseline");
        sb.AppendLine();
        sb.AppendLine("| Role | Baseline (min–ideal–max) | Actual | Status |");
        sb.AppendLine("|------|--------------------------|--------|--------|");
        foreach (var role in RoleDisplayOrder)
        {
            if (!DeckTemplate.Balanced.Targets.TryGetValue(role, out var target)) continue;
            var actual = result.ActualCoverage.GetValueOrDefault(role, 0.0);
            sb.AppendLine($"| {RoleName(role)} | {target.Min}–{target.Ideal}–{target.Max} | {actual:0.#} | {(actual >= target.Min ? "OK" : "LOW")} |");
        }
        sb.AppendLine();

        if (result.UnresolvedNames.Count > 0)
        {
            sb.AppendLine("**Unresolved cards:**");
            foreach (var n in result.UnresolvedNames) sb.AppendLine($"- {n}");
            sb.AppendLine();
        }
        if (result.ColorIdentityViolations.Count > 0)
        {
            sb.AppendLine("**Color identity violations:**");
            foreach (var v in result.ColorIdentityViolations) sb.AppendLine($"- {v}");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Analyzed Decklist");
        sb.AppendLine();
        foreach (var c in result.Commanders)
            sb.AppendLine($"1 {c.Name}");
        sb.AppendLine();
        foreach (var card in result.Cards.OrderBy(c => c.Card.Name))
            sb.AppendLine($"1 {card.Card.Name}");
        foreach (var (land, count) in result.BasicLandCounts.OrderByDescending(kv => kv.Value))
            sb.AppendLine($"{count} {land}");

        return sb.ToString();
    }

    private static readonly CardRole[] RoleDisplayOrder = CardRoleDisplay.DisplayOrder;

    private static string RoleName(CardRole role) => CardRoleDisplay.RoleName(role);

    private static string RelationLabel(RoleRelation rel) => rel switch
    {
        RoleRelation.Always    => "always",
        RoleRelation.Modal     => "modal",
        RoleRelation.Transform => "transform",
        _                      => rel.ToString().ToLowerInvariant(),
    };
}
