using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Core.Abstractions;

/// <summary>
/// Produces combo-piece <see cref="CardCandidate"/>s for a given commander + locked card seed.
/// Implemented in Infrastructure (Commander Spellbook). Consumed by the Agent layer.
/// </summary>
public interface IComboCardSource
{
    /// <summary>
    /// Returns cards that complete near-miss combos enabled by the supplied commanders and locked cards.
    /// Inclusion scores are normalized 0–1 relative to the most popular combo in the result.
    /// </summary>
    Task<IReadOnlyList<CardCandidate>> GetComboCandidatesAsync(
        IReadOnlyList<Card> commanders,
        IReadOnlyList<Card> lockedCards,
        CancellationToken ct = default);
}
