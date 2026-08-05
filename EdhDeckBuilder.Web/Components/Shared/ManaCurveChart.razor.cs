using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Shared;

public partial class ManaCurveChart : ComponentBase
{
    [Parameter, EditorRequired] public required DeckAnalysisResult AnalysisResult { get; set; }

    private static readonly string[] TypeOrder = CardRoleDisplay.TypeOrder;

    private sealed record ManaCurveBucket(string Label, int Count, double BarHeightPx);
    private sealed record BadgeInfo(string CssClass, string Label, string Tooltip);

    private readonly HashSet<string> _collapsedMvBuckets = [];

    private void ToggleMvBucket(string key)
    {
        if (!_collapsedMvBuckets.Add(key)) _collapsedMvBuckets.Remove(key);
    }

    private static BadgeInfo SecondaryBadge(RoleContribution contrib)
    {
        var name = CardRoleDisplay.RoleName(contrib.Role);
        return contrib.Relation switch
        {
            RoleRelation.Always    => new("bg-info bg-opacity-75 text-dark", name,
                                         $"Always fills {name} (+{contrib.Weight:0.##} coverage)"),
            RoleRelation.Modal     => new("bg-secondary bg-opacity-50", name + "?",
                                         $"Sometimes fills {name} (+{contrib.Weight:0.##} coverage, modal)"),
            RoleRelation.Transform => new("bg-secondary bg-opacity-50", "→ " + name,
                                         $"Eventually fills {name} (+{contrib.Weight:0.##} coverage, transform)"),
            _                      => new("bg-secondary", name, ""),
        };
    }

    private (ManaCurveBucket[] Buckets, double AverageMv, int TotalSpells, int TotalLands, int MdfcLandCount, int MaxScale) GetManaCurveData()
    {
        var all    = AnalysisResult.CommanderCards.Concat(AnalysisResult.Cards).ToList();
        var spells = all.Where(c => CardRoleDisplay.CardTypeBucket(c.Card) != "Land").ToList();
        var basics = AnalysisResult.BasicLandCounts.Values.Sum();
        var totalLands = all.Count(c => CardRoleDisplay.CardTypeBucket(c.Card) == "Land") + basics;

        var buckets = new int[8];
        foreach (var card in spells)
        {
            var mv = (int)Math.Floor(card.Card.ManaValue);
            buckets[Math.Min(mv, 7)]++;
        }

        var maxCount = buckets.Max();
        var maxScale = maxCount == 0 ? 5 : (int)(Math.Ceiling(maxCount / 5.0) * 5);
        const double barMaxPx = 180.0;
        var curveData = Enumerable.Range(0, 8).Select(i => new ManaCurveBucket(
            i == 7 ? "7+" : i.ToString(),
            buckets[i],
            maxScale > 0 ? buckets[i] / (double)maxScale * barMaxPx : 0.0
        )).ToArray();

        var avgMv = spells.Count > 0 ? (double)spells.Average(c => c.Card.ManaValue) : 0.0;

        var mdfcLands = spells.Count(c =>
            c.Card.TypeLine.Contains(" // ", StringComparison.Ordinal) &&
            c.Card.TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase));

        return (curveData, avgMv, spells.Count, totalLands, mdfcLands, maxScale);
    }

    private static string GetSplinePath(ManaCurveBucket[] buckets, int maxScale)
    {
        if (buckets.Length == 0 || maxScale == 0) return "";

        const double vbWidth  = 800.0;
        const double vbHeight = 180.0;

        var pts = buckets.Select((b, i) => (
            x: (i + 0.5) / buckets.Length * vbWidth,
            y: vbHeight - b.Count / (double)maxScale * vbHeight
        )).ToArray();

        var sb = new System.Text.StringBuilder();
        sb.Append(FormattableString.Invariant($"M {pts[0].x:F1} {pts[0].y:F1}"));

        for (int i = 0; i < pts.Length - 1; i++)
        {
            var p0 = pts[Math.Max(0, i - 1)];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = pts[Math.Min(pts.Length - 1, i + 2)];

            var cp1x = p1.x + (p2.x - p0.x) / 6.0;
            var cp1y = p1.y + (p2.y - p0.y) / 6.0;
            var cp2x = p2.x - (p3.x - p1.x) / 6.0;
            var cp2y = p2.y - (p3.y - p1.y) / 6.0;

            sb.Append(FormattableString.Invariant(
                $" C {cp1x:F1} {cp1y:F1} {cp2x:F1} {cp2y:F1} {p2.x:F1} {p2.y:F1}"));
        }

        return sb.ToString();
    }

    private IReadOnlyList<(string Type, int Count)> GetTypeDistribution()
    {
        var all  = AnalysisResult.CommanderCards.Concat(AnalysisResult.Cards).ToList();
        var dist = all.GroupBy(c => CardRoleDisplay.CardTypeBucket(c.Card))
                      .ToDictionary(g => g.Key, g => g.Count());

        var basics = AnalysisResult.BasicLandCounts.Values.Sum();
        if (basics > 0)
            dist["Land"] = dist.GetValueOrDefault("Land", 0) + basics;

        return TypeOrder
            .Where(t => dist.ContainsKey(t))
            .Select(t => (t, dist[t]))
            .ToList();
    }
}
