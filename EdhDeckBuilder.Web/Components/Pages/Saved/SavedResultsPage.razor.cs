using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EdhDeckBuilder.Web.Components.Pages.Saved;

public partial class SavedResultsPage : ComponentBase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _loading = true;
    private List<(string Id, StoredDeckResult Deck)>         _decks    = [];
    private List<(string Id, StoredAnalysisResult Analysis)> _analyses = [];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await LoadDecksAsync();
        await LoadAnalysesAsync();

        _loading = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadDecksAsync()
    {
        try
        {
            var keys = await JS.InvokeAsync<string[]>("getResultIndex", DeckResultStorage.IndexKey);
            foreach (var key in keys)
            {
                var json = await JS.InvokeAsync<string?>("getLocalStorage", key);
                if (string.IsNullOrEmpty(json)) continue;
                var stored = DeckResultStorage.Deserialize(json);
                if (stored is null) continue;
                var id = DeckResultStorage.ExtractId(key);
                if (id is null) continue;
                _decks.Add((id, stored));
            }
        }
        catch { /* localStorage unavailable */ }
    }

    private async Task LoadAnalysesAsync()
    {
        try
        {
            var keys = await JS.InvokeAsync<string[]>("getResultIndex", AnalysisResultStorage.IndexKey);
            foreach (var key in keys)
            {
                var json = await JS.InvokeAsync<string?>("getLocalStorage", key);
                if (string.IsNullOrEmpty(json)) continue;
                var stored = AnalysisResultStorage.Deserialize(json);
                if (stored is null) continue;
                var id = AnalysisResultStorage.ExtractId(key);
                if (id is null) continue;
                _analyses.Add((id, stored));
            }
        }
        catch { /* localStorage unavailable */ }
    }

    private static string CommanderNames(IReadOnlyList<Card> commanders) =>
        string.Join(" + ", commanders.Select(c => c.Name));

    private static string BracketLabel(Bracket bracket) => bracket switch
    {
        Bracket.One   => "Bracket 1 — Casual",
        Bracket.Two   => "Bracket 2 — Precon",
        Bracket.Three => "Bracket 3 — Upgraded",
        Bracket.Four  => "Bracket 4 — Optimized",
        Bracket.Five  => "Bracket 5 — cEDH",
        _             => $"Bracket {(int)bracket}",
    };

    private static string TopArchetype(IReadOnlyDictionary<Archetype, double> weights)
    {
        var top = weights.OrderByDescending(kv => kv.Value).FirstOrDefault();
        return top.Key == default ? "" : top.Key.ToString();
    }
}
