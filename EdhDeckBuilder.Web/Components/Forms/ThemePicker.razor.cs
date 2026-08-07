using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Forms;

public partial class ThemePicker : ComponentBase
{
    [Parameter] public EventCallback<IReadOnlyList<WeightedTheme>> OnChanged { get; set; }
    [Parameter] public IReadOnlyList<WeightedTheme>? InitialThemes { get; set; }
    [Inject] private CreatureTypeCatalog CreatureTypes { get; set; } = null!;

    private readonly List<WeightedTheme> _selectedThemes = new();
    private string _themeFilter = "";
    private bool _showThemeForm;
    private Theme? _tuningPreset;
    private int _editingCustomIndex = -1;
    private string _formThemeName = string.Empty;
    private string _formThemeDesc = string.Empty;
    private Dictionary<CardRole, int> _formEffectiveValues = new();
    private string _tribalName = string.Empty;
    private IReadOnlyList<string> _creatureTypes = [];
    private bool _tribalDropdownOpen;
    private List<string> _filteredTribes = [];

    protected override void OnInitialized()
    {
        if (InitialThemes is not null)
        {
            foreach (var theme in InitialThemes)
            {
                _selectedThemes.Add(theme);
                if (theme.Profile.Theme == Theme.Tribal && theme.TribeName is not null)
                    _tribalName = theme.TribeName;
            }
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _creatureTypes = await CreatureTypes.GetTypesAsync();
    }

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
        else
        {
            var tribeName = t == Theme.Tribal ? _tribalName : null;
            _selectedThemes.Add(new WeightedTheme(ThemeLibrary.All[t], 1.0, tribeName));
        }
        await NotifyChanged();
    }

    private async Task SetTribalName(string name)
    {
        _tribalName = name;
        UpdateFilteredTribes();
        var i = _selectedThemes.FindIndex(wt => wt.Profile.Theme == Theme.Tribal);
        if (i >= 0)
            _selectedThemes[i] = _selectedThemes[i] with { TribeName = name.Trim() };
        await NotifyChanged();
    }

    private void UpdateFilteredTribes()
    {
        _filteredTribes = string.IsNullOrEmpty(_tribalName)
            ? [.. _creatureTypes.Take(30)]
            : [.. _creatureTypes
                .Where(t => t.StartsWith(_tribalName, StringComparison.OrdinalIgnoreCase)
                         || t.Contains(_tribalName, StringComparison.OrdinalIgnoreCase))
                .Take(30)];
    }

    private void OpenTribalDropdown()
    {
        UpdateFilteredTribes();
        _tribalDropdownOpen = true;
    }

    private void CloseTribalDropdown() => _tribalDropdownOpen = false;

    private async Task SelectTribalType(string type)
    {
        _tribalDropdownOpen = false;
        await SetTribalName(type);
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
