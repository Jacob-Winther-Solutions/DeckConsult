namespace EdhDeckBuilder.Agent.Selection;

/// <summary>
/// A single ranked pick returned by <see cref="EdhDeckBuilder.Agent.Interfaces.ICardSelector"/>.
/// The selector returns a ranked list; deterministic code in the fill engine takes the top N
/// according to the resolved target — the model never dictates count.
/// </summary>
public sealed record SelectionResult
{
    /// <summary>
    /// Must echo an <c>OracleId</c> from the input candidate list. Any result whose id is not
    /// in the list is rejected before the fill engine sees it (whitelist rule).
    /// </summary>
    public required Guid OracleId { get; init; }

    /// <summary>Relative position within this role's picks (1 = strongest fit, higher = weaker).</summary>
    public required int Rank { get; init; }

    /// <summary>
    /// Why this card was chosen for this specific deck, written in the context of the
    /// commander's strategy. Stored verbatim on the resulting <c>CardSuggestion</c> and
    /// surfaced in the UI alongside the card.
    /// </summary>
    public required string Rationale { get; init; }
}
