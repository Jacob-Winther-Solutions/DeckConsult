using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Partnerships;

namespace EdhDeckBuilder.Core.Abstractions;

/// <summary>
/// Determines whether two cards can legally form a partnership in Commander.
/// Implementations are deterministic — no state, no external calls. This is a strategy interface
/// to support current and future partnership variants (Partner, Partner with, Background, Friends Forever, etc.).
/// </summary>
public interface IPartnershipEligibilityRule
{
    /// <summary>
    /// Evaluates whether two cards form a valid partnership based on their keywords.
    /// </summary>
    /// <param name="first">The first card in the potential partnership.</param>
    /// <param name="second">The second card in the potential partnership.</param>
    /// <param name="firstKeyword">The partnership keyword extracted from the first card's oracle text.</param>
    /// <param name="secondKeyword">The partnership keyword extracted from the second card's oracle text (may be null if asymmetric).</param>
    /// <returns>True if the cards form a legal partnership; otherwise false.</returns>
    bool CanPartner(Card first, Card second, string firstKeyword, string? secondKeyword);

    /// <summary>
    /// Set of Scryfall keywords this rule recognizes and validates.
    /// Used during ingestion to quickly identify candidate partnership keywords.
    /// Examples: "Partner", "Background", "Friends Forever", "Doctor's Companion".
    /// </summary>
    IReadOnlySet<string> SupportedKeywords { get; }
}
