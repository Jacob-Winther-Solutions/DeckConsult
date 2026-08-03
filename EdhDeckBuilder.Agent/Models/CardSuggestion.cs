using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Models;

/// <summary>
/// A card chosen for the final 99, enriched with the LLM's rationale for picking it.
/// This is the committed form of a <see cref="FillCandidate"/> — every card in the deck
/// becomes a <see cref="CardSuggestion"/> so the UI can display the reason alongside the card.
/// </summary>
public sealed record CardSuggestion
{
    public required Card Card { get; init; }
    public required RoleProfile Roles { get; init; }

    /// <summary>
    /// The LLM-generated reason this card was selected for this specific deck, e.g.
    /// "Refills your hand after emptying your grip with your token-making spells, which this
    /// deck does regularly." Captured during selection so the context is still fresh.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>Rank within the card's primary role bucket (1 = highest scored by the selector).</summary>
    public required int Rank { get; init; }

    /// <summary>True when the user locked this card in before the build started.</summary>
    public bool IsLocked { get; init; } = false;
}
