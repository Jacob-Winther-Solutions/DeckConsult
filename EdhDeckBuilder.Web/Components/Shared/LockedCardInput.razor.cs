using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Core.Cards;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Shared;

public record LockedCardState(
    bool IsValidated,
    bool HasText,
    IReadOnlyList<Card> ValidCards,
    IReadOnlyList<string> Errors,
    IReadOnlyList<Card> ColorWarnings)
{
    public static LockedCardState Empty { get; } = new(false, false, [], [], []);
    public IReadOnlyList<Card> AllLockedCards => [.. ValidCards, .. ColorWarnings];
}

public partial class LockedCardInput : ComponentBase
{
    [Inject] private ILockedCardValidator LockedCardValidator { get; set; } = default!;

    [Parameter, EditorRequired] public required IReadOnlyList<Card> Commanders { get; set; }
    [Parameter] public EventCallback<LockedCardState> OnChanged { get; set; }

    private string _text = "";
    private IReadOnlyList<Card>   _validCards    = [];
    private IReadOnlyList<string> _errors        = [];
    private IReadOnlyList<Card>   _colorWarnings = [];
    private bool _validated = false;

    private int SlotLimit => 99 - Commanders.Count;

    private LockedCardState CurrentState =>
        new(_validated, _text.Length > 0, _validCards, _errors, _colorWarnings);

    public void Reset()
    {
        _text = "";
        _validCards = [];
        _errors = [];
        _colorWarnings = [];
        _validated = false;
        _ = OnChanged.InvokeAsync(LockedCardState.Empty);
    }

    private void OnTextChanged(ChangeEventArgs e)
    {
        _text = e.Value?.ToString() ?? "";
        _validCards = [];
        _errors = [];
        _colorWarnings = [];
        _validated = false;
        _ = OnChanged.InvokeAsync(CurrentState);
    }

    private async Task ValidateAsync()
    {
        if (Commanders.Count == 0) return;

        var names = _text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(n => n.Length > 0)
            .ToList();

        if (names.Count == 0)
        {
            _validCards = [];
            _errors = [];
            _colorWarnings = [];
            _validated = true;
            await OnChanged.InvokeAsync(CurrentState);
            return;
        }

        var colorIdentity = Commanders.Aggregate(Color.None, (ci, c) => ci | c.ColorIdentity);
        var result = await LockedCardValidator.ValidateAsync(names, colorIdentity);
        _validCards = result.ValidCards;
        _errors = result.UnrecognizedNames;
        _colorWarnings = result.WrongColorCards;
        _validated = true;
        await OnChanged.InvokeAsync(CurrentState);
    }
}
