using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Agent.Models;

/// <summary>
/// Immutable snapshot of everything the fill engine needs before it starts.
/// Constructed once by the pipeline after the commander has been classified and
/// the template has been resolved; never mutated during the build.
/// </summary>
public sealed record BuildContext
{
    public required IReadOnlyList<Card> Commanders { get; init; }
    public required Color ColorIdentity { get; init; }

    /// <summary>
    /// The template as produced by <c>TemplateResolver</c> — the original coverage targets
    /// before any commander contribution is subtracted. Stored here so <see cref="DeckBuildResult"/>
    /// can show it alongside actual coverage for the user to compare.
    /// </summary>
    public required DeckTemplate ResolvedTemplate { get; init; }

    /// <summary>
    /// Per-role coverage targets after subtracting the commanders' contribution at 1.5× weight.
    /// This is what the fill engine fills toward — not the raw template ideals.
    /// Computed by the pipeline from <see cref="ResolvedTemplate"/> and <see cref="CommanderProfiles"/>.
    /// </summary>
    public required IReadOnlyDictionary<CardRole, RoleTarget> NetTargets { get; init; }

    /// <summary>
    /// Classified role profiles for the commander(s). Used to compute <see cref="NetTargets"/>
    /// and retained here so the selector can use commander context in its prompts.
    /// For partner pairs both profiles are present.
    /// </summary>
    public required IReadOnlyList<RoleProfile> CommanderProfiles { get; init; }

    public required SoftConstraints Constraints { get; init; }

    /// <summary>
    /// How many basic land slots are reserved at the start of the fill (Pass A).
    /// Equals <c>NetTargets[CardRole.Land].Ideal</c>. The fill engine decrements this
    /// as utility lands and MDFCs are committed; the remainder becomes the actual basic count.
    /// </summary>
    public required int ReservedLandCount { get; init; }

    /// <summary>
    /// Total non-commander card slots the fill engine must fill. 99 for a single commander;
    /// 98 for a partner pair (two commanders, 98 + 2 = 100 total deck size).
    /// </summary>
    public int NonCommanderCount { get; init; } = 99;

    /// <summary>
    /// True if the two selected commanders form a legal partner pair (as defined by
    /// <see cref="EdhDeckBuilder.Core.Rules.PartnershipEligibilityRule"/>).
    /// Used by <see cref="DeckBuilder"/> to decide whether to use the EDHREC partner-pair
    /// recommendations endpoint (if available) or fall back to merging single-commander pools.
    /// Defaults to false (safe — always merge).
    /// </summary>
    public bool IsLegalPartnerPair { get; init; } = false;
}
