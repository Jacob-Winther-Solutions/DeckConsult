using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Core.Partnerships;

namespace EdhDeckBuilder.Core.Abstractions;

/// <summary>
/// Provides authoritative partner pairing information from external sources.
/// Used to disambiguate when keyword-based detection is unreliable.
/// </summary>
public interface IPartnerPairingRepository
{
    /// <summary>
    /// Returns the definitive list of "Partner with" pairs from EDHREC.
    /// These pairs are matched by name, so callers must resolve to oracle IDs.
    /// </summary>
    Task<IReadOnlyList<(string FirstCardName, string SecondCardName)>> GetPartnerWithPairsAsync(CancellationToken ct = default);
}

/// <summary>Local card data sourced from Scryfall bulk data. Implemented in Infrastructure.</summary>
public interface ICardRepository
{
    Task<Card?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<Card?> GetByOracleIdAsync(Guid oracleId, CancellationToken ct = default);
    Task<Card?> GetByScryfallIdAsync(Guid scryfallId, CancellationToken ct = default);
    Task<IReadOnlyList<Card>> SearchAsync(string query, CancellationToken ct = default);
    Task<IReadOnlyList<Card>> GetCommandersAsync(
        Color? colorFilter = null,
        bool exactMatch = false,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all valid partner-card combinations that match the given color-identity filter.
    /// Color identity is computed as the union (bitwise OR) of both cards' color identities.
    /// </summary>
    /// <param name="colorFilter">
    /// If specified, only return combos where the union of both cards' color identities
    /// satisfies this filter. If null, all partner combos are returned.
    /// </param>
    /// <param name="exactMatch">
    /// If true, require exact color-identity match (union == filter).
    /// If false, allow union to be a subset of the filter (IsWithin).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All valid partner combos matching the filter.</returns>
    Task<IReadOnlyList<PartnerCombo>> GetPartnerCombosAsync(
        Color? colorFilter = null,
        bool exactMatch = false,
        CancellationToken ct = default);
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

    /// <summary>
    /// Partner-card popularity data from EDHREC's partners page.
    /// Maps card names to number of decks they appear in.
    /// Used by Commander Discovery to rank partner pairs.
    /// </summary>
    Task<Dictionary<string, int>> GetPartnerPopularityAsync(CancellationToken ct = default);

    /// <summary>
    /// Definitive "Partner with" pairs from EDHREC's partners page.
    /// EDHREC lists pre-matched partner pairs in the "Partner with" cardlist.
    /// This is the source of truth for which cards should partner with each other.
    /// Returns pairs as (FirstCardName, SecondCardName) tuples.
    /// </summary>
    Task<IReadOnlyList<(string FirstCardName, string SecondCardName)>> GetPartnerWithPairsAsync(CancellationToken ct = default);

    /// <summary>
    /// Recommendations optimized for a partner pair, fetched from EDHREC's partner-pair endpoint.
    /// Returns null if the partner pair is not recognized by EDHREC.
    /// </summary>
    Task<IReadOnlyList<CardCandidate>?> GetPartnerPairRecommendationsAsync(Card first, Card second, CancellationToken ct = default);

    /// <summary>
    /// Cards from the EDHREC commander+theme page (<c>/commanders/{slug}/{themeSlug}</c>).
    /// Returns <see langword="null"/> when the theme has no EDHREC slug or the page does not exist (404).
    /// </summary>
    Task<IReadOnlyList<CardCandidate>?> GetCommanderThemeRecommendationsAsync(
        Card commander, WeightedTheme theme, CancellationToken ct = default);

    /// <summary>
    /// Cards and commanders from the EDHREC global theme tag page (<c>/tags/{themeSlug}</c>).
    /// <c>Cards</c> contains all non-commander cardlist sections (13 sections covering all card types).
    /// <c>Commanders</c> contains the Top and New commander entries resolved to <see cref="Card"/> objects.
    /// Returns <see langword="null"/> when the theme has no EDHREC slug or the page does not exist (404).
    /// </summary>
    Task<(IReadOnlyList<CardCandidate> Cards, IReadOnlyList<Card> Commanders)?> GetTagsAsync(
        WeightedTheme theme, CancellationToken ct = default);

    /// <summary>
    /// The most popular EDHREC themes for this commander, parsed from <c>panels.taglinks</c>
    /// on the commander's EDHREC page.  Ordered by deck count descending.
    /// Each entry is (Slug, DisplayName, DeckCount, KnownTheme).
    /// <c>KnownTheme</c> is non-null when the slug maps to a <see cref="EdhDeckBuilder.Core.Decks.Theme"/> enum value.
    /// Returns an empty list when the page is unavailable or has no tag data.
    /// </summary>
    Task<IReadOnlyList<(string Slug, string Name, int Count, Theme? KnownTheme, Archetype? KnownArchetype)>> GetPopularThemesAsync(
        Card commander, CancellationToken ct = default);
}

/// <summary>Assigns a functional role to a card. Heuristic and LLM implementations live elsewhere.</summary>
public interface ICardClassifier
{
    Task<(CardRole Role, double Confidence)> ClassifyAsync(Card card, CancellationToken ct = default);
}
