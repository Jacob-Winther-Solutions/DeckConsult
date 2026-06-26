using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components;

public partial class DeckResults
{
    [Parameter, EditorRequired] public required DeckBuildResult Result { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyList<Card> Commanders { get; set; }
    [Parameter] public Bracket Bracket { get; set; } = Bracket.Three;

    // ── Collapse state ─────────────────────────────────────────────────────

    private bool _showCoverage  = true;
    private bool _showBasics    = true;
    private bool _showRunnerUps = false;
    private readonly HashSet<CardRole> _collapsedBuckets = [];

    private void ToggleBucket(CardRole role)
    {
        if (!_collapsedBuckets.Add(role)) _collapsedBuckets.Remove(role);
    }

    // ── Display order ──────────────────────────────────────────────────────

    private static readonly CardRole[] RoleDisplayOrder =
    [
        CardRole.Plan,
        CardRole.Ramp,
        CardRole.CardAdvantage,
        CardRole.TargetedDisruption,
        CardRole.MassDisruption,
        CardRole.Tutor,
        CardRole.Protection,
        CardRole.Recursion,
        CardRole.Synergy,
        CardRole.Payoff,
        CardRole.Land,
        CardRole.Unclassified,
    ];

    // ── Helpers ────────────────────────────────────────────────────────────

    internal static string RoleName(CardRole role) => role switch
    {
        CardRole.Land               => "Lands (Utility)",
        CardRole.Ramp               => "Ramp",
        CardRole.CardAdvantage      => "Card Advantage",
        CardRole.TargetedDisruption => "Targeted Disruption",
        CardRole.MassDisruption     => "Mass Disruption",
        CardRole.Protection         => "Protection",
        CardRole.Tutor              => "Tutors",
        CardRole.Recursion          => "Recursion",
        CardRole.Plan               => "Plan",
        CardRole.Payoff             => "Payoffs",
        CardRole.Synergy            => "Synergy",
        CardRole.Unclassified       => "Unclassified",
        _                           => role.ToString(),
    };

    private static BadgeInfo SecondaryBadge(RoleContribution contrib)
    {
        var name = RoleName(contrib.Role);
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

    private IEnumerable<ColorPip> CommanderColorPips()
    {
        var identity = Commanders.Aggregate(Color.None, (ci, c) => ci | c.ColorIdentity);
        return GetColorPips(identity);
    }

    private static IEnumerable<ColorPip> GetColorPips(Color identity)
    {
        if (identity == Color.None)
            yield return new("C", "badge bg-secondary", "");
        if (identity.HasFlag(Color.White))
            yield return new("W", "badge border", "background:#f9fafb;color:#555;");
        if (identity.HasFlag(Color.Blue))
            yield return new("U", "badge bg-primary", "");
        if (identity.HasFlag(Color.Black))
            yield return new("B", "badge bg-dark", "");
        if (identity.HasFlag(Color.Red))
            yield return new("R", "badge bg-danger", "");
        if (identity.HasFlag(Color.Green))
            yield return new("G", "badge bg-success", "");
    }

    private sealed record ColorPip(string Symbol, string BadgeClass, string Style);
    private sealed record BadgeInfo(string CssClass, string Label, string Tooltip);
}
