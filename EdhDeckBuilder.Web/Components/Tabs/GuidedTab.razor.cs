using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace EdhDeckBuilder.Web.Components.Tabs;

public partial class GuidedTab : ComponentBase, IDisposable
{
    [Inject] private IDeckBuilder          DeckBuilder { get; set; } = default!;
    [Inject] private SessionApiKeyProvider Keys        { get; set; } = default!;
    [Inject] private IJSRuntime            JS          { get; set; } = default!;
    [Inject] private IApiKeyStateService   ApiKeyState { get; set; } = default!;
    [Inject] private NavigationManager     Navigation  { get; set; } = default!;
    [Inject] private DeckResultStore       ResultStore { get; set; } = default!;
    [Inject] private IConfiguration        Config      { get; set; } = default!;

    [Parameter] public Card? InitialCommander { get; set; }
    [Parameter] public IReadOnlyDictionary<Archetype, double>? InitialArchetypeWeights { get; set; }
    [Parameter] public IReadOnlyList<WeightedTheme>? InitialThemes { get; set; }
    [Parameter] public BracketSelection? InitialBracket { get; set; }
    [Parameter] public BudgetSelection? InitialBudget { get; set; }

    private static readonly string[] AllStages =
    [
        "Resolving template",
        "Gathering card pool",
        "Filtering pool",
        "Classifying commanders",
        "Classifying card pool",
        "Filling deck",
        "Applying color fixing",
        "Repairing illegal cards",
        "Distributing basic lands",
        "Assembling result",
    ];

    // ── Form state ─────────────────────────────────────────────────────────

    private IReadOnlyList<Card> _selectedCommanders = [];
    private int _commanderResetKey = 0;

    private IReadOnlyDictionary<Archetype, double> _archetypeWeights = new Dictionary<Archetype, double>();
    private int _archetypeResetKey = 0;

    private IReadOnlyList<WeightedTheme> _themes = [];
    private int _themeResetKey = 0;

    private BracketSelection _bracketSelection = new(Bracket.Three, true);
    private int _bracketResetKey = 0;

    private BudgetSelection _budget = new(null, null);
    private int _budgetResetKey = 0;

    // ── Build state ────────────────────────────────────────────────────────

    private bool _isBuilding;
    private string? _currentStage;
    private readonly List<string> _completedStages = [];
    private string? _errorMessage;
    private CancellationTokenSource? _buildCts;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        ApiKeyState.OnChange += OnApiKeyStateChanged;

        if (InitialCommander is not null)
            _selectedCommanders = [InitialCommander];

        if (InitialArchetypeWeights is not null)
            _archetypeWeights = InitialArchetypeWeights;

        if (InitialThemes is not null)
            _themes = InitialThemes;

        if (InitialBracket is not null)
            _bracketSelection = InitialBracket;

        if (InitialBudget is not null)
            _budget = InitialBudget;
    }

    private void OnApiKeyStateChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => ApiKeyState.OnChange -= OnApiKeyStateChanged;

    // ── Callbacks ──────────────────────────────────────────────────────────

    private void OnCommandersChanged(IReadOnlyList<Card> commanders) =>
        _selectedCommanders = commanders;

    private void OnArchetypesChanged(IReadOnlyDictionary<Archetype, double> weights) =>
        _archetypeWeights = weights;

    private void OnThemesChanged(IReadOnlyList<WeightedTheme> themes) =>
        _themes = themes;

    private void OnBracketChanged(BracketSelection selection) =>
        _bracketSelection = selection;

    private void OnBudgetChanged(BudgetSelection budget) =>
        _budget = budget;

    // ── Build ──────────────────────────────────────────────────────────────

    private async Task StartBuildAsync()
    {
        if (_selectedCommanders.Count == 0) return;

        _isBuilding = true;
        _currentStage = null;
        _completedStages.Clear();
        _errorMessage = null;
        _buildCts = new CancellationTokenSource();

        var p = BuildRequestFactory.ForGuided(_archetypeWeights, _themes, _bracketSelection, _budget);

        // Enable token tracking if configured
        var enableTracking = Config.GetValue<bool>("Features:EnableTokenUsageTracking");
        if (enableTracking)
        {
            var tracker = new UsageTracker();
            DeckBuilder.UsageTracker = tracker;
        }

        try
        {
            var buildResult = await DeckBuilder.BuildAsync(
                [.. _selectedCommanders],
                p.Template,
                p.Archetypes,
                p.Themes,
                p.BracketProfile,
                p.Constraints,
                new Progress<string>(OnStageReport),
                _buildCts.Token);

            // Log usage if tracking was enabled
            if (enableTracking && DeckBuilder.UsageTracker != null)
            {
                var summary = DeckBuilder.UsageTracker.GetSummary();
                Console.WriteLine($"=== Token Usage Summary ===");
                Console.WriteLine(DeckBuilder.UsageTracker.FormatTable());
                Console.WriteLine($"Total cost: ${summary.EstimatedCostUsd:F4}");
            }

            await InvokeAsync(async () =>
            {
                if (_currentStage is not null) _completedStages.Add(_currentStage);
                _currentStage = null;
                _isBuilding = false;

                var id = Guid.NewGuid().ToString("N");
                var stored = new StoredDeckResult(
                    buildResult,
                    _selectedCommanders,
                    _archetypeWeights,
                    _themes,
                    _bracketSelection.Enabled ? _bracketSelection.Bracket : Bracket.Three,
                    _budget.MaxCardPriceUsd,
                    _budget.TotalBudgetUsd,
                    DateOnly.FromDateTime(DateTime.Today));
                ResultStore.Put(id, stored);
                await JS.InvokeVoidAsync("saveDeckResult",
                    DeckResultStorage.LocalStorageKey(id),
                    DeckResultStorage.Serialize(stored),
                    DeckResultStorage.DefaultMaxSavedResults);
                Navigation.NavigateTo($"/results/{id}");
            });
        }
        catch (OperationCanceledException)
        {
            await InvokeAsync(() =>
            {
                _isBuilding = false;
                _currentStage = null;
                _completedStages.Clear();
                StateHasChanged();
            });
        }
        catch (ApiKeyRejectedException)
        {
            await InvokeAsync(() =>
            {
                Keys.Clear();
                _isBuilding = false;
                _currentStage = null;
                _completedStages.Clear();
                _errorMessage = "Your API key was rejected — please reconnect.";
                ApiKeyState.NotifyChanged();
                StateHasChanged();
            });
        }
        catch (Exception ex)
        {
            await InvokeAsync(() =>
            {
                _isBuilding = false;
                _currentStage = null;
                _completedStages.Clear();
                _errorMessage = $"Build failed: {ex.Message}";
                StateHasChanged();
            });
        }
    }

    private void OnStageReport(string stage)
    {
        _ = InvokeAsync(() =>
        {
            if (_currentStage is not null) _completedStages.Add(_currentStage);
            _currentStage = stage;
            StateHasChanged();
        });
    }

    private void CancelBuild() => _buildCts?.Cancel();
}
