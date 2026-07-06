namespace EdhDeckBuilder.Core.Partnerships;

/// <summary>
/// An immutable pair of cards that form a valid partnership in Commander.
/// Stored by OracleId for cacheability and clarity; callers can resolve full cards from repository if needed.
/// </summary>
public sealed record PartnerCombo(
    Guid FirstCardId,
    Guid SecondCardId,
    PartnershipType Type,
    string FirstKeyword,
    string? SecondKeyword = null
)
{
    /// <summary>
    /// Convenience property: returns an unordered set of both card IDs.
    /// Useful for deduplication (Thrasios+Tymna and Tymna+Thrasios are the same combo).
    /// </summary>
    public HashSet<Guid> CardIds => [FirstCardId, SecondCardId];

    /// <summary>
    /// Convenience: returns true if the combo contains both specified card IDs (order-agnostic).
    /// </summary>
    public bool Contains(Guid cardId) => FirstCardId == cardId || SecondCardId == cardId;

    /// <summary>
    /// Convenience: returns the opposite card in the combo (the one that is not the given ID).
    /// Throws if the given ID is not in this combo.
    /// </summary>
    public Guid OtherCard(Guid cardId)
    {
        if (FirstCardId == cardId) return SecondCardId;
        if (SecondCardId == cardId) return FirstCardId;
        throw new ArgumentException($"Card {cardId} is not in this combo.", nameof(cardId));
    }
}
