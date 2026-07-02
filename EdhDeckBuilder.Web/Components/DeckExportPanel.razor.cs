using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text;

namespace EdhDeckBuilder.Web.Components;

public partial class DeckExportPanel
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public required DeckBuildResult      Result     { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyList<Card>  Commanders { get; set; }
    [Parameter]                 public          EventCallback        OnReset    { get; set; }
    [Parameter]                 public          EventCallback        OnDownloadReport { get; set; }

    private bool _showExport;
    private bool _exportCopied;

    private string BuildExportText()
    {
        var sb = new StringBuilder();
        foreach (var c in Commanders)
            sb.AppendLine($"1 {c.Name}");
        sb.AppendLine();
        foreach (var s in Result.Deck.OrderBy(s => s.Card.Name))
            sb.AppendLine($"1 {s.Card.Name}");
        foreach (var (land, count) in Result.BasicLandCounts.OrderByDescending(kv => kv.Value))
            sb.AppendLine($"{count} {land}");
        return sb.ToString().TrimEnd();
    }

    private async Task CopyExportTextAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", BuildExportText());
            _exportCopied = true;
            StateHasChanged();
            await Task.Delay(2000);
            _exportCopied = false;
            StateHasChanged();
        }
        catch { /* clipboard unavailable — user can select-all from the textarea */ }
    }
}
