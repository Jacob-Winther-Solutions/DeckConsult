namespace EdhDeckBuilder.Core.Abstractions;

public sealed record ComboPiece(string Name, string TypeLine);

public sealed record ComboVariant
{
    public required string Id { get; init; }
    /// <summary>Named cards from the combo that are present in the deck.</summary>
    public required IReadOnlyList<ComboPiece> OwnedPieces { get; init; }
    /// <summary>Named cards from the combo that are NOT in the deck.</summary>
    public required IReadOnlyList<string> MissingCardNames { get; init; }
    /// <summary>Flexible template slots — any card matching the description works.</summary>
    public required IReadOnlyList<string> MissingTemplates { get; init; }
    public required IReadOnlyList<string> ProducedEffects { get; init; }
    public required string Description { get; init; }
    public string BracketTag { get; init; } = "";
    public int Popularity { get; init; }
    public string ManaNeeded { get; init; } = "";
    public string NotablePrerequisites { get; init; } = "";
    public string ColorIdentity { get; init; } = "";
}

public sealed record ComboSearchResult
{
    public required IReadOnlyList<ComboVariant> Included { get; init; }
    public required IReadOnlyList<ComboVariant> AlmostIncluded { get; init; }
}

/// <summary>
/// Finds combos present in or near a decklist. Implemented in Infrastructure (Commander Spellbook).
/// </summary>
public interface IComboSource
{
    Task<ComboSearchResult> FindCombosAsync(
        IReadOnlyList<string> commanderNames,
        IReadOnlyList<string> cardNames,
        CancellationToken ct = default);

    Task<string?> EstimateBracketTagAsync(
        IReadOnlyList<string> commanderNames,
        IReadOnlyList<string> cardNames,
        CancellationToken ct = default);
}
