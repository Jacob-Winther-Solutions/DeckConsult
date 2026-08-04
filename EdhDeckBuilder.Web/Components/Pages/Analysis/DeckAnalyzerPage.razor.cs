using EdhDeckBuilder.Agent.Analysis;
using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EdhDeckBuilder.Web.Components.Pages.Analysis;

public partial class DeckAnalyzerPage : ComponentBase, IDisposable
{
    [Inject] private IDeckAnalyzer          Analyzer        { get; set; } = default!;
    [Inject] private IDeckUpgrader          Upgrader        { get; set; } = default!;
    [Inject] private DecklistParser         Parser          { get; set; } = default!;
    [Inject] private ICardRepository        CardRepository  { get; set; } = default!;
    [Inject] private SessionApiKeyProvider  Keys            { get; set; } = default!;
    [Inject] private IApiKeyStateService    ApiKeyState     { get; set; } = default!;
    [Inject] private IJSRuntime             JS              { get; set; } = default!;

    private static readonly string[] AnalysisStages =
    [
        "Resolving card names",
        "Classifying cards",
        "Computing analysis",
    ];

    // ── Form state ──────────────────────────────────────────────────────────

    private IReadOnlyList<Card> _selectedCommanders = [];
    private string _decklistText = "";
    private string _userFeedback = "";

    // ── Validation state ────────────────────────────────────────────────────

    private bool _isValidating;
    private bool _validationDone;
    private int  _validationResolved;
    private IReadOnlyList<string> _validationUnresolved = [];

    // ── Run state ───────────────────────────────────────────────────────────

    private bool _isAnalyzing;
    private string? _currentStage;
    private string? _currentStageDetail;
    private List<string> _completedStages = new List<string>();
    private string? _errorMessage;
    private CancellationTokenSource? _cts;

    // ── Result ──────────────────────────────────────────────────────────────

    private DeckAnalysisResult? _result;
    private bool _copiedReport;

    // ── Upgrade state ────────────────────────────────────────────────────────

    private decimal? _maxUpgradePriceUsd;
    private bool     _isLoadingUpgrades;
    private bool     _upgradeStarted;
    private string?  _upgradeCurrentStage;
    private string?  _upgradeError;
    private DeckUpgradeResult? _upgradeResult;
    private CancellationTokenSource? _upgradeCts;

    protected override void OnInitialized()
    {
        ApiKeyState.OnChange += OnApiKeyStateChanged;
    }

    public void Dispose()
    {
        ApiKeyState.OnChange -= OnApiKeyStateChanged;
        _cts?.Cancel();
        _cts?.Dispose();
        _upgradeCts?.Cancel();
        _upgradeCts?.Dispose();
    }

    // ── View state ──────────────────────────────────────────────────────────

    private enum AnalysisView { ByRole, AllCards, ByType, UpgradePaths }
    private AnalysisView _view = AnalysisView.ByRole;

    private bool _showCoverage = true;
    private bool _showBasics   = true;
    private readonly HashSet<CardRole>  _collapsedBuckets     = [];
    private readonly HashSet<string>    _collapsedTypeBuckets = [];

    private void ToggleBucket(CardRole role)
    {
        if (!_collapsedBuckets.Add(role)) _collapsedBuckets.Remove(role);
    }

    private void ToggleTypeBucket(string name)
    {
        if (!_collapsedTypeBuckets.Add(name)) _collapsedTypeBuckets.Remove(name);
    }

    // ── Display helpers ─────────────────────────────────────────────────────

    private static readonly CardRole[] RoleDisplayOrder = CardRoleDisplay.DisplayOrder;
    private static readonly string[]   TypeOrder        = CardRoleDisplay.TypeOrder;

    private static string CardTypeBucket(Card card)
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

    private sealed record BadgeInfo(string CssClass, string Label, string Tooltip);

    private void OnApiKeyStateChanged() => InvokeAsync(StateHasChanged);

    private void OnCommandersChanged(IReadOnlyList<Card> commanders)
    {
        _selectedCommanders = commanders;
        _result = null;
        _upgradeResult = null;
        ClearValidation();
    }

    private void OnDecklistChanged(ChangeEventArgs e)
    {
        _decklistText = e.Value?.ToString() ?? "";
        ClearValidation();
    }

    private void ClearValidation()
    {
        _validationDone      = false;
        _validationResolved  = 0;
        _validationUnresolved = [];
    }

    private bool CanAnalyze =>
        _selectedCommanders.Count > 0 &&
        !string.IsNullOrWhiteSpace(_decklistText) &&
        !_isAnalyzing &&
        !_isValidating;

    private bool CanValidate =>
        _selectedCommanders.Count > 0 &&
        !string.IsNullOrWhiteSpace(_decklistText) &&
        !_isValidating &&
        !_isAnalyzing;

    private bool CanGetUpgrades =>
        _result is not null &&
        !_isLoadingUpgrades &&
        !_isAnalyzing;

    private async Task ValidateAsync()
    {
        _isValidating = true;
        _validationDone = false;
        StateHasChanged();

        var entries  = Parser.Parse(_decklistText);
        var resolved = 0;
        var unresolved = new List<string>();

        foreach (var entry in entries)
        {
            var card = await CardRepository.GetByNameAsync(entry.Name);
            if (card is not null)
                resolved++;
            else
                unresolved.Add(entry.Name);
        }

        _validationResolved   = resolved;
        _validationUnresolved = unresolved;
        _validationDone       = true;
        _isValidating         = false;
        StateHasChanged();
    }

    private async Task AnalyzeAsync()
    {
        _errorMessage = null;
        _result = null;
        _upgradeResult = null;
        _upgradeError = null;
        _completedStages = new List<string>();
        _currentStage = null;
        _currentStageDetail = null;
        _isAnalyzing = true;

        _cts = new CancellationTokenSource();

        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            var entries = Parser.Parse(_decklistText);

            if (Keys.GetApiKey() is not null)
                Analyzer.UsageTracker = new UsageTracker();

            _result = await Analyzer.AnalyzeAsync(
                _selectedCommanders,
                entries,
                async stage =>
                {
                    await InvokeAsync(() =>
                    {
                        if (_currentStage is not null)
                            _completedStages.Add(_currentStage);
                        _currentStage = stage;
                        _currentStageDetail = null;
                        StateHasChanged();
                    });
                    await Task.Yield();
                },
                async detail =>
                {
                    await InvokeAsync(() =>
                    {
                        _currentStageDetail = detail;
                        StateHasChanged();
                    });
                    await Task.Yield();
                },
                _cts.Token);

            await InvokeAsync(() =>
            {
                if (_currentStage is not null)
                    _completedStages.Add(_currentStage);
                _currentStage = null;
                _currentStageDetail = null;
                _isAnalyzing = false;
                StateHasChanged();
            });

            // Auto-run upgrades if the user provided feedback or a budget cap
            if (_result is not null &&
                (_maxUpgradePriceUsd.HasValue || !string.IsNullOrWhiteSpace(_userFeedback)))
            {
                _ = AutoRunUpgradesAsync();
            }
        }
        catch (OperationCanceledException)
        {
            await InvokeAsync(() =>
            {
                _isAnalyzing = false;
                _completedStages = new List<string>();
                _currentStage = null;
                _currentStageDetail = null;
                StateHasChanged();
            });
        }
        catch (ApiKeyRejectedException)
        {
            await InvokeAsync(() =>
            {
                _isAnalyzing = false;
                _completedStages = new List<string>();
                _currentStage = null;
                _currentStageDetail = null;
                _errorMessage = "Your API key was rejected — please reconnect.";
                ApiKeyState.NotifyChanged();
                StateHasChanged();
            });
        }
        catch (Exception ex)
        {
            await InvokeAsync(() =>
            {
                _isAnalyzing = false;
                _completedStages = new List<string>();
                _currentStage = null;
                _currentStageDetail = null;
                _errorMessage = ex.Message;
                StateHasChanged();
            });
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelAnalysis() => _cts?.Cancel();

    // Called from onclick — switches to the Upgrade Paths tab and runs
    private Task GetUpgradesAsync() => RunUpgradesAsync(switchView: true);

    // Called automatically after analysis when the user pre-filled upgrade params
    private Task AutoRunUpgradesAsync() => RunUpgradesAsync(switchView: false);

    private async Task RunUpgradesAsync(bool switchView)
    {
        if (_result is null) return;

        _upgradeError = null;
        _upgradeResult = null;
        _upgradeCurrentStage = null;
        _isLoadingUpgrades = true;
        _upgradeStarted = true;
        if (switchView)
            _view = AnalysisView.UpgradePaths;

        _upgradeCts = new CancellationTokenSource();

        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            if (Keys.GetApiKey() is not null)
                Upgrader.UsageTracker = new UsageTracker();

            _upgradeResult = await Upgrader.UpgradeAsync(
                _result,
                string.IsNullOrWhiteSpace(_userFeedback) ? null : _userFeedback.Trim(),
                _maxUpgradePriceUsd,
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
                _isLoadingUpgrades = false;
                StateHasChanged();
            });
        }
        catch (OperationCanceledException)
        {
            await InvokeAsync(() =>
            {
                _isLoadingUpgrades = false;
                _upgradeCurrentStage = null;
                StateHasChanged();
            });
        }
        catch (ApiKeyRejectedException)
        {
            await InvokeAsync(() =>
            {
                _isLoadingUpgrades = false;
                _upgradeCurrentStage = null;
                _upgradeError = "Your API key was rejected — please reconnect.";
                ApiKeyState.NotifyChanged();
                StateHasChanged();
            });
        }
        catch (Exception ex)
        {
            await InvokeAsync(() =>
            {
                _isLoadingUpgrades = false;
                _upgradeCurrentStage = null;
                _upgradeError = ex.Message;
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

    private void Reset()
    {
        _result = null;
        _upgradeResult = null;
        _upgradeError = null;
        _upgradeCurrentStage = null;
        _upgradeStarted = false;
        _errorMessage = null;
        _completedStages = new List<string>();
        _currentStage = null;
        _currentStageDetail = null;
        _view = AnalysisView.ByRole;
        ClearValidation();
    }

    private async Task DownloadReportAsync()
    {
        if (_result is null) return;
        var date = DateOnly.FromDateTime(DateTime.Today);
        var text = DeckReportExporter.ExportAnalysis(_result, date);
        var filename = DeckReportExporter.SlugifyFilename(_result.Commanders) + "-analysis.md";
        await JS.InvokeVoidAsync("downloadTextFile", filename, text, "text/markdown");
    }

    private async Task CopyReportAsync()
    {
        if (_result is null) return;
        try
        {
            var date = DateOnly.FromDateTime(DateTime.Today);
            var text = DeckReportExporter.ExportAnalysis(_result, date);
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", text);
            _copiedReport = true;
            StateHasChanged();
            await Task.Delay(2000);
            _copiedReport = false;
            StateHasChanged();
        }
        catch { /* clipboard unavailable */ }
    }
}
