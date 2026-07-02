using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Forms;

public partial class BracketPicker : ComponentBase
{
    [Parameter] public EventCallback<BracketSelection> OnChanged { get; set; }
    [Parameter] public BracketSelection? InitialBracket { get; set; }

    private Bracket _bracket = Bracket.Three;
    private bool    _enabled = true;

    protected override void OnInitialized()
    {
        if (InitialBracket is not null)
        {
            _bracket = InitialBracket.Bracket;
            _enabled = InitialBracket.Enabled;
        }
    }

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
