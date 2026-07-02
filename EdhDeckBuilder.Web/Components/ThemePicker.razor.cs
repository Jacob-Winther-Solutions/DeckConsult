using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components;

public partial class ThemePicker
{
    [Parameter] public EventCallback<IReadOnlyList<WeightedTheme>> OnChanged { get; set; }

    private readonly List<WeightedTheme> _selectedThemes = new();
    private string _themeFilter = "";
    private bool _showThemeForm;
    private Theme? _tuningPreset;
    private int _editingCustomIndex = -1;
    private string _formThemeName = string.Empty;
    private string _formThemeDesc = string.Empty;
    private Dictionary<CardRole, int> _formEffectiveValues = new();

    private static readonly IReadOnlyDictionary<CardRole, int> BaselineIdeals =
        DeckTemplate.Balanced.Targets.ToDictionary(kv => kv.Key, kv => kv.Value.Ideal);

    private static readonly CardRole[] FormRoles =
        Enum.GetValues<CardRole>().Where(r => r != CardRole.Unclassified).ToArray();

    private static int EffectiveValue(CardRole role, int delta) =>
        (BaselineIdeals.TryGetValue(role, out var b) ? b : 0) + delta;

    private static string RoleLabel(CardRole role) => CardRoleDisplay.FormLabel(role);

    private bool IsPresetSelected(Theme t) =>
        _selectedThemes.Any(wt => wt.Profile.Theme == t);

    private double GetPresetWeight(Theme t) =>
        _selectedThemes.FirstOrDefault(wt => wt.Profile.Theme == t).Weight;

    private IEnumerable<(int Index, WeightedTheme Wt)> CustomThemes =>
        _selectedThemes.Select((wt, i) => (i, wt)).Where(x => x.wt.Profile.Theme is null);

    private async Task TogglePreset(Theme t)
    {
        var i = _selectedThemes.FindIndex(wt => wt.Profile.Theme == t);
        if (i >= 0) _selectedThemes.RemoveAt(i);
        else _selectedThemes.Add(new WeightedTheme(ThemeLibrary.All[t], 1.0));
        await NotifyChanged();
    }

    private async Task SetPresetWeight(Theme t, double w)
    {
        var i = _selectedThemes.FindIndex(wt => wt.Profile.Theme == t);
        if (i >= 0) _selectedThemes[i] = _selectedThemes[i] with { Weight = w };
        await NotifyChanged();
    }

    private async Task SetCustomThemeWeight(int index, double w)
    {
        if (index >= 0 && index < _selectedThemes.Count)
            _selectedThemes[index] = _selectedThemes[index] with { Weight = w };
        await NotifyChanged();
    }

    private async Task RemoveCustomTheme(int index)
    {
        _selectedThemes.RemoveAt(index);
        await NotifyChanged();
    }

    private void OpenTuneForm(Theme t)
    {
        var i = _selectedThemes.FindIndex(wt => wt.Profile.Theme == t);
        var profile = i >= 0 ? _selectedThemes[i].Profile : ThemeLibrary.All[t];
        _tuningPreset       = t;
        _editingCustomIndex = -1;
        _formThemeName      = profile.Name;
        _formThemeDesc      = profile.Description;
        _formEffectiveValues = FormRoles.ToDictionary(r => r,
            r => EffectiveValue(r, profile.Adjustments.TryGetValue(r, out var d) ? d : 0));
        _showThemeForm = true;
    }

    private void OpenCustomForm()
    {
        _tuningPreset       = null;
        _editingCustomIndex = -1;
        _formThemeName      = string.Empty;
        _formThemeDesc      = string.Empty;
        _formEffectiveValues = FormRoles.ToDictionary(r => r,
            r => BaselineIdeals.TryGetValue(r, out var b) ? b : 0);
        _showThemeForm = true;
    }

    private void OpenEditCustomForm(int index)
    {
        var wt = _selectedThemes[index];
        _tuningPreset       = null;
        _editingCustomIndex = index;
        _formThemeName      = wt.Profile.Name;
        _formThemeDesc      = wt.Profile.Description;
        _formEffectiveValues = FormRoles.ToDictionary(r => r,
            r => EffectiveValue(r, wt.Profile.Adjustments.TryGetValue(r, out var d) ? d : 0));
        _showThemeForm = true;
    }

    private async Task ApplyThemeForm()
    {
        if (string.IsNullOrWhiteSpace(_formThemeName)) return;

        var adjustments = FormRoles
            .Select(r => (Role: r, Delta: _formEffectiveValues[r] - (BaselineIdeals.TryGetValue(r, out var b) ? b : 0)))
            .Where(x => x.Delta != 0)
            .ToDictionary(x => x.Role, x => x.Delta);

        var profile = new ThemeProfile
        {
            Theme       = _tuningPreset,
            Name        = _formThemeName.Trim(),
            Description = _formThemeDesc.Trim(),
            Adjustments = adjustments,
        };

        if (_tuningPreset.HasValue)
        {
            var i = _selectedThemes.FindIndex(wt => wt.Profile.Theme == _tuningPreset);
            var weight = i >= 0 ? _selectedThemes[i].Weight : 1.0;
            if (i >= 0) _selectedThemes[i] = new WeightedTheme(profile, weight);
            else _selectedThemes.Add(new WeightedTheme(profile, 1.0));
        }
        else if (_editingCustomIndex >= 0)
        {
            _selectedThemes[_editingCustomIndex] =
                new WeightedTheme(profile, _selectedThemes[_editingCustomIndex].Weight);
        }
        else
        {
            _selectedThemes.Add(new WeightedTheme(profile, 1.0));
        }

        _showThemeForm      = false;
        _editingCustomIndex = -1;
        await NotifyChanged();
    }

    private void CancelThemeForm()
    {
        _showThemeForm      = false;
        _editingCustomIndex = -1;
    }

    private async Task NotifyChanged() => await OnChanged.InvokeAsync(_selectedThemes);
}
