using EdhDeckBuilder.Agent.Models;
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

    private bool _showImageZoom = false;

    private string BuilderLink
    {
        get
        {
            var link = $"/commander?commander={Suggestion.Commander.OracleId}";

            if (ArchetypeWeights?.Count > 0)
            {
                var archetypes = string.Join(",", ArchetypeWeights.Keys);
                link += $"&archetypes={Uri.EscapeDataString(archetypes)}";
            }

            if (Themes?.Count > 0)
            {
                var themes = string.Join(",", Themes.Where(t => t.Profile.Theme.HasValue).Select(t => t.Profile.Theme!.Value));
                if (!string.IsNullOrEmpty(themes))
                {
                    link += $"&themes={Uri.EscapeDataString(themes)}";
                }
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
