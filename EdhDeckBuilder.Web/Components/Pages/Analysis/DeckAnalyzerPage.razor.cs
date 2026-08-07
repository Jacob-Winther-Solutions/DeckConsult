using EdhDeckBuilder.Agent.Analysis;
using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Components.Results;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EdhDeckBuilder.Web.Components.Pages.Analysis;

public partial class DeckAnalyzerPage : ComponentBase, IDisposable
{
    [Inject] private IDeckAnalyzer          Analyzer          { get; set; } = default!;
    [Inject] private IDeckUpgrader          Upgrader          { get; set; } = default!;
    [Inject] private IComboFinder           ComboFinder       { get; set; } = default!;
    [Inject] private DecklistParser         Parser            { get; set; } = default!;
    [Inject] private ICardRepository        CardRepository    { get; set; } = default!;
    [Inject] private ISuggestionSource      SuggestionSource  { get; set; } = default!;
    [Inject] private CreatureTypeCatalog    CreatureTypes     { get; set; } = default!;
    [Inject] private SessionApiKeyProvider  Keys              { get; set; } = default!;
    [Inject] private IApiKeyStateService    ApiKeyState       { get; set; } = default!;
    [Inject] private IJSRuntime             JS                { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "load")]
    public string? LoadId { get; set; }

    private bool _triedLoad;

    private static readonly string[] AnalysisStages =
    [
        "Resolving card names",
        "Classifying cards",
        "Computing analysis",
    ];

    // ── Form state ──────────────────────────────────────────────────────────

    private IReadOnlyList<Card> _selectedCommanders = [];
    private string _decklistText = "";

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
    private IReadOnlyList<(string Slug, string Name, int Count, Theme? KnownTheme, Archetype? KnownArchetype)> _popularThemes = [];
    private HashSet<string> _tribeSlugSet = [];

    // ── Combo state ──────────────────────────────────────────────────────────

    private CombosPanel?         _comboPanel;
    private ComboAnalysisResult? _comboResult;

    // ── Upgrade state ────────────────────────────────────────────────────────

    private UpgradePathsPanel? _upgradePanel;
    private DeckUpgradeResult? _upgradeResult;

    protected override void OnInitialized()
    {
        ApiKeyState.OnChange += OnApiKeyStateChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _triedLoad || string.IsNullOrEmpty(LoadId)) return;
        _triedLoad = true;

        try
        {
            var json = await JS.InvokeAsync<string?>("getLocalStorage", AnalysisResultStorage.LocalStorageKey(LoadId));
            if (string.IsNullOrEmpty(json)) return;
            var stored = AnalysisResultStorage.Deserialize(json);
            if (stored is null) return;
            _result = stored.Result;
            _selectedCommanders = stored.Result.Commanders;
            await InvokeAsync(StateHasChanged);
        }
        catch { /* localStorage unavailable or JSON malformed */ }
    }

    public void Dispose()
    {
        ApiKeyState.OnChange -= OnApiKeyStateChanged;
        _cts?.Cancel();
        _cts?.Dispose();
    }

    // ── Custom targets ──────────────────────────────────────────────────────

    private readonly Dictionary<CardRole, int> _customIdeal = new();
    private bool _editingTargets;

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

    // ── View state ──────────────────────────────────────────────────────────

    private enum AnalysisView { ByRole, AllCards, ByType, ByManaValue, UpgradePaths, Combos }
    private AnalysisView _view = AnalysisView.ByRole;

    // ── Display helpers ─────────────────────────────────────────────────────

    private static CardRoleDisplay.BadgeInfo SecondaryBadge(RoleContribution contrib) => CardRoleDisplay.SecondaryBadge(contrib);
    private static string BracketTagLabel(string? tag) => CardRoleDisplay.BracketTagLabel(tag);
    private static string BracketTagCss(string? tag)   => CardRoleDisplay.BracketTagCss(tag);

    private bool IsPrimary((string Slug, string Name, int Count, Theme? KnownTheme, Archetype? KnownArchetype) t)
        => t.KnownTheme.HasValue || t.KnownArchetype.HasValue || _tribeSlugSet.Contains(t.Slug);

    private void OnApiKeyStateChanged() => InvokeAsync(StateHasChanged);

    private void OnCommandersChanged(IReadOnlyList<Card> commanders)
    {
        _selectedCommanders = commanders;
        _result = null;
        _upgradeResult = null;
        _popularThemes = [];
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
        _popularThemes = [];
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

            if (_result is not null && _selectedCommanders.Count > 0)
            {
                try
                {
                    _popularThemes = await SuggestionSource.GetPopularThemesAsync(
                        _selectedCommanders[0], _cts?.Token ?? CancellationToken.None);
                    if (_tribeSlugSet.Count == 0)
                    {
                        var types = await CreatureTypes.GetTypesAsync();
                        _tribeSlugSet = types.Select(t => t.ToLowerInvariant().Replace(' ', '-')).ToHashSet();
                    }
                }
                catch { _popularThemes = []; }
                await InvokeAsync(StateHasChanged);
            }

            if (_result is not null)
            {
                var id = Guid.NewGuid().ToString("N");
                var stored = new StoredAnalysisResult(_result, DateOnly.FromDateTime(DateTime.UtcNow));
                try
                {
                    await JS.InvokeVoidAsync("saveAnalysisResult",
                        AnalysisResultStorage.LocalStorageKey(id),
                        AnalysisResultStorage.Serialize(stored),
                        AnalysisResultStorage.DefaultMaxSavedResults);
                }
                catch { /* localStorage unavailable */ }
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
        catch (QuotaExceededException ex)
        {
            await InvokeAsync(() =>
            {
                _isAnalyzing = false;
                _completedStages = new List<string>();
                _currentStage = null;
                _currentStageDetail = null;
                _errorMessage = LlmErrorMessages.ForQuotaException(ex);
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


    private void Reset()
    {
        _customIdeal.Clear();
        _editingTargets = false;
        _result = null;
        _upgradeResult = null;
        _comboResult = null;
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
