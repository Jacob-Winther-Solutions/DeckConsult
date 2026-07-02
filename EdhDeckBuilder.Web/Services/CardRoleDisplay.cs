using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Web.Services;

/// <summary>
/// Shared display helpers for CardRole — used by DeckResults, DeckReportExporter,
/// and form components (ThemePicker, custom template editor).
/// </summary>
public static class CardRoleDisplay
{
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
