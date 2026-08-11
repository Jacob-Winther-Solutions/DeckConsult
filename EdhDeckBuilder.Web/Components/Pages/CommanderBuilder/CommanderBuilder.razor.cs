using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace EdhDeckBuilder.Web.Components.Pages.CommanderBuilder;

public partial class CommanderBuilder : IDisposable
{
    [Inject] private SessionApiKeyProvider Keys         { get; set; } = default!;
    [Inject] private IApiKeyStateService   ApiKeyState  { get; set; } = default!;
    [Inject] private NavigationManager     Navigation   { get; set; } = default!;
    [Inject] private ICardRepository       CardRepository { get; set; } = default!;

    [SupplyParameterFromQuery]
    public string? Commander { get; set; }

    [SupplyParameterFromQuery]
    public string? Archetypes { get; set; }

    [SupplyParameterFromQuery]
    public string? Themes { get; set; }

    [SupplyParameterFromQuery]
    public string? Bracket { get; set; }

    [SupplyParameterFromQuery]
    public string? MaxCardPrice { get; set; }

    [SupplyParameterFromQuery]
    public string? TotalBudget { get; set; }

    private string _activeTab  = "guided";
    private bool   _showModal;

    private int _guidedTabKey = 0;
    private Card? _preCommander;
    private IReadOnlyDictionary<Archetype, double>? _preArchetypes;
    private IReadOnlyList<WeightedTheme>? _preThemes;
    private BracketSelection? _preBracket;
    private BudgetSelection? _preBudget;

    protected override async Task OnInitializedAsync()
    {
        ApiKeyState.OnChange += OnApiKeyStateChanged;
    }

    protected override async Task OnParametersSetAsync()
    {
        // Clear previous values first
        _preCommander = null;
        _preArchetypes = null;
        _preThemes = null;
        _preBracket = null;
        _preBudget = null;

        // If commander query param provided, load it
        if (!string.IsNullOrEmpty(Commander) && Guid.TryParse(Commander, out var oracleId))
        {
            _preCommander = await CardRepository.GetByOracleIdAsync(oracleId);
        }

        // Parse archetypes if provided (comma-separated enum names)
        if (!string.IsNullOrEmpty(Archetypes))
        {
            var archetypeNames = Archetypes.Split(',');
            var archetypes = new Dictionary<Archetype, double>();
            foreach (var name in archetypeNames)
            {
                if (Enum.TryParse<Archetype>(name.Trim(), out var arch))
                {
                    archetypes[arch] = 1.0;
                }
            }
            if (archetypes.Count > 0)
            {
                _preArchetypes = archetypes;
            }
        }

        // Parse themes if provided (comma-separated enum names)
        if (!string.IsNullOrEmpty(Themes))
        {
            var themeNames = Themes.Split(',');
            var themes = new List<WeightedTheme>();
            foreach (var name in themeNames)
            {
                if (Enum.TryParse<Theme>(name.Trim(), out var theme))
                {
                    var profile = ThemeLibrary.All[theme];
                    themes.Add(new WeightedTheme(profile, 1.0));
                }
            }
            if (themes.Count > 0)
            {
                _preThemes = themes;
            }
        }

        // Parse bracket if provided
        if (!string.IsNullOrEmpty(Bracket) && Enum.TryParse<Bracket>(Bracket, out var bracketValue))
        {
            _preBracket = new BracketSelection(bracketValue, true);
        }

        // Parse budget if provided (max card price and/or total budget)
        decimal? maxCardPrice = null;
        decimal? totalBudget = null;

        if (!string.IsNullOrEmpty(MaxCardPrice) && decimal.TryParse(MaxCardPrice, out var price))
        {
            maxCardPrice = price;
        }

        if (!string.IsNullOrEmpty(TotalBudget) && decimal.TryParse(TotalBudget, out var total))
        {
            totalBudget = total;
        }

        if (maxCardPrice.HasValue || totalBudget.HasValue)
        {
            _preBudget = new BudgetSelection(maxCardPrice, totalBudget);
        }

        if (_preCommander != null || _preArchetypes != null || _preThemes != null || _preBracket != null || _preBudget != null)
        {
            _guidedTabKey++;
        }
    }

    private void OnApiKeyStateChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => ApiKeyState.OnChange -= OnApiKeyStateChanged;
}
