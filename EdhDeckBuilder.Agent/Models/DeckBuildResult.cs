using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Agent.Models;

/// <summary>
/// The complete output of a deck build: the 99 chosen cards, near-miss runner-ups,
/// coverage diagnostics, and cut suggestions for over-served roles.
/// </summary>
public sealed record DeckBuildResult
{
    /// <summary>
    /// Committed non-basic cards chosen for the 99 — spells, MDFCs, and utility lands —
    /// each with an LLM-generated rationale. Does not include basic lands; see <see cref="BasicLandCounts"/>.
    /// </summary>
    public required IReadOnlyList<CardSuggestion> Deck { get; init; }

    /// <summary>
    /// Basic land distribution by land name → count (e.g. "Forest" → 20, "Mountain" → 11).
    /// Sums to the number of basic land slots remaining after the fill passes.
    /// Together with <see cref="Deck"/>, covers all 99 non-commander card slots.
    /// </summary>
    public required IReadOnlyDictionary<string, int> BasicLandCounts { get; init; }

    /// <summary>
    /// Candidates that were evaluated but not selected — the next-best options per role.
    /// Lets the user review near-misses and make manual swaps without rerunning the build.
    /// </summary>
    public required IReadOnlyList<CardCandidate> RunnerUps { get; init; }

    /// <summary>
    /// The template that was used to guide construction (resolved from baseline + archetype +
    /// theme + bracket). Displayed alongside <see cref="ActualCoverage"/> so the user can see
    /// at a glance how closely the build hit its targets.
    /// </summary>
    public required DeckTemplate PlannedTemplate { get; init; }

    /// <summary>
    /// Actual role coverage achieved by the final 99 cards. Mirrors <c>Deck.CoverageByRole()</c>
    /// and may exceed 99 due to Always-relation overlaps — that is expected and correct.
    /// Compare against <see cref="PlannedTemplate"/> targets to spot gaps or surpluses.
    /// </summary>
    public required IReadOnlyDictionary<CardRole, double> ActualCoverage { get; init; }

    /// <summary>
    /// Human-readable warnings for roles whose actual coverage landed outside their planned
    /// range. Empty when the build hit every target. Surfaced so the user can decide whether
    /// to accept the result or request adjustments.
    /// </summary>
    public required IReadOnlyList<string> CoverageWarnings { get; init; }

    /// <summary>
    /// For each role where actual coverage exceeded the planned maximum, an ordered list of
    /// the weakest committed cards (worst-ranked first). Lets the user cut surplus cards with
    /// confidence rather than having to evaluate the full list themselves.
    /// </summary>
    public required IReadOnlyDictionary<CardRole, IReadOnlyList<CardSuggestion>> CutSuggestions { get; init; }

    /// <summary>
    /// Sum of <see cref="Card.PriceUsd"/> for all committed non-basic cards. Cards with no
    /// price data contribute $0. Does not include basic lands (essentially free).
    /// </summary>
    public required decimal TotalPriceUsd { get; init; }

    /// <summary>
    /// Budget constraint violations: per-card overages and total-budget excess.
    /// Empty when no budget was set or all cards are within budget.
    /// </summary>
    public required IReadOnlyList<string> BudgetWarnings { get; init; }
}
