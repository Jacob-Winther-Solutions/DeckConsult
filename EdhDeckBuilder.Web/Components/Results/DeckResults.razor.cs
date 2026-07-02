using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Results;

public partial class DeckResults
{
    [Parameter, EditorRequired] public required DeckBuildResult Result { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyList<Card> Commanders { get; set; }
    [Parameter] public Bracket Bracket { get; set; } = Bracket.Three;
    [Parameter] public decimal? MaxCardPriceUsd { get; set; }
    [Parameter] public decimal? TotalBudgetUsd { get; set; }

    private bool IsOverBudget(Card card) =>
        MaxCardPriceUsd.HasValue && card.PriceUsd.HasValue && card.PriceUsd > MaxCardPriceUsd;

    private static string FormatPrice(decimal? price) =>
        price.HasValue ? $"${price:F2}" : "—";

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

    private static readonly CardRole[] RoleDisplayOrder = CardRoleDisplay.DisplayOrder;
    private static readonly string[]   TypeOrder        = CardRoleDisplay.TypeOrder;

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string RoleName(CardRole role)            => CardRoleDisplay.RoleName(role);
    private static string PrimaryRoleBadgeClass(CardRole role) => CardRoleDisplay.PrimaryRoleBadgeClass(role);

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

    private Color CommanderColorIdentity =>
        Commanders.Aggregate(Color.None, (ci, c) => ci | c.ColorIdentity);

    private sealed record BadgeInfo(string CssClass, string Label, string Tooltip);
}
