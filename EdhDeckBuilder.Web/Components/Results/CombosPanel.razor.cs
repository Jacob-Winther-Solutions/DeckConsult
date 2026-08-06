using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Results;

public partial class CombosPanel : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required DeckAnalysisResult AnalysisResult { get; set; }
    [Parameter, EditorRequired] public required IComboFinder ComboFinder { get; set; }

    /// <summary>Notifies the host that a run has started (e.g. to switch tabs).</summary>
    [Parameter] public EventCallback OnRunStarted { get; set; }

    /// <summary>Notifies the host of the current combo result (null = cleared).</summary>
    [Parameter] public EventCallback<ComboAnalysisResult?> OnResultChanged { get; set; }

    private bool              _isLoadingCombos;
    private bool              _started;
    private string?           _comboError;
    private ComboAnalysisResult? _comboResult;
    private CancellationTokenSource? _comboCts;
    private readonly HashSet<string> _expandedCombos = [];

    public int TotalCount => (_comboResult?.Combos.Included.Count ?? 0) + (_comboResult?.Combos.AlmostIncluded.Count ?? 0);

    public Task RunAsync() => RunCombosAsync();

    private async Task RunCombosAsync()
    {
        _comboError      = null;
        _comboResult     = null;
        _isLoadingCombos = true;
        _started         = true;
        _expandedCombos.Clear();

        await OnRunStarted.InvokeAsync();
        await OnResultChanged.InvokeAsync(null);

        _comboCts = new CancellationTokenSource();
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            _comboResult = await ComboFinder.FindCombosAsync(AnalysisResult, _comboCts.Token);
            await OnResultChanged.InvokeAsync(_comboResult);
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

    public void Dispose()
    {
        _comboCts?.Cancel();
        _comboCts?.Dispose();
    }
}
