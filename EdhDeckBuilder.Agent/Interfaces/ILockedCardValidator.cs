using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Interfaces;

/// <summary>
/// Validates a list of user-supplied card names for use as must-include (locked) cards.
/// Resolves names against the card repository and checks color identity compatibility.
/// </summary>
public interface ILockedCardValidator
{
    /// <summary>
    /// Validates <paramref name="cardNames"/> and returns resolved cards, unrecognized names,
    /// and out-of-color-identity warnings.
    /// </summary>
    /// <param name="cardNames">Raw card names entered by the user (one per line, trimmed).</param>
    /// <param name="commanderColorIdentity">Union of the selected commanders' color identities.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<LockedCardValidationResult> ValidateAsync(
        IReadOnlyList<string> cardNames,
        Color commanderColorIdentity,
        CancellationToken ct = default);
}
