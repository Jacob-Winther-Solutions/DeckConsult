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

    /// <summary>
    /// Free-text description of the deck's intent, forwarded verbatim to the LLM selector.
    /// Used by the Custom builder path in place of archetypes and themes.
    /// </summary>
    public string? DeckDescription { get; init; }

    /// <summary>Additional context hints forwarded verbatim to the LLM selector prompt.</summary>
    public IReadOnlyList<string> AdditionalHints { get; init; } = [];

    /// <summary>
    /// No single card in the deck may cost more than this amount. Cards above this threshold
    /// are deprioritized by the selector; if no affordable card fills a role well, the best
    /// available is selected and flagged in <see cref="DeckBuildResult.BudgetWarnings"/>.
    /// </summary>
    public decimal? MaxCardPriceUsd { get; init; }

    /// <summary>
    /// The sum of all 99 non-basic cards must stay within this amount. Useful when a player
    /// is fine with one or two expensive pieces but wants to control total spend.
    /// </summary>
    public decimal? TotalBudgetUsd { get; init; }
}
