using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EdhDeckBuilder.Web.Components.Results;

public partial class DeckExportPanel
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public required DeckBuildResult      Result     { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyList<Card>  Commanders { get; set; }
    [Parameter]                 public          EventCallback        OnReset    { get; set; }
    [Parameter]                 public          EventCallback        OnDownloadReport { get; set; }

    private bool _copiedDecklist;

    private string DecklistText() => DeckReportExporter.ExportDecklist(Result, Commanders);

    private async Task CopyDecklistAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", DecklistText());
            _copiedDecklist = true;
            StateHasChanged();
            await Task.Delay(2000);
            _copiedDecklist = false;
            StateHasChanged();
        }
        catch { /* clipboard unavailable */ }
    }

    private async Task DownloadDecklistAsync()
    {
        var filename = DeckReportExporter.SlugifyFilename(Commanders) + "-decklist.txt";
        await JS.InvokeVoidAsync("downloadTextFile", filename, DecklistText(), "text/plain");
    }
}
