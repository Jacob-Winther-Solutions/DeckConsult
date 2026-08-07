using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Results;

public partial class UpgradePathsPanel : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required DeckAnalysisResult AnalysisResult { get; set; }
    [Parameter, EditorRequired] public required IDeckUpgrader Upgrader { get; set; }
    [Parameter, EditorRequired] public required SessionApiKeyProvider Keys { get; set; }
    [Parameter, EditorRequired] public required IApiKeyStateService ApiKeyState { get; set; }

    /// <summary>Custom controls rendered inside the action card (e.g. a role-target editor).</summary>
    [Parameter] public RenderFragment? ExtraControls { get; set; }

    /// <summary>Override for effective targets passed to the upgrader (null = use result's own gaps).</summary>
    [Parameter] public IReadOnlyDictionary<CardRole, RoleTarget>? EffectiveTargets { get; set; }

    /// <summary>Notifies the host that a run has started (e.g. to switch tabs).</summary>
    [Parameter] public EventCallback OnRunStarted { get; set; }

    /// <summary>Notifies the host of the current upgrade result (null = cleared).</summary>
    [Parameter] public EventCallback<DeckUpgradeResult?> OnResultChanged { get; set; }

    private string  _userFeedback         = "";
    private decimal? _maxUpgradePriceUsd;
    private bool    _isLoadingUpgrades;
    private bool    _started;
    private string? _upgradeCurrentStage;
    private string? _upgradeError;
    private DeckUpgradeResult? _upgradeResult;
    private CancellationTokenSource? _upgradeCts;

    private bool CanRun => !_isLoadingUpgrades;

    public int SuggestionCount => _upgradeResult?.RoleUpgrades.Sum(r => r.Suggestions.Count) ?? 0;

    protected override void OnInitialized()
    {
        ApiKeyState.OnChange += OnApiKeyStateChanged;
    }

    public void Dispose()
    {
        ApiKeyState.OnChange -= OnApiKeyStateChanged;
        _upgradeCts?.Cancel();
        _upgradeCts?.Dispose();
    }

    private void OnApiKeyStateChanged() => InvokeAsync(StateHasChanged);

    public Task RunAsync() => RunUpgradesAsync();

    private async Task RunUpgradesAsync()
    {
        _upgradeError        = null;
        _upgradeResult       = null;
        _upgradeCurrentStage = null;
        _isLoadingUpgrades   = true;
        _started             = true;

        await OnRunStarted.InvokeAsync();
        await OnResultChanged.InvokeAsync(null);

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
                EffectiveTargets,
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

            await OnResultChanged.InvokeAsync(_upgradeResult);
            await InvokeAsync(() => { _upgradeCurrentStage = null; _isLoadingUpgrades = false; StateHasChanged(); });
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
        catch (QuotaExceededException ex)
        {
            await InvokeAsync(() =>
            {
                _isLoadingUpgrades   = false;
                _upgradeCurrentStage = null;
                _upgradeError        = LlmErrorMessages.ForQuotaException(ex);
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
}
