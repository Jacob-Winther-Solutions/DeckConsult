using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text;

namespace EdhDeckBuilder.Web.Components.Pages;

public partial class CommanderBuilder
{
    [Inject] private ICardRepository CardRepository { get; set; } = default!;
    [Inject] private IDeckBuilder DeckBuilder { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Progress stage definitions ─────────────────────────────────────────

    private static readonly string[] AllStages =
    [
        "Resolving template",
        "Gathering card pool",
        "Filtering pool",
        "Classifying commanders",
        "Classifying card pool",
        "Filling deck",
        "Applying color fixing",
        "Repairing illegal cards",
        "Distributing basic lands",
        "Assembling result",
    ];

    // ── Form state ─────────────────────────────────────────────────────────

    private string _commanderQuery = "";
    private List<Card> _searchResults = [];
    private List<Card> _selectedCommanders = [];
    private bool _showDropdown;
    private bool _isSearching;
    private CancellationTokenSource? _searchCts;

    private readonly Dictionary<Archetype, double> _archetypeWeights = new();

    // Theme state — list-based so preset and custom themes share the same collection
    private readonly List<WeightedTheme> _selectedThemes = new();
    private string _themeFilter = "";

    // Shared tune/custom theme form state
    private bool _showThemeForm;
    private Theme? _tuningPreset;
    private int _editingCustomIndex = -1;
    private string _formThemeName = string.Empty;
    private string _formThemeDesc = string.Empty;
    private Dictionary<CardRole, int> _formEffectiveValues = new();

    private Bracket _bracket = Bracket.Three;
    private string _maxCardPriceText = "";
    private string _totalBudgetText  = "";

    private decimal? ParsedMaxCardPrice =>
        decimal.TryParse(_maxCardPriceText, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;

    private decimal? ParsedTotalBudget =>
        decimal.TryParse(_totalBudgetText, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;

    // ── Export state ───────────────────────────────────────────────────────

    private bool _showExport;
    private bool _exportCopied;

    private string BuildExportText()
    {
        if (_result is null) return "";
        var sb = new StringBuilder();

        sb.AppendLine("// Commander");
        foreach (var c in _selectedCommanders)
            sb.AppendLine($"1 {c.Name}");

        sb.AppendLine();
        sb.AppendLine("// Deck");
        foreach (var s in _result.Deck.OrderBy(s => s.Roles.Primary).ThenBy(s => s.Card.Name))
            sb.AppendLine($"1 {s.Card.Name}");

        if (_result.BasicLandCounts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// Basic Lands");
            foreach (var (land, count) in _result.BasicLandCounts.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"{count} {land}");
        }

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

    // ── Build state ────────────────────────────────────────────────────────

    private bool _isBuilding;
    private string? _currentStage;
    private readonly List<string> _completedStages = [];
    private DeckBuildResult? _result;
    private string? _errorMessage;
    private CancellationTokenSource? _buildCts;

    // ── Commander search ───────────────────────────────────────────────────

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
            _errorMessage = $"Search failed: {ex.Message}";
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

    private void SelectCommander(Card card)
    {
        if (_selectedCommanders.Count < 2 && !_selectedCommanders.Contains(card))
            _selectedCommanders.Add(card);
        _commanderQuery = "";
        _searchResults.Clear();
        _showDropdown = false;
    }

    private void RemoveCommander(Card card) => _selectedCommanders.Remove(card);

    // ── Archetype toggles ──────────────────────────────────────────────────

    private void ToggleArchetype(Archetype a)
    {
        if (_archetypeWeights.ContainsKey(a)) _archetypeWeights.Remove(a);
        else _archetypeWeights[a] = 1.0;
    }

    private void SetArchetypeWeight(Archetype a, double w) => _archetypeWeights[a] = w;

    // ── Theme helpers ──────────────────────────────────────────────────────

    private bool IsPresetSelected(Theme t) =>
        _selectedThemes.Any(wt => wt.Profile.Theme == t);

    private double GetPresetWeight(Theme t) =>
        _selectedThemes.FirstOrDefault(wt => wt.Profile.Theme == t).Weight;

    private void TogglePreset(Theme t)
    {
        var i = _selectedThemes.FindIndex(wt => wt.Profile.Theme == t);
        if (i >= 0) _selectedThemes.RemoveAt(i);
        else _selectedThemes.Add(new WeightedTheme(ThemeLibrary.All[t], 1.0));
    }

    private void SetPresetWeight(Theme t, double w)
    {
        var i = _selectedThemes.FindIndex(wt => wt.Profile.Theme == t);
        if (i >= 0) _selectedThemes[i] = _selectedThemes[i] with { Weight = w };
    }

    private void SetCustomThemeWeight(int index, double w)
    {
        if (index >= 0 && index < _selectedThemes.Count)
            _selectedThemes[index] = _selectedThemes[index] with { Weight = w };
    }

    private IEnumerable<(int Index, WeightedTheme Wt)> CustomThemes =>
        _selectedThemes
            .Select((wt, i) => (i, wt))
            .Where(x => x.wt.Profile.Theme is null);

    // ── Theme form baseline ────────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<CardRole, int> BaselineIdeals =
        DeckTemplate.Balanced.Targets.ToDictionary(kv => kv.Key, kv => kv.Value.Ideal);

    private static readonly CardRole[] FormRoles =
        Enum.GetValues<CardRole>().Where(r => r != CardRole.Unclassified).ToArray();

    private static int EffectiveValue(CardRole role, int delta) =>
        (BaselineIdeals.TryGetValue(role, out var b) ? b : 0) + delta;

    internal static string RoleLabel(CardRole role) => role switch
    {
        CardRole.Land               => "Lands",
        CardRole.Ramp               => "Ramp",
        CardRole.CardAdvantage      => "Card Advantage",
        CardRole.TargetedDisruption => "Targeted Disruption",
        CardRole.MassDisruption     => "Mass Disruption",
        CardRole.Tutor              => "Tutors",
        CardRole.Protection         => "Protection",
        CardRole.Recursion          => "Recursion",
        CardRole.Plan               => "Plan",
        CardRole.Payoff             => "Payoff",
        CardRole.Synergy            => "Synergy",
        _                           => role.ToString(),
    };

    // ── Theme form open/apply/cancel ───────────────────────────────────────

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

    private void ApplyThemeForm()
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
    }

    private void CancelThemeForm()
    {
        _showThemeForm      = false;
        _editingCustomIndex = -1;
    }

    // ── Build ──────────────────────────────────────────────────────────────

    private async Task StartBuildAsync()
    {
        if (_selectedCommanders.Count == 0) return;

        _isBuilding = true;
        _currentStage = null;
        _completedStages.Clear();
        _errorMessage = null;
        _buildCts = new CancellationTokenSource();

        var archetypes = _archetypeWeights
            .Select(kv => new WeightedArchetype(ArchetypeLibrary.All[kv.Key], kv.Value))
            .ToList();

        var themes = _selectedThemes.Count > 0
            ? (IReadOnlyList<WeightedTheme>)_selectedThemes
            : null;

        var bracketProfile = BracketLibrary.All[_bracket];

        var curveNote = _archetypeWeights.ContainsKey(Archetype.Aggro) && _archetypeWeights[Archetype.Aggro] >= 0.5
            ? "Strongly favor threats with mana value ≤3."
            : "";

        var hints = _selectedThemes
            .Where(wt => !string.IsNullOrWhiteSpace(wt.Profile.Description))
            .Select(wt => $"Theme: {wt.Profile.Name} — {wt.Profile.Description}")
            .ToList();

        var constraints = new SoftConstraints
        {
            Bracket           = _bracket,
            CurveNote         = curveNote,
            AdditionalHints   = hints,
            MaxCardPriceUsd   = ParsedMaxCardPrice,
            TotalBudgetUsd    = ParsedTotalBudget,
        };

        var progress = new Progress<string>(OnStageReport);

        try
        {
            var buildResult = await DeckBuilder.BuildAsync(
                [.. _selectedCommanders],
                DeckTemplate.Balanced,
                archetypes,
                themes,
                bracketProfile,
                constraints,
                progress,
                _buildCts.Token);

            await InvokeAsync(() =>
            {
                _result = buildResult;
                if (_currentStage is not null) _completedStages.Add(_currentStage);
                _currentStage = null;
                _isBuilding = false;
                StateHasChanged();
            });
        }
        catch (OperationCanceledException)
        {
            await InvokeAsync(() =>
            {
                _isBuilding = false;
                _currentStage = null;
                _completedStages.Clear();
                StateHasChanged();
            });
        }
        catch (Exception ex)
        {
            await InvokeAsync(() =>
            {
                _isBuilding = false;
                _currentStage = null;
                _completedStages.Clear();
                _errorMessage = $"Build failed: {ex.Message}";
                StateHasChanged();
            });
        }
    }

    private void OnStageReport(string stage)
    {
        _ = InvokeAsync(() =>
        {
            if (_currentStage is not null) _completedStages.Add(_currentStage);
            _currentStage = stage;
            StateHasChanged();
        });
    }

    private void CancelBuild() => _buildCts?.Cancel();

    private void ResetForm()
    {
        _result = null;
        _errorMessage = null;
        _showExport = false;
        _exportCopied = false;
        _selectedCommanders.Clear();
        _commanderQuery = "";
        _searchResults.Clear();
        _archetypeWeights.Clear();
        _selectedThemes.Clear();
        _themeFilter = "";
        _showThemeForm = false;
        _editingCustomIndex = -1;
        _bracket = Bracket.Three;
        _maxCardPriceText = "";
        _totalBudgetText  = "";
    }

    // ── Color identity display ─────────────────────────────────────────────

    internal static IEnumerable<ColorPip> GetColorPips(Color identity)
    {
        if (identity == Color.None)
            yield return new("C", "badge bg-secondary", "");
        if (identity.HasFlag(Color.White))
            yield return new("W", "badge border", "background:#f9fafb;color:#555;");
        if (identity.HasFlag(Color.Blue))
            yield return new("U", "badge bg-primary", "");
        if (identity.HasFlag(Color.Black))
            yield return new("B", "badge bg-dark", "");
        if (identity.HasFlag(Color.Red))
            yield return new("R", "badge bg-danger", "");
        if (identity.HasFlag(Color.Green))
            yield return new("G", "badge bg-success", "");
    }

    internal sealed record ColorPip(string Symbol, string BadgeClass, string Style);
}
