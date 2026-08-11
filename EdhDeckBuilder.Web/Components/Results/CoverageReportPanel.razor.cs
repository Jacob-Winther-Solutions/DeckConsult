using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Prompts;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Results;

/// <summary>Context passed to <see cref="CoverageReportPanel.TargetCellContent"/> for each role row.</summary>
public record TargetCellContext(CardRole Role, RoleTarget Target);

public partial class CoverageReportPanel : ComponentBase
{
    // ── Required data ──────────────────────────────────────────────────────

    [Parameter, EditorRequired] public required IReadOnlyList<AnalyzedCard> Cards { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyDictionary<CardRole, double> ActualCoverage { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyDictionary<string, int> BasicLandCounts { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyDictionary<CardRole, RoleTarget> Targets { get; set; }

    // ── Coverage summary customization ─────────────────────────────────────

    [Parameter] public string SummaryTitle { get; set; } = "Coverage Summary";
    [Parameter] public string TargetsColumnLabel { get; set; } = "Planned (min–ideal–max)";

    /// <summary>Added to the Land row's actual coverage in the summary table only.
    /// Pass <c>BasicLandCounts.Values.Sum()</c> when the source's ActualCoverage excludes basic lands.</summary>
    [Parameter] public int ExtraLandCoverage { get; set; } = 0;

    /// <summary>Extra buttons rendered in the coverage summary card header (e.g. Customize / Reset).</summary>
    [Parameter] public RenderFragment? SummaryHeaderActions { get; set; }

    /// <summary>Content rendered inside the summary card body, before the table (e.g. a descriptive paragraph).</summary>
    [Parameter] public RenderFragment? SummaryBodyPrefix { get; set; }

    /// <summary>When provided, replaces the default min–ideal–max cell for each role row.
    /// The delegate must render exactly one &lt;td&gt; element.</summary>
    [Parameter] public RenderFragment<TargetCellContext>? TargetCellContent { get; set; }

    // ── Alerts ─────────────────────────────────────────────────────────────

    /// <summary>Content rendered between the coverage summary and the role buckets (warnings, gaps, violations).</summary>
    [Parameter] public RenderFragment? Alerts { get; set; }

    // ── Role bucket extras ─────────────────────────────────────────────────

    [Parameter] public decimal? MaxCardPriceUsd { get; set; }

    /// <summary>Oracle ID → rank. When provided, primary cards sort by rank and show the rank column.</summary>
    [Parameter] public IReadOnlyDictionary<Guid, int>? CardRanks { get; set; }

    /// <summary>Per-role set of oracle IDs marked as cut suggestions.</summary>
    [Parameter] public IReadOnlyDictionary<CardRole, IReadOnlyCollection<Guid>>? CutOracleIds { get; set; }

    // ── Runner-ups ─────────────────────────────────────────────────────────

    [Parameter] public IReadOnlyList<CardCandidate>? RunnerUps { get; set; }

    // ── View state ─────────────────────────────────────────────────────────

    private bool _showCoverage  = true;
    private bool _showBasics    = true;
    private bool _showRunnerUps = false;
    private readonly HashSet<CardRole> _collapsedBuckets = [];

    private static readonly CardRole[] RoleDisplayOrder = CardRoleDisplay.DisplayOrder;

    private void ToggleBucket(CardRole role)
    {
        if (!_collapsedBuckets.Add(role)) _collapsedBuckets.Remove(role);
    }

    private bool IsOverBudget(AnalyzedCard card) =>
        MaxCardPriceUsd.HasValue && card.Card.PriceUsd.HasValue && !card.IsLocked && card.Card.PriceUsd > MaxCardPriceUsd;

    private static string RoleName(CardRole role)              => CardRoleDisplay.RoleName(role);
    private static string PrimaryRoleBadgeClass(CardRole role) => CardRoleDisplay.PrimaryRoleBadgeClass(role);
    private static CardRoleDisplay.BadgeInfo SecondaryBadge(RoleContribution contrib) => CardRoleDisplay.SecondaryBadge(contrib);
    private static string? RoleTooltip(CardRole role) =>
        CardRoleGlossary.Definitions.TryGetValue(role, out var def) ? def.Description : null;
}
