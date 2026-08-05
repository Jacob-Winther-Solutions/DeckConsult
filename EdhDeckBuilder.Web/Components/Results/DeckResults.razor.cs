using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
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

    private static string RoleName(CardRole role)              => CardRoleDisplay.RoleName(role);
    private static string PrimaryRoleBadgeClass(CardRole role) => CardRoleDisplay.PrimaryRoleBadgeClass(role);

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

    // ── Upgrade state ──────────────────────────────────────────────────────

    private string  _userFeedback         = "";
    private decimal? _maxUpgradePriceUsd;
    private bool    _isLoadingUpgrades;
    private bool    _upgradeStarted;
    private string? _upgradeCurrentStage;
    private string? _upgradeError;
    private DeckUpgradeResult? _upgradeResult;
    private CancellationTokenSource? _upgradeCts;

    private bool CanGetUpgrades => !_isLoadingUpgrades;

    private Task GetUpgradesAsync() => RunUpgradesAsync();

    private async Task RunUpgradesAsync()
    {
        _upgradeError        = null;
        _upgradeResult       = null;
        _upgradeCurrentStage = null;
        _isLoadingUpgrades   = true;
        _upgradeStarted      = true;
        _view                = DeckView.UpgradePaths;

        _upgradeCts = new CancellationTokenSource();
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            if (Keys.GetApiKey() is not null)
                Upgrader.UsageTracker = new UsageTracker();

            _upgradeResult = await Upgrader.UpgradeAsync(
                AnalysisResult,
                string.IsNullOrWhiteSpace(_userFeedback) ? null : _userFeedback.Trim(),
                _maxUpgradePriceUsd,
                null,
                async stage =>
                {
                    await InvokeAsync(() =>
                    {
                        _upgradeCurrentStage = stage;
                        StateHasChanged();
                    });
                    await Task.Yield();
                },
                _upgradeCts.Token);

            await InvokeAsync(() =>
            {
                _upgradeCurrentStage = null;
                _isLoadingUpgrades   = false;
                StateHasChanged();
            });
        }
        catch (OperationCanceledException)
        {
            await InvokeAsync(() => { _isLoadingUpgrades = false; _upgradeCurrentStage = null; StateHasChanged(); });
        }
        catch (ApiKeyRejectedException)
        {
            await InvokeAsync(() =>
            {
                _isLoadingUpgrades   = false;
                _upgradeCurrentStage = null;
                _upgradeError        = "Your API key was rejected — please reconnect.";
                ApiKeyState.NotifyChanged();
                StateHasChanged();
            });
        }
        catch (Exception ex)
        {
            await InvokeAsync(() =>
            {
                _isLoadingUpgrades   = false;
                _upgradeCurrentStage = null;
                _upgradeError        = ex.Message;
                StateHasChanged();
            });
        }
        finally
        {
            _upgradeCts?.Dispose();
            _upgradeCts = null;
        }
    }

    private void CancelUpgrades() => _upgradeCts?.Cancel();

    // ── Combo state ────────────────────────────────────────────────────────

    private bool              _isLoadingCombos;
    private bool              _comboStarted;
    private string?           _comboError;
    private ComboAnalysisResult? _comboResult;
    private CancellationTokenSource? _comboCts;
    private readonly HashSet<string> _expandedCombos = [];

    private Task FindCombosAsync() => RunCombosAsync();

    private async Task RunCombosAsync()
    {
        _comboError      = null;
        _comboResult     = null;
        _isLoadingCombos = true;
        _comboStarted    = true;
        _expandedCombos.Clear();
        _view = DeckView.Combos;

        _comboCts = new CancellationTokenSource();
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            _comboResult = await ComboFinder.FindCombosAsync(AnalysisResult, _comboCts.Token);
            await InvokeAsync(() => { _isLoadingCombos = false; StateHasChanged(); });
        }
        catch (OperationCanceledException)
        {
            await InvokeAsync(() => { _isLoadingCombos = false; StateHasChanged(); });
        }
        catch (Exception ex)
        {
            await InvokeAsync(() =>
            {
                _isLoadingCombos = false;
                _comboError      = ex.Message;
                StateHasChanged();
            });
        }
        finally
        {
            _comboCts?.Dispose();
            _comboCts = null;
        }
    }

    private void CancelCombos() => _comboCts?.Cancel();

    private void ToggleCombo(string id)
    {
        if (!_expandedCombos.Add(id)) _expandedCombos.Remove(id);
    }

    private static string BracketTagLabel(string? tag) => tag switch
    {
        "S" => "Spike (cEDH) — Bracket 5",
        "R" => "Ruthless — Bracket 4",
        "E" => "Escalated — Bracket 3",
        "P" => "Precon Appropriate — Bracket 2",
        "C" => "Casual — Bracket 1",
        _   => tag ?? "Unknown",
    };

    private static string BracketTagCss(string? tag) => tag switch
    {
        "S" => "bg-danger",
        "R" => "bg-warning text-dark",
        "E" => "bg-primary",
        _   => "bg-secondary",
    };

    // ── DeckAnalysisResult adapter ─────────────────────────────────────────

    private DeckAnalysisResult? _cachedAnalysis;
    private DeckAnalysisResult AnalysisResult => _cachedAnalysis ??= BuildAnalysisResult();

    private DeckAnalysisResult BuildAnalysisResult()
    {
        var commanderCards = Commanders
            .Select(c => new AnalyzedCard { Card = c, Roles = RoleProfile.Of(CardRole.Plan), IsCommander = true })
            .ToList();

        var deckCards = Result.Deck
            .Select(s => new AnalyzedCard { Card = s.Card, Roles = s.Roles, ClassifierReasoning = s.Reason })
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
        _upgradeCts?.Cancel();
        _upgradeCts?.Dispose();
        _comboCts?.Cancel();
        _comboCts?.Dispose();
    }

    private void OnApiKeyStateChanged() => InvokeAsync(StateHasChanged);
}
