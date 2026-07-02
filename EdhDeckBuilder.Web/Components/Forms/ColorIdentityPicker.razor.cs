using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Forms;

public partial class ColorIdentityPicker
{
    enum FilterMode { Any, Colorless, Specific }

    [Parameter]
    public EventCallback<ColorFilterSelection> OnChanged { get; set; }

    private FilterMode _filterMode = FilterMode.Any;
    private Color _selectedColors = Color.None;
    private bool _exactMatch = false;

    private string GetColorButtonClass(Color color)
    {
        var isSelected = (_selectedColors & color) != Color.None;
        return isSelected ? "btn-primary" : "btn-outline-secondary";
    }

    private void SetFilterMode(FilterMode mode)
    {
        _filterMode = mode;
        _exactMatch = false;
        _selectedColors = Color.None;

        switch (mode)
        {
            case FilterMode.Any:
                EmitSelection(null);
                break;
            case FilterMode.Colorless:
                EmitSelection(Color.None);
                break;
            case FilterMode.Specific:
                break;
        }
    }

    private void SelectColors(Color color)
    {
        _selectedColors ^= color;

        if (_selectedColors == Color.None)
        {
            _filterMode = FilterMode.Any;
            EmitSelection(null);
        }
        else
        {
            _filterMode = FilterMode.Specific;
            EmitSelection(_selectedColors);
        }
    }

    private async Task OnExactMatchChanged(ChangeEventArgs e)
    {
        _exactMatch = (bool)e.Value!;
        EmitSelection(_selectedColors);
    }

    private void EmitSelection(Color? filter)
    {
        OnChanged.InvokeAsync(new ColorFilterSelection(filter, _exactMatch));
    }
}
