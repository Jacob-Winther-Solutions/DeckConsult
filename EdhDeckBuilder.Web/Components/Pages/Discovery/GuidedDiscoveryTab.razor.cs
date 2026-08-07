using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Pages.Discovery;

public partial class GuidedDiscoveryTab : IDisposable
{
    [Inject] private ICommanderDiscovery Discovery { get; set; } = default!;
    [Inject] private SessionApiKeyProvider Keys { get; set; } = default!;
    [Inject] private IApiKeyStateService ApiKeyState { get; set; } = default!;

    private static readonly string[] AllStages =
    [
        "Gathering candidates",
        "Ranking commanders",
        "Assembling results",
    ];

    private Color? _colorFilter = null;
    private bool _exactColorMatch = false;
    private IReadOnlyDictionary<Archetype, double> _archetypeWeights = new Dictionary<Archetype, double>();
    private IReadOnlyList<WeightedTheme> _themes = [];
    private BracketSelection _bracketSelection = new(Bracket.Three, true);
    private BudgetSelection _budget = new(null, null);
    private string _description = "";

    private bool _isRunning = false;
    private string? _currentStage;
    private string? _currentDetail;
    private readonly List<string> _completedStages = [];
    private string _errorMessage = "";
    private CancellationTokenSource? _cts;
    private CommanderDiscoveryResult? _result;

    private void OnColorFilterChanged(ColorFilterSelection selection)
    {
        _colorFilter = selection.Filter;
        _exactColorMatch = selection.ExactMatch;
    }

    private void OnArchetypesChanged(IReadOnlyDictionary<Archetype, double> weights)
    {
        _archetypeWeights = weights;
    }

    private void OnThemesChanged(IReadOnlyList<WeightedTheme> themes)
    {
        _themes = themes;
    }

    private void OnBracketChanged(BracketSelection selection)
    {
        _bracketSelection = selection;
    }

    private void OnBudgetChanged(BudgetSelection budget)
    {
        _budget = budget;
    }

    private async Task FindCommandersAsync()
    {
        _isRunning = true;
        _errorMessage = "";
        _result = null;
        _cts = new CancellationTokenSource();

        var tracker = new UsageTracker();
        Discovery.SetUsageTracker(tracker);

        try
        {
            var request = new CommanderDiscoveryRequest
            {
                Archetypes = _archetypeWeights.Keys.ToList(),
                Themes = _themes,
                ColorFilter = _colorFilter,
                ExactColorMatch = _exactColorMatch,
                Bracket = _bracketSelection.Bracket,
                Description = _description,
                MaxCardPriceUsd = _budget.MaxCardPriceUsd,
            };

            _result = await Discovery.DiscoverAsync(request, OnStageReport, _cts.Token);

            await InvokeAsync(() =>
            {
                if (_currentStage is not null) _completedStages.Add(_currentStage);
                _currentStage = null;
                _currentDetail = null;
                StateHasChanged();
            });
            await Task.Yield();

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
        catch (QuotaExceededException ex)
        {
            _errorMessage = LlmErrorMessages.ForQuotaException(ex);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            _isRunning = false;
            _currentStage = null;
            _currentDetail = null;
            _completedStages.Clear();
            _cts?.Dispose();
        }
    }

    private async Task OnStageReport(DiscoveryProgress p)
    {
        await InvokeAsync(() =>
        {
            if (_currentStage is not null) _completedStages.Add(_currentStage);
            _currentStage = p.Stage;
            _currentDetail = p.Detail;
            StateHasChanged();
        });
        await Task.Yield();
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
        _archetypeWeights = new Dictionary<Archetype, double>();
        _themes = [];
        _bracketSelection = new(Bracket.Three, true);
        _budget = new(null, null);
        _description = "";
        _result = null;
        _errorMessage = "";
        _currentStage = null;
        _currentDetail = null;
        _completedStages.Clear();
    }

    void IDisposable.Dispose()
    {
        _cts?.Dispose();
    }
}
