using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Agent.Models;

/// <summary>
/// Soft, non-enforced guidance passed to the LLM selector as deck-building context.
/// None of these are hard rules — they shape candidate ranking, not legality.
/// </summary>
public sealed record SoftConstraints
{
    /// <summary>
    /// The agreed power level for this build. Informs the selector about which categories
    /// of cards are acceptable (e.g. Bracket 1 avoids tutors; Bracket 5 expects fast mana).
    /// </summary>
    public required Bracket Bracket { get; init; }

    /// <summary>
    /// Free-text curve guidance derived from the archetype, e.g.
    /// "Aggro: strongly favor low mana-value cards (≤3)."
    /// Empty for Balanced/Midrange builds where curve is not a priority.
    /// </summary>
    public string CurveNote { get; init; } = string.Empty;

    /// <summary>Additional context hints forwarded verbatim to the LLM selector prompt.</summary>
    public IReadOnlyList<string> AdditionalHints { get; init; } = [];
}
