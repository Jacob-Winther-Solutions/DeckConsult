using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Core.Abstractions;

/// <summary>Local card data sourced from Scryfall bulk data. Implemented in Infrastructure.</summary>
public interface ICardRepository
{
    Task<Card?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<Card?> GetByOracleIdAsync(Guid oracleId, CancellationToken ct = default);
    Task<IReadOnlyList<Card>> SearchAsync(string query, CancellationToken ct = default);
}

/// <summary>
/// Produces a pool of <see cref="CardCandidate"/>s for a given commander.
/// Implemented in Infrastructure (EDHREC). Consumed by the Agent layer.
/// </summary>
/// <remarks>
/// This interface sits at the boundary between Infrastructure and the Agent.
/// Infrastructure observes patterns across thousands of decks and returns raw signals
/// (inclusion rate, thematic section). It cannot reason about the deck being built.
/// The Agent is responsible for selecting candidates, assigning roles, and generating
/// a human-readable reason for each choice — see <c>CardSuggestion</c> and
/// <c>DeckBuildResult</c> in the Agent layer (TODO).
/// </remarks>
public interface ISuggestionSource
{
    /// <summary>
    /// All recommended cards across every section for this commander, deduplicated and
    /// ordered by inclusion rate. Includes themed sections (e.g. "Spellslinger Cards")
    /// which are often stronger archetype-fit signals than generic popularity alone.
    /// </summary>
    Task<IReadOnlyList<CardCandidate>> GetRecommendationsAsync(Card commander, CancellationToken ct = default);

    /// <summary>
    /// The statistical "average deck" for this commander, as a flat candidate list.
    /// Useful as a baseline or sanity-check against a freshly built deck.
    /// </summary>
    Task<IReadOnlyList<CardCandidate>> GetAverageDeckAsync(Card commander, CancellationToken ct = default);
}

/// <summary>Assigns a functional role to a card. Heuristic and LLM implementations live elsewhere.</summary>
public interface ICardClassifier
{
    Task<(CardRole Role, double Confidence)> ClassifyAsync(Card card, CancellationToken ct = default);
}
