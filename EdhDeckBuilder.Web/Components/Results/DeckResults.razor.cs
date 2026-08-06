using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Results;

public partial class DeckResults : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required DeckBuildResult Result { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyList<Card> Commanders { get; set; }
    [Parameter] public Bracket Bracket { get; set; } = Bracket.Three;
    [Parameter] public decimal? MaxCardPriceUsd { get; set; }
    [Parameter] public decimal? TotalBudgetUsd { get; set; }

    [Inject] private IDeckUpgrader         Upgrader    { get; set; } = default!;
    [Inject] private IComboFinder          ComboFinder { get; set; } = default!;
    [Inject] private SessionApiKeyProvider Keys        { get; set; } = default!;
    [Inject] private IApiKeyStateService   ApiKeyState { get; set; } = default!;

    private bool IsOverBudget(Card card) =>
        MaxCardPriceUsd.HasValue && card.PriceUsd.HasValue && card.PriceUsd > MaxCardPriceUsd;

    private static string FormatPrice(decimal? price) =>
        price.HasValue ? $"${price:F2}" : "—";

    // ── View state ─────────────────────────────────────────────────────────

    private enum DeckView { ByRole, AllCards, ByType, ByManaValue, UpgradePaths, Combos }
    private DeckView _view = DeckView.AllCards;

    private bool _showCoverage  = true;
    private bool _showBasics    = true;
    private bool _showRunnerUps = false;
    private readonly HashSet<CardRole> _collapsedBuckets = [];

    private void ToggleBucket(CardRole role)
    {
        if (!_collapsedBuckets.Add(role)) _collapsedBuckets.Remove(role);
    }

    // ── Display order ──────────────────────────────────────────────────────

    private static readonly CardRole[] RoleDisplayOrder = CardRoleDisplay.DisplayOrder;

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string RoleName(CardRole role)              => CardRoleDisplay.RoleName(role);
    private static string PrimaryRoleBadgeClass(CardRole role) => CardRoleDisplay.PrimaryRoleBadgeClass(role);
    private static CardRoleDisplay.BadgeInfo SecondaryBadge(RoleContribution contrib) => CardRoleDisplay.SecondaryBadge(contrib);
    private static string BracketTagLabel(string? tag) => CardRoleDisplay.BracketTagLabel(tag);
    private static string BracketTagCss(string? tag)   => CardRoleDisplay.BracketTagCss(tag);

    private Color CommanderColorIdentity =>
        Commanders.Aggregate(Color.None, (ci, c) => ci | c.ColorIdentity);

    // ── Upgrade state ──────────────────────────────────────────────────────

    private UpgradePathsPanel? _upgradePanel;
    private DeckUpgradeResult? _upgradeResult;

    // ── Combo state ────────────────────────────────────────────────────────

    private CombosPanel?         _comboPanel;
    private ComboAnalysisResult? _comboResult;

    // ── DeckAnalysisResult adapter ─────────────────────────────────────────

    private DeckAnalysisResult? _cachedAnalysis;
    private DeckAnalysisResult AnalysisResult => _cachedAnalysis ??= BuildAnalysisResult();

    private DeckAnalysisResult BuildAnalysisResult()
    {
        var commanderCards = Commanders
            .Select(c => new AnalyzedCard { Card = c, Roles = RoleProfile.Of(CardRole.Plan), IsCommander = true })
            .ToList();

        var deckCards = Result.Deck
            .Select(s => new AnalyzedCard { Card = s.Card, Roles = s.Roles, ClassifierReasoning = s.Reason, IsLocked = s.IsLocked })
            .ToList();

        var gaps = Result.PlannedTemplate.Targets
            .Where(kv => kv.Key != CardRole.Land)
            .Select(kv =>
            {
                var actual    = Result.ActualCoverage.GetValueOrDefault(kv.Key, 0.0);
                var shortfall = Math.Max(0.0, kv.Value.Ideal - actual);
                return new RoleGap
                {
                    Role           = kv.Key,
                    ActualCoverage = actual,
                    IdealTarget    = kv.Value.Ideal,
                    Shortfall      = shortfall,
                };
            })
            .Where(g => g.Shortfall > 0)
            .OrderByDescending(g => g.Shortfall)
            .ToList();

        return new DeckAnalysisResult
        {
            Commanders             = Commanders,
            CommanderCards         = commanderCards,
            Cards                  = deckCards,
            BasicLandCounts        = Result.BasicLandCounts,
            ActualCoverage         = Result.ActualCoverage,
            SpellbookBracketTag    = null,
            SpellbookBracket       = null,
            RoleGaps               = gaps,
            UnresolvedNames        = [],
            ColorIdentityViolations = [],
            TotalPriceUsd          = Result.TotalPriceUsd,
        };
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        ApiKeyState.OnChange += OnApiKeyStateChanged;
    }

    public void Dispose()
    {
        ApiKeyState.OnChange -= OnApiKeyStateChanged;
    }

    private void OnApiKeyStateChanged() => InvokeAsync(StateHasChanged);
}
