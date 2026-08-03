using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Agent.Models;

public sealed record DeckAnalysisResult
{
    /// <summary>Commander card(s) — for header display.</summary>
    public required IReadOnlyList<Card> Commanders { get; init; }

    /// <summary>Classified commander(s) — included in coverage and role buckets.</summary>
    public required IReadOnlyList<AnalyzedCard> CommanderCards { get; init; }

    /// <summary>Classified non-basic, non-commander cards (the 99 minus basics).</summary>
    public required IReadOnlyList<AnalyzedCard> Cards { get; init; }

    /// <summary>Basic land counts by name (e.g. "Plains" → 20). May be empty.</summary>
    public required IReadOnlyDictionary<string, int> BasicLandCounts { get; init; }

    /// <summary>
    /// Coverage by role across all classified cards (commanders + 99) and basic lands.
    /// Basic lands contribute to Land coverage. Use this for both gap analysis and bracket estimation.
    /// </summary>
    public required IReadOnlyDictionary<CardRole, double> ActualCoverage { get; init; }

    public required Bracket EstimatedBracket { get; init; }
    public required string BracketExplanation { get; init; }
    public required IReadOnlyList<RoleGap> RoleGaps { get; init; }
    public required IReadOnlyList<string> UnresolvedNames { get; init; }
    public required IReadOnlyList<string> ColorIdentityViolations { get; init; }
    public required decimal TotalPriceUsd { get; init; }
}
