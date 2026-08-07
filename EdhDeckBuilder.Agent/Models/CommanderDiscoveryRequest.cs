using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Agent.Models;

public sealed record CommanderDiscoveryRequest
{
    public required IReadOnlyList<Archetype> Archetypes { get; init; }
    public required IReadOnlyList<WeightedTheme> Themes { get; init; }
    public Color? ColorFilter { get; init; }
    public bool ExactColorMatch { get; init; } = false;
    public Bracket Bracket { get; init; } = Bracket.Three;
    public string? Description { get; init; }
    public decimal? MaxCardPriceUsd { get; init; }
}
