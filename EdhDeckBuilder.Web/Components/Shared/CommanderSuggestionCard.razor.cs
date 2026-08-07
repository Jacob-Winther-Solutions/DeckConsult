using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Shared;

public partial class CommanderSuggestionCard
{
    [Parameter]
    public required CommanderSuggestion Suggestion { get; set; }

    [Parameter]
    public IReadOnlyDictionary<Archetype, double>? ArchetypeWeights { get; set; }

    [Parameter]
    public IReadOnlyList<WeightedTheme>? Themes { get; set; }

    [Parameter]
    public BracketSelection? BracketSelection { get; set; }

    [Parameter]
    public BudgetSelection? Budget { get; set; }

    [Inject] private ISuggestionSource SuggestionSource { get; set; } = default!;
    [Inject] private CreatureTypeCatalog CreatureTypes { get; set; } = default!;

    private bool _showImageZoom = false;
    private IReadOnlyList<(string Slug, string Name, int Count, Theme? KnownTheme, Archetype? KnownArchetype)> _popularThemes = [];
    private HashSet<string> _tribeSlugSet = [];

    protected override async Task OnParametersSetAsync()
    {
        try { _popularThemes = await SuggestionSource.GetPopularThemesAsync(Suggestion.Commander); }
        catch { _popularThemes = []; }

        if (_tribeSlugSet.Count == 0)
        {
            var types = await CreatureTypes.GetTypesAsync();
            _tribeSlugSet = types.Select(t => t.ToLowerInvariant().Replace(' ', '-')).ToHashSet();
        }
    }

    private bool IsPrimary((string Slug, string Name, int Count, Theme? KnownTheme, Archetype? KnownArchetype) t)
        => t.KnownTheme.HasValue || t.KnownArchetype.HasValue || _tribeSlugSet.Contains(t.Slug);

    private string BuilderLink
    {
        get
        {
            var link = $"/commander?commander={Suggestion.Commander.OracleId}";

            if (Suggestion.PartnerCommander != null)
            {
                link += $"&partnerCommander={Suggestion.PartnerCommander.OracleId}";
            }

            if (ArchetypeWeights?.Count > 0)
            {
                var archetypes = string.Join(",", ArchetypeWeights.Keys);
                link += $"&archetypes={Uri.EscapeDataString(archetypes)}";
            }

            if (Themes?.Count > 0)
            {
                var themes = string.Join(",", Themes.Where(t => t.Profile.Theme.HasValue).Select(t => t.Profile.Theme!.Value));
                if (!string.IsNullOrEmpty(themes))
                    link += $"&themes={Uri.EscapeDataString(themes)}";
            }
            else
            {
                // No user-selected themes — preset the top EDHREC popular theme that maps to a known Theme enum.
                var topKnown = _popularThemes.FirstOrDefault(t => t.KnownTheme.HasValue);
                if (topKnown.KnownTheme.HasValue)
                    link += $"&themes={Uri.EscapeDataString(topKnown.KnownTheme.Value.ToString())}";
            }

            if (BracketSelection != null)
            {
                link += $"&bracket={BracketSelection.Bracket}";
            }

            if (Budget?.MaxCardPriceUsd.HasValue == true)
            {
                link += $"&maxCardPrice={Budget.MaxCardPriceUsd}";
            }

            if (Budget?.TotalBudgetUsd.HasValue == true)
            {
                link += $"&totalBudget={Budget.TotalBudgetUsd}";
            }

            return link;
        }
    }

    private void ToggleImageZoom()
    {
        _showImageZoom = !_showImageZoom;
    }
}
