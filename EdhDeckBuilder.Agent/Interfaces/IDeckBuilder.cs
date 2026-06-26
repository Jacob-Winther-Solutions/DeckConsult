using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Agent.Interfaces;

/// <summary>
/// The top-level entry point for building a Commander deck. Orchestrates the full staged
/// pipeline: resolve → gather pool → classify → fill → color-fix → validate → repair.
/// </summary>
/// <remarks>
/// The caller provides archetypes, themes, and a bracket; the builder resolves them into
/// a <c>DeckTemplate</c> internally via <c>TemplateResolver</c> before the fill begins.
/// The caller never reasons about the resolved targets directly.
/// </remarks>
public interface IDeckBuilder
{
    /// <param name="commanders">
    /// One commander normally; two for partner / background pairings.
    /// Must be valid commanders (checked by the caller before invoking the builder).
    /// </param>
    /// <param name="template">
    /// The baseline template to build from (typically <c>DeckTemplate.Balanced</c>).
    /// Archetypes, themes, and bracket are applied as deltas on top of this.
    /// </param>
    /// <param name="archetypes">Weighted archetypes that shift the template targets.</param>
    /// <param name="themes">Weighted themes applied on top of the archetype adjustments. May be empty.</param>
    /// <param name="bracket">Bracket profile applied at weight 1.0. May be null to use the baseline unchanged.</param>
    /// <param name="constraints">Soft guidance forwarded to the LLM selector (curve preference, hints).</param>
    Task<DeckBuildResult> BuildAsync(
        IReadOnlyList<Card> commanders,
        DeckTemplate template,
        IReadOnlyList<WeightedArchetype> archetypes,
        IReadOnlyList<WeightedTheme>? themes = null,
        BracketProfile? bracket = null,
        SoftConstraints? constraints = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}
