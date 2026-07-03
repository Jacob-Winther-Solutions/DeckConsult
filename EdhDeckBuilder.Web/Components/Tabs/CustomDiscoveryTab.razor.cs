using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Tabs;

public partial class CustomDiscoveryTab : ComponentBase, IDisposable
{
    [Inject] private ICommanderDiscovery Discovery { get; set; } = default!;
    [Inject] private SessionApiKeyProvider Keys { get; set; } = default!;
    [Inject] private IApiKeyStateService ApiKeyState { get; set; } = default!;

    private Color? _colorFilter = null;
    private bool _exactColorMatch = false;
    private BracketSelection _bracketSelection = new(Bracket.Three, true);
    private BudgetSelection _budget = new(null, null);
    private string _description = "";

    private bool _isRunning = false;
    private string _currentStage = "";
    private string? _errorMessage;
    private CancellationTokenSource? _cts;
    private CommanderDiscoveryResult? _result;

    private int _budgetResetKey = 0;

    // ── Static form metadata ───────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<CardRole, int> BaselineIdeals =
        DeckTemplate.Balanced.Targets.ToDictionary(kv => kv.Key, kv => kv.Value.Ideal);

    private static readonly CardRole[] FormRoles =
        Enum.GetValues<CardRole>().Where(r => r != CardRole.Unclassified).ToArray();

    private static Dictionary<CardRole, int> BuildDefaultTemplateValues() =>
        FormRoles.ToDictionary(r => r,
            r => DeckTemplate.Balanced.Targets.TryGetValue(r, out var t) ? t.Ideal : 0);

    private Dictionary<CardRole, int> _customTemplateValues = BuildDefaultTemplateValues();

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

    // ── Callbacks ──────────────────────────────────────────────────────────

    private void OnColorFilterChanged(ColorFilterSelection selection)
    {
        _colorFilter = selection.Filter;
        _exactColorMatch = selection.ExactMatch;
    }

    private void OnBracketChanged(BracketSelection selection)
    {
        _bracketSelection = selection;
    }

    private void OnBudgetChanged(BudgetSelection budget)
    {
        _budget = budget;
    }

    // ── Build ──────────────────────────────────────────────────────────────

    private async Task StartBuildAsync()
    {
        if (string.IsNullOrWhiteSpace(_description)) return;

        _isRunning = true;
        _errorMessage = null;
        _result = null;
        _cts = new CancellationTokenSource();

        var tracker = new UsageTracker();
        Discovery.SetUsageTracker(tracker);

        try
        {
            var request = new CommanderDiscoveryRequest
            {
                Archetypes = [],
                Themes = [],
                ColorFilter = _colorFilter,
                ExactColorMatch = _exactColorMatch,
                Bracket = _bracketSelection.Bracket,
                Description = _description,
                MaxCardPriceUsd = _budget.MaxCardPriceUsd,
            };

            var progress = new Progress<string>(OnStageReport);
            _result = await Discovery.DiscoverAsync(request, progress, _cts.Token);

            if (tracker != null)
            {
                var summary = tracker.GetSummary();
                Console.WriteLine($"=== Token Usage Summary ===");
                Console.WriteLine(tracker.FormatTable());
                Console.WriteLine($"Total cost: ${summary.EstimatedCostUsd:F4}");
            }
        }
        catch (OperationCanceledException)
        {
            _errorMessage = "Discovery cancelled.";
        }
        catch (ApiKeyRejectedException)
        {
            Keys.Clear();
            ApiKeyState.NotifyChanged();
            _errorMessage = "API key rejected. Please check your API key.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            _isRunning = false;
            _cts?.Dispose();
        }
    }

    private void OnStageReport(string message)
    {
        _currentStage = message;
        StateHasChanged();
    }

    private async Task Cancel()
    {
        _cts?.Cancel();
        await Task.Delay(100);
    }

    private void ResetForm()
    {
        _colorFilter = null;
        _exactColorMatch = false;
        _bracketSelection = new(Bracket.Three, true);
        _budget = new(null, null);
        _description = "";
        _result = null;
        _errorMessage = null;
        _currentStage = "";
        _customTemplateValues = BuildDefaultTemplateValues();
    }

    void IDisposable.Dispose()
    {
        _cts?.Dispose();
    }
}
