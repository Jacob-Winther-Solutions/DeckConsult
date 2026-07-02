using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components;

public partial class CommanderPicker
{
    [Inject] private ICardRepository CardRepository { get; set; } = default!;

    [Parameter] public IReadOnlyList<Card> SelectedCommanders { get; set; } = [];
    [Parameter] public EventCallback<IReadOnlyList<Card>> OnSelectionChanged { get; set; }
    [Parameter] public EventCallback<string> OnError { get; set; }

    private string _commanderQuery = "";
    private List<Card> _searchResults = [];
    private bool _showDropdown;
    private bool _isSearching;
    private CancellationTokenSource? _searchCts;

    private async Task OnCommanderInput(ChangeEventArgs e)
    {
        _commanderQuery = e.Value?.ToString() ?? "";
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(300, _searchCts.Token);
            await SearchAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _isSearching = false;
            _showDropdown = false;
            await OnError.InvokeAsync($"Search failed: {ex.Message}");
            StateHasChanged();
        }
    }

    private async Task SearchAsync()
    {
        if (_commanderQuery.Length < 2)
        {
            _searchResults.Clear();
            _showDropdown = false;
            return;
        }
        _isSearching = true;
        StateHasChanged();

        var results = await CardRepository.SearchAsync(_commanderQuery);
        _searchResults = [.. results.Where(c => c.CanBeCommander).Take(8)];
        _showDropdown = _searchResults.Count > 0;
        _isSearching = false;
        StateHasChanged();
    }

    private void OnSearchFocus() => _showDropdown = _searchResults.Count > 0;

    private async Task OnSearchBlur()
    {
        await Task.Delay(200);
        _showDropdown = false;
        StateHasChanged();
    }

    private async Task SelectCommander(Card card)
    {
        if (SelectedCommanders.Count >= 2 || SelectedCommanders.Contains(card)) return;
        _commanderQuery = "";
        _searchResults.Clear();
        _showDropdown = false;
        await OnSelectionChanged.InvokeAsync([.. SelectedCommanders, card]);
    }

    private async Task RemoveCommander(Card card) =>
        await OnSelectionChanged.InvokeAsync(SelectedCommanders.Where(c => c != card).ToList());
}
