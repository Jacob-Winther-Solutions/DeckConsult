using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Tabs;

public partial class DiscoveryTab : IDisposable
{
    [Inject] private ICommanderDiscovery Discovery { get; set; } = default!;
    [Inject] private SessionApiKeyProvider Keys { get; set; } = default!;
    [Inject] private IApiKeyStateService ApiKeyState { get; set; } = default!;

    private Color? _colorFilter = null;
    private bool _exactColorMatch = false;
    private IReadOnlyDictionary<Archetype, double> _archetypeWeights = new Dictionary<Archetype, double>();
    private IReadOnlyList<WeightedTheme> _themes = [];
    private BracketSelection _bracketSelection = new(Bracket.Three, true);
    private BudgetSelection _budget = new(null, null);
    private string _description = "";

    private bool _isRunning = false;
    private string _currentStage = "";
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
                Themes = _themes.Where(t => t.Profile.Theme.HasValue).Select(t => t.Profile.Theme!.Value).ToList(),
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
                Console.WriteLine($"Commander Discovery Usage - Input: {summary.TotalInputTokens}, Output: {summary.TotalOutputTokens}, Cost: ${summary.EstimatedCostUsd:F4}");
                Console.WriteLine(tracker.FormatTable());
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
        _archetypeWeights = new Dictionary<Archetype, double>();
        _themes = [];
        _bracketSelection = new(Bracket.Three, true);
        _budget = new(null, null);
        _description = "";
        _result = null;
        _errorMessage = "";
        _currentStage = "";
    }

    void IDisposable.Dispose()
    {
        _cts?.Dispose();
    }
}
