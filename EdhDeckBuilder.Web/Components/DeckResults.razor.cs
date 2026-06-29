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

    // ── View state ─────────────────────────────────────────────────────────

    private enum DeckView { ByRole, AllCards, ByType }
    private DeckView _view = DeckView.ByRole;

    private bool _showCoverage  = true;
    private bool _showBasics    = true;
    private bool _showRunnerUps = false;
    private readonly HashSet<CardRole>  _collapsedBuckets     = [];
    private readonly HashSet<string>    _collapsedTypeBuckets = [];

    private void ToggleBucket(CardRole role)
    {
        if (!_collapsedBuckets.Add(role)) _collapsedBuckets.Remove(role);
    }

    private void ToggleTypeBucket(string typeName)
    {
        if (!_collapsedTypeBuckets.Add(typeName)) _collapsedTypeBuckets.Remove(typeName);
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

    private static readonly string[] TypeOrder =
    [
        "Creature", "Planeswalker", "Instant", "Sorcery",
        "Artifact", "Enchantment", "Battle", "Land", "Other",
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

    internal static string PrimaryRoleBadgeClass(CardRole role) => role switch
    {
        CardRole.Plan               => "bg-primary",
        CardRole.Ramp               => "bg-success",
        CardRole.CardAdvantage      => "bg-info text-dark",
        CardRole.TargetedDisruption => "bg-danger",
        CardRole.MassDisruption     => "bg-warning text-dark",
        CardRole.Protection         => "bg-secondary",
        CardRole.Tutor              => "bg-dark",
        CardRole.Recursion          => "bg-success bg-opacity-50 text-dark",
        CardRole.Payoff             => "bg-primary bg-opacity-50 text-dark",
        CardRole.Synergy            => "bg-info bg-opacity-50 text-dark",
        CardRole.Land               => "bg-success bg-opacity-75",
        _                           => "bg-secondary",
    };

    internal static string CardTypeBucket(Card card)
    {
        var tl = card.TypeLine;
        if (tl.Contains("Creature",     StringComparison.OrdinalIgnoreCase)) return "Creature";
        if (tl.Contains("Planeswalker", StringComparison.OrdinalIgnoreCase)) return "Planeswalker";
        if (tl.Contains("Instant",      StringComparison.OrdinalIgnoreCase)) return "Instant";
        if (tl.Contains("Sorcery",      StringComparison.OrdinalIgnoreCase)) return "Sorcery";
        if (tl.Contains("Artifact",     StringComparison.OrdinalIgnoreCase)) return "Artifact";
        if (tl.Contains("Enchantment",  StringComparison.OrdinalIgnoreCase)) return "Enchantment";
        if (tl.Contains("Battle",       StringComparison.OrdinalIgnoreCase)) return "Battle";
        if (tl.Contains("Land",         StringComparison.OrdinalIgnoreCase)) return "Land";
        return "Other";
    }

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
