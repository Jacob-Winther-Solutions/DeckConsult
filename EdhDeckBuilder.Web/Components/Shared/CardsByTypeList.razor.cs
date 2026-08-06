using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EdhDeckBuilder.Web.Components.Shared;

public partial class CardsByTypeList : ComponentBase
{
    [Parameter, EditorRequired] public required IReadOnlyList<AnalyzedCard> Cards { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyDictionary<string, int> BasicLandCounts { get; set; }

    /// <summary>When set, price badges exceeding this limit are highlighted as over-budget.</summary>
    [Parameter] public decimal? MaxCardPriceUsd { get; set; }

    private static readonly string[] TypeOrder = CardRoleDisplay.TypeOrder;

    private readonly HashSet<string> _collapsedTypeBuckets = [];

    private void ToggleTypeBucket(string name)
    {
        if (!_collapsedTypeBuckets.Add(name)) _collapsedTypeBuckets.Remove(name);
    }

    private bool IsOverBudget(Card card) =>
        MaxCardPriceUsd.HasValue && card.PriceUsd.HasValue && card.PriceUsd > MaxCardPriceUsd;
}
