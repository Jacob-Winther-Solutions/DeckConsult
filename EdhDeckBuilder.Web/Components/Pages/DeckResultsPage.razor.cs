using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EdhDeckBuilder.Web.Components.Pages;

public partial class DeckResultsPage
{
    [Parameter] public string Id { get; set; } = "";

    [Inject] private IJSRuntime        JS          { get; set; } = default!;
    [Inject] private NavigationManager Navigation  { get; set; } = default!;
    [Inject] private DeckResultStore   ResultStore { get; set; } = default!;

    private StoredDeckResult? _stored;
    private bool _notFound;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        // Fast path: same-session navigation — result is already in memory.
        _stored = ResultStore.Get(Id);

        // Slow path: page reload — fall back to localStorage (requires raised SignalR limit).
        if (_stored is null)
        {
            try
            {
                var json = await JS.InvokeAsync<string?>("getLocalStorage", DeckResultStorage.LocalStorageKey(Id));
                if (!string.IsNullOrEmpty(json))
                    _stored = DeckResultStorage.Deserialize(json);
            }
            catch { /* localStorage unavailable or JSON malformed */ }
        }

        _notFound = _stored is null;
        await InvokeAsync(StateHasChanged);
    }

    private void GoBack() => Navigation.NavigateTo("/commander");

    private async Task DownloadReportAsync()
    {
        if (_stored is null) return;
        var content = DeckReportExporter.Export(
            _stored.Result,
            _stored.Commanders,
            _stored.ArchetypeWeights,
            _stored.Themes is { Count: > 0 } t ? t : null,
            _stored.Bracket,
            _stored.MaxCardPriceUsd,
            _stored.TotalBudgetUsd,
            _stored.BuiltOn);
        var filename = DeckReportExporter.SlugifyFilename(_stored.Commanders) + "-build-report.md";
        await JS.InvokeVoidAsync("downloadTextFile", filename, content, "text/markdown");
    }
}
