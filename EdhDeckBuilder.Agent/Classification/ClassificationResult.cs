using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Classification;

/// <summary>
/// The LLM's verdict on a single card's functional role in this commander's deck.
/// Returned as a batch by <see cref="EdhDeckBuilder.Agent.Interfaces.ILlmClassifier"/>.
/// </summary>
public sealed record ClassificationResult
{
    /// <summary>
    /// Must echo an <c>OracleId</c> from the input batch. Any result whose id is not in the
    /// batch is rejected by the classifier before the caller ever sees it (whitelist rule).
    /// </summary>
    public required Guid OracleId { get; init; }

    /// <summary>
    /// Card name for cache readability. Populated when storing to cache; may be null when
    /// reconstructed from cache (not essential to functionality, only for human inspection).
    /// </summary>
    public string? CardName { get; init; }

    public required CardRole PrimaryRole { get; init; }

    /// <summary>Secondary roles this card also covers, with relation and coverage weight.</summary>
    public IReadOnlyList<RoleContribution> Secondary { get; init; } = [];

    /// <summary>
    /// MDFC land credit on the 0–1 scale defined by <see cref="EdhDeckBuilder.Agent.Models.FillCandidate.LandCredit"/>.
    /// Zero for all non-MDFC cards. Set by the classifier for MDFC cards; the fill engine
    /// uses it to reduce the basic land reserve when the card is committed.
    /// </summary>
    public double LandCredit { get; init; }

    /// <summary>Convenience: turn this result into a <see cref="RoleProfile"/> for a <c>FillCandidate</c>.</summary>
    public RoleProfile ToRoleProfile() =>
        RoleProfile.Of(PrimaryRole).With([.. Secondary]);
}
