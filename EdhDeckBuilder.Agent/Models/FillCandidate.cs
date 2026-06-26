using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Models;

/// <summary>
/// A card from the candidate pool that has been classified and is ready for the fill loop.
/// Carries both the raw EDHREC signal (<see cref="Candidate"/>) and the LLM-assigned
/// role profile, so the fill engine can reason about coverage without re-querying.
/// </summary>
public sealed record FillCandidate
{
    public required CardCandidate Candidate { get; init; }
    public required RoleProfile Roles { get; init; }

    /// <summary>
    /// How much of a basic land slot this card offsets, on a 0–1 scale.
    /// <list type="bullet">
    ///   <item>0 — regular spell; no land-slot interaction.</item>
    ///   <item>0 — utility land (e.g. Reliquary Tower); the land itself occupies a land slot
    ///         directly, so no additional offset is needed — the slot is consumed on commit.</item>
    ///   <item>0–1 — MDFC with a land back: the card takes a <em>spell</em> slot (front face is
    ///         the primary play), but its land back reduces reliance on dedicated land slots.
    ///         <b>Below 0.5</b> = more spell than land (e.g. Agadeem's Awakening ≈ 0.3 — the
    ///         spell is the main reason to include it). <b>Above 0.5</b> = more land than spell
    ///         (e.g. a weak cantrip whose land back you'll use most of the time).</item>
    /// </list>
    /// The fill engine subtracts this credit from the basic land reserve when the card is
    /// committed, so the Pass B accounting stays accurate without a separate tracking pass.
    /// </summary>
    public double LandCredit { get; init; }

    public Card Card => Candidate.Card;
}
