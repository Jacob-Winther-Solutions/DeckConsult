using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Models;

/// <summary>
/// Result of validating a list of user-supplied card names against the card repository.
/// </summary>
public sealed record LockedCardValidationResult
{
    /// <summary>Cards found in the repository and legal within the commander's color identity.</summary>
    public required IReadOnlyList<Card> ValidCards { get; init; }

    /// <summary>Names that could not be resolved in the card repository. These block the build.</summary>
    public required IReadOnlyList<string> UnrecognizedNames { get; init; }

    /// <summary>Cards found in the repository but outside the commander's color identity. Advisory — do not block the build.</summary>
    public required IReadOnlyList<Card> WrongColorCards { get; init; }

    public bool HasErrors => UnrecognizedNames.Count > 0;
}
