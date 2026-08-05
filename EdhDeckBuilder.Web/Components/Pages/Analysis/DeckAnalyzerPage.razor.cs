using EdhDeckBuilder.Agent.Analysis;
using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EdhDeckBuilder.Web.Components.Pages.Analysis;

public partial class DeckAnalyzerPage : ComponentBase, IDisposable
{
    [Inject] private IDeckAnalyzer          Analyzer        { get; set; } = default!;
    [Inject] private IDeckUpgrader          Upgrader        { get; set; } = default!;
    [Inject] private IComboFinder           ComboFinder     { get; set; } = default!;
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

    // ── Combo state ──────────────────────────────────────────────────────────

    private bool              _isLoadingCombos;
    private bool              _comboStarted;
    private string?           _comboError;
    private ComboAnalysisResult? _comboResult;
    private CancellationTokenSource? _comboCts;
    private readonly HashSet<string> _expandedCombos = [];

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
        _comboCts?.Cancel();
        _comboCts?.Dispose();
    }

    // ── Custom targets ──────────────────────────────────────────────────────

    private readonly Dictionary<CardRole, int> _customIdeal = new();
    private bool _editingTargets;
    private bool _showTargetEditor;

    private IReadOnlyDictionary<CardRole, RoleTarget> GetEffectiveTargets()
    {
        if (_customIdeal.Count == 0) return DeckTemplate.Balanced.Targets;
        return DeckTemplate.Balanced.Targets.ToDictionary(
            kv => kv.Key,
            kv =>
            {
                if (!_customIdeal.TryGetValue(kv.Key, out var ideal)) return kv.Value;
                var minDelta = kv.Value.Ideal - kv.Value.Min;
                var maxDelta = kv.Value.Max - kv.Value.Ideal;
                return new RoleTarget(Math.Max(0, ideal - minDelta), ideal, ideal + maxDelta);
            });
    }

    private IReadOnlyList<RoleGap> GetCurrentGaps()
    {
        if (_result is null) return [];
        var targets = GetEffectiveTargets();
        return targets
            .Where(kv => kv.Key != CardRole.Land)
            .Select(kv => (Role: kv.Key, Target: kv.Value, Actual: _result.ActualCoverage.GetValueOrDefault(kv.Key)))
            .Where(x => x.Actual < x.Target.Min)
            .Select(x => new RoleGap
            {
                Role           = x.Role,
                ActualCoverage = x.Actual,
                IdealTarget    = x.Target.Ideal,
                Shortfall      = x.Target.Ideal - x.Actual,
            })
            .OrderByDescending(g => g.Shortfall)
            .ToList();
    }

    private void SetCustomIdeal(CardRole role, object? value)
    {
        if (!int.TryParse(value?.ToString(), out var ideal) || ideal < 0) return;
        if (DeckTemplate.Balanced.Targets.TryGetValue(role, out var t) && t.Ideal == ideal)
            _customIdeal.Remove(role);
        else
            _customIdeal[role] = ideal;
    }

    private RenderFragment RoleTargetEditor => __builder =>
    {
        __builder.OpenElement(0, "div");
        __builder.AddAttribute(1, "class", "border-top mt-3 pt-3");

        __builder.OpenElement(2, "div");
        __builder.AddAttribute(3, "class", "d-flex align-items-center gap-2 mb-1");

        __builder.OpenElement(4, "button");
        __builder.AddAttribute(5, "class", $"btn btn-sm btn-link p-0 {(_showTargetEditor ? "text-primary" : "text-muted")}");
        __builder.AddAttribute(6, "onclick", EventCallback.Factory.Create(this, () => _showTargetEditor = !_showTargetEditor));
        __builder.AddContent(7, _showTargetEditor ? "▲ Hide role targets" : "▼ Adjust role targets");
        __builder.CloseElement();

        if (_customIdeal.Count > 0)
        {
            __builder.OpenElement(8, "span");
            __builder.AddAttribute(9, "class", "badge bg-primary");
            __builder.AddAttribute(10, "style", "font-size: 0.65rem;");
            __builder.AddContent(11, $"{_customIdeal.Count} custom");
            __builder.CloseElement();
        }

        __builder.CloseElement(); // d-flex

        if (_showTargetEditor)
        {
            __builder.OpenElement(12, "p");
            __builder.AddAttribute(13, "class", "text-muted small mt-1 mb-2");
            __builder.AddContent(14,
                "Set the ideal count for each role. A higher ideal creates a larger gap and makes that role " +
                "more likely to appear in upgrade suggestions. Changes here are also reflected in the Coverage tab.");
            __builder.CloseElement();

            __builder.OpenElement(15, "div");
            __builder.AddAttribute(16, "class", "row row-cols-2 row-cols-sm-3 g-2 mb-2");

            // Sequence numbers 17–28 are used per-iteration (constant within loop = correct for Blazor diffing)
            foreach (var role in RoleDisplayOrder)
            {
                if (!DeckTemplate.Balanced.Targets.TryGetValue(role, out var baseTarget)) continue;
                var effectiveIdeal = _customIdeal.TryGetValue(role, out var ci) ? ci : baseTarget.Ideal;
                var isCustom = _customIdeal.ContainsKey(role);
                var capturedRole = role;

                __builder.OpenElement(17, "div");
                __builder.AddAttribute(18, "class", "col");

                __builder.OpenElement(19, "label");
                __builder.AddAttribute(20, "class", $"form-label small mb-1 {(isCustom ? "text-primary fw-semibold" : "text-muted")}");
                __builder.AddContent(21, CardRoleDisplay.RoleName(role) + (isCustom ? " *" : ""));
                __builder.CloseElement();

                __builder.OpenElement(22, "input");
                __builder.AddAttribute(23, "type", "number");
                __builder.AddAttribute(24, "class", "form-control form-control-sm");
                __builder.AddAttribute(25, "min", "0");
                __builder.AddAttribute(26, "max", "40");
                __builder.AddAttribute(27, "value", effectiveIdeal);
                __builder.AddAttribute(28, "onchange",
                    EventCallback.Factory.Create<Microsoft.AspNetCore.Components.ChangeEventArgs>(
                        this, e => SetCustomIdeal(capturedRole, e.Value)));
                __builder.CloseElement();

                __builder.CloseElement(); // col
            }

            __builder.CloseElement(); // row

            if (_customIdeal.Count > 0)
            {
                __builder.OpenElement(29, "button");
                __builder.AddAttribute(30, "class", "btn btn-sm btn-link text-danger p-0");
                __builder.AddAttribute(31, "onclick",
                    EventCallback.Factory.Create(this, () => _customIdeal.Clear()));
                __builder.AddContent(32, "Reset to defaults");
                __builder.CloseElement();
            }
        }

        __builder.CloseElement(); // border-top div
    };

    // ── View state ──────────────────────────────────────────────────────────

    private enum AnalysisView { ByRole, AllCards, ByType, UpgradePaths, Combos }
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

    private Task GetUpgradesAsync() => RunUpgradesAsync(switchView: true);

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
                _customIdeal.Count > 0 ? GetEffectiveTargets() : null,
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
        _customIdeal.Clear();
        _editingTargets = false;
        _showTargetEditor = false;
        _result = null;
        _upgradeResult = null;
        _upgradeError = null;
        _upgradeCurrentStage = null;
        _upgradeStarted = false;
        _comboResult = null;
        _comboError = null;
        _comboStarted = false;
        _expandedCombos.Clear();
        _errorMessage = null;
        _completedStages = new List<string>();
        _currentStage = null;
        _currentStageDetail = null;
        _view = AnalysisView.ByRole;
        ClearValidation();
    }

    private Task FindCombosAsync() => RunCombosAsync(switchView: true);

    private async Task RunCombosAsync(bool switchView)
    {
        if (_result is null) return;

        _comboError   = null;
        _comboResult  = null;
        _isLoadingCombos = true;
        _comboStarted = true;
        _expandedCombos.Clear();
        if (switchView)
            _view = AnalysisView.Combos;

        _comboCts = new CancellationTokenSource();

        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            _comboResult = await ComboFinder.FindCombosAsync(_result, _comboCts.Token);
            await InvokeAsync(() =>
            {
                _isLoadingCombos = false;
                StateHasChanged();
            });
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
                _comboError = ex.Message;
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
