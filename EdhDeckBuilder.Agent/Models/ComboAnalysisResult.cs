using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Agent.Models;

public sealed record ComboAnalysisResult
{
    public required ComboSearchResult Combos { get; init; }
    /// <summary>Raw tag from Commander Spellbook (e.g. "R", "S", "E").</summary>
    public required string? SpellbookBracketTag { get; init; }
    /// <summary>Mapped bracket number, or null if the tag is unrecognized.</summary>
    public required Bracket? SpellbookBracket { get; init; }
}
