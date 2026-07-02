using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components;

public partial class BracketPicker
{
    [Parameter] public EventCallback<BracketSelection> OnChanged { get; set; }

    private Bracket _bracket = Bracket.Three;
    private bool    _enabled = true;

    private async Task OnBracketSelected(Bracket b)
    {
        _bracket = b;
        await NotifyChanged();
    }

    private async Task OnEnabledChanged(bool value)
    {
        _enabled = value;
        await NotifyChanged();
    }

    private async Task NotifyChanged() => await OnChanged.InvokeAsync(new(_bracket, _enabled));
}
