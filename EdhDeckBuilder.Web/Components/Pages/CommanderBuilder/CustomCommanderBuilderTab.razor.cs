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

namespace EdhDeckBuilder.Web.Components.Pages.CommanderBuilder;

public partial class CustomCommanderBuilderTab : ComponentBase, IDisposable
{
    [Inject] private IDeckBuilder          DeckBuilder { get; set; } = default!;
    [Inject] private SessionApiKeyProvider Keys        { get; set; } = default!;
    [Inject] private IJSRuntime            JS          { get; set; } = default!;
    [Inject] private IApiKeyStateService   ApiKeyState { get; set; } = default!;
    [Inject] private NavigationManager     Navigation  { get; set; } = default!;
    [Inject] private DeckResultStore       ResultStore { get; set; } = default!;
    [Inject] private IConfiguration        Config      { get; set; } = default!;

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

    private string _customDescription = "";

    private Dictionary<CardRole, int> _customTemplateValues = BuildDefaultTemplateValues();

    private BudgetSelection _budget = new(null, null);
    private int _budgetResetKey = 0;

    // ── Static form metadata ───────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<CardRole, int> BaselineIdeals =
        DeckTemplate.Balanced.Targets.ToDictionary(kv => kv.Key, kv => kv.Value.Ideal);

    private static readonly CardRole[] FormRoles =
        Enum.GetValues<CardRole>().Where(r => r != CardRole.Unclassified).ToArray();

    private static Dictionary<CardRole, int> BuildDefaultTemplateValues() =>
        FormRoles.ToDictionary(r => r,
            r => DeckTemplate.Balanced.Targets.TryGetValue(r, out var t) ? t.Ideal : 0);

    private static readonly IReadOnlyDictionary<CardRole, string> RoleDescriptions =
        new Dictionary<CardRole, string>
        {
            [CardRole.Land]               = "Lands and mana-producing permanents",
            [CardRole.Ramp]               = "Accelerants — rocks, dorks, rituals, land-fetch spells",
            [CardRole.CardAdvantage]      = "Draw, impulse draw, and hand-refill effects",
            [CardRole.TargetedDisruption] = "Single-target removal for creatures, artifacts, enchantments",
            [CardRole.MassDisruption]     = "Board wipes and mass-bounce effects",
            [CardRole.Protection]         = "Counterspells, hexproof, indestructibility",
            [CardRole.Tutor]              = "Search effects that find specific cards",
            [CardRole.Recursion]          = "Graveyard recursion and reanimation effects",
            [CardRole.Plan]               = "Engines and threats that execute your core strategy",
            [CardRole.Payoff]             = "Cards that close out or greatly accelerate a win",
            [CardRole.Synergy]            = "Pieces that interact favorably with your commander or strategy",
        };

    internal static string RoleLabel(CardRole role) => CardRoleDisplay.FormLabel(role);

    // ── Build state ────────────────────────────────────────────────────────

    private bool _isBuilding;
    private string? _currentStage;
    private readonly List<string> _completedStages = [];
    private string? _errorMessage;
    private CancellationTokenSource? _buildCts;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    protected override void OnInitialized() =>
        ApiKeyState.OnChange += OnApiKeyStateChanged;

    private void OnApiKeyStateChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => ApiKeyState.OnChange -= OnApiKeyStateChanged;

    // ── Callbacks ──────────────────────────────────────────────────────────

    private void OnCommandersChanged(IReadOnlyList<Card> commanders) =>
        _selectedCommanders = commanders;

    private void OnBudgetChanged(BudgetSelection budget) =>
        _budget = budget;

    // ── Build ──────────────────────────────────────────────────────────────

    private async Task StartBuildAsync()
    {
        if (_selectedCommanders.Count == 0) return;
        if (string.IsNullOrWhiteSpace(_customDescription)) return;

        _isBuilding = true;
        _currentStage = null;
        _completedStages.Clear();
        _errorMessage = null;
        _buildCts = new CancellationTokenSource();

        var p = BuildRequestFactory.ForCustom(_customDescription, _customTemplateValues, _budget);

        var enableTracking = Config.GetValue<bool>("Features:EnableTokenUsageTracking");
        if (enableTracking)
        {
            var tracker = new UsageTracker();
            DeckBuilder.UsageTracker = tracker;
        }

        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            var buildResult = await DeckBuilder.BuildAsync(
                [.. _selectedCommanders],
                p.Template,
                p.Archetypes,
                p.Themes,
                p.BracketProfile,
                p.Constraints,
                OnStageReport,
                _buildCts.Token);

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
                    new Dictionary<Archetype, double>(),
                    null,
                    Bracket.Three,
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

    private async Task OnStageReport(string stage)
    {
        await InvokeAsync(() =>
        {
            if (_currentStage is not null) _completedStages.Add(_currentStage);
            _currentStage = stage;
            StateHasChanged();
        });
        await Task.Yield();
    }

    private void CancelBuild() => _buildCts?.Cancel();
}
