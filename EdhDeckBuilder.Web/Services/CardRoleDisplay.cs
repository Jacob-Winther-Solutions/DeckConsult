using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Web.Services;

public static class CardRoleDisplay
{
    public sealed record BadgeInfo(string CssClass, string Label, string Tooltip);

    public static BadgeInfo SecondaryBadge(RoleContribution contrib)
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

    public static string BracketTagLabel(string? tag) => tag switch
    {
        "S" => "Spike (cEDH) — Bracket 5",
        "R" => "Ruthless — Bracket 4",
        "E" => "Escalated — Bracket 3",
        "P" => "Precon Appropriate — Bracket 2",
        "C" => "Casual — Bracket 1",
        _   => tag ?? "Unknown",
    };

    public static string BracketTagCss(string? tag) => tag switch
    {
        "S" => "bg-danger",
        "R" => "bg-warning text-dark",
        "E" => "bg-primary",
        _   => "bg-secondary",
    };

    public static readonly CardRole[] DisplayOrder =
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
        CardRole.Unmatched,
    ];

    public static readonly string[] TypeOrder =
    [
        "Creature", "Planeswalker", "Instant", "Sorcery",
        "Artifact", "Enchantment", "Battle", "Land", "Other",
    ];

    /// <summary>Display name used in deck results and reports.</summary>
    public static string RoleName(CardRole role) => role switch
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
        CardRole.Unmatched          => "Unmatched",
        _                           => role.ToString(),
    };

    /// <summary>Shorter label used in form inputs (theme editor, custom template).</summary>
    public static string FormLabel(CardRole role) => role switch
    {
        CardRole.Land               => "Lands",
        CardRole.Ramp               => "Ramp",
        CardRole.CardAdvantage      => "Card Advantage",
        CardRole.TargetedDisruption => "Targeted Disruption",
        CardRole.MassDisruption     => "Mass Disruption",
        CardRole.Tutor              => "Tutors",
        CardRole.Protection         => "Protection",
        CardRole.Recursion          => "Recursion",
        CardRole.Plan               => "Plan",
        CardRole.Payoff             => "Payoff",
        CardRole.Synergy            => "Synergy",
        _                           => role.ToString(),
    };

    public static string CardTypeBucket(Card card)
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

    public static string PrimaryRoleBadgeClass(CardRole role) => role switch
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
}
