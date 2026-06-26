using EdhDeckBuilder.Agent.Fill;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Agent.Pipeline;

/// <summary>
/// Post-fill assembly. Swaps out any cards that violate the deck's color identity, then
/// converts the populated <see cref="BuildState"/> into a <see cref="DeckBuildResult"/>.
/// Called by <see cref="DeckBuilder"/> after <see cref="ColorFixingPass"/> has run.
/// </summary>
internal static class RepairEngine
{
    /// <summary>
    /// Removes any committed cards whose color identity lies outside the commander's, replacing
    /// each with the highest-inclusion legal alternative of the same primary role from the pool.
    /// If no replacement is found the slot is left empty (deck is short; covered by coverage warnings).
    /// </summary>
    internal static void RepairIllegalCards(
        BuildContext context,
        BuildState state,
        IReadOnlyList<FillCandidate> pool)
    {
        var committedIds = state.CommittedCandidates.Keys.ToHashSet();

        var illegal = state.CommittedCandidates.Values
            .Where(c => !c.Card.ColorIdentity.IsWithin(context.ColorIdentity))
            .ToList();

        foreach (var illegalCard in illegal)
        {
            state.Remove(illegalCard.Card.OracleId);
            committedIds.Remove(illegalCard.Card.OracleId);

            // Best replacement: same primary role, legal, within CI, not yet committed.
            var replacement = pool
                .Where(c => !committedIds.Contains(c.Card.OracleId)
                         && c.Card.ColorIdentity.IsWithin(context.ColorIdentity)
                         && c.Card.CommanderLegality == Legality.Legal
                         && c.Roles.Primary == illegalCard.Roles.Primary)
                .OrderByDescending(c => c.Candidate.Inclusion)
                .ThenBy(c => c.Card.OracleId)
                .FirstOrDefault()
                // Fallback: same land/spell category, any role.
                ?? pool
                    .Where(c => !committedIds.Contains(c.Card.OracleId)
                             && c.Card.ColorIdentity.IsWithin(context.ColorIdentity)
                             && c.Card.CommanderLegality == Legality.Legal
                             && c.Card.Types.HasFlag(CardType.Land) == illegalCard.Card.Types.HasFlag(CardType.Land))
                    .OrderByDescending(c => c.Candidate.Inclusion)
                    .ThenBy(c => c.Card.OracleId)
                    .FirstOrDefault();

            if (replacement is not null)
            {
                state.Commit(replacement);
                committedIds.Add(replacement.Card.OracleId);
            }
        }
    }

    /// <summary>
    /// Constructs the final <see cref="DeckBuildResult"/> from the completed build state.
    /// </summary>
    internal static DeckBuildResult Assemble(
        BuildContext context,
        FillResult fillResult,
        IReadOnlyList<string> fixingWarnings,
        IReadOnlyList<FillCandidate> pool,
        DeckTemplate resolvedTemplate,
        IReadOnlyDictionary<string, int> basicLandCounts)
    {
        var state = fillResult.State;
        var committedIds = state.CommittedCandidates.Keys.ToHashSet();

        var deck = BuildCardSuggestions(state.Committed, fillResult.SelectionRationales);

        var runnerUps = pool
            .Where(fc => !committedIds.Contains(fc.Card.OracleId)
                      && !fc.Card.Types.HasFlag(CardType.Land)
                      && fc.Card.CommanderLegality == Legality.Legal)
            .OrderByDescending(fc => fc.Candidate.Inclusion)
            .Select(fc => fc.Candidate)
            .Take(30)
            .ToList();

        var cutSuggestions = BuildCutSuggestions(context, deck, state);

        var allWarnings = new List<string>(fillResult.Warnings);
        allWarnings.AddRange(fixingWarnings);

        return new DeckBuildResult
        {
            Deck              = deck,
            BasicLandCounts   = basicLandCounts,
            RunnerUps         = runnerUps,
            PlannedTemplate   = resolvedTemplate,
            ActualCoverage    = new Dictionary<CardRole, double>(state.Coverage),
            CoverageWarnings  = allWarnings,
            CutSuggestions    = cutSuggestions,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<CardSuggestion> BuildCardSuggestions(
        IReadOnlyList<DeckSlot> committed,
        IReadOnlyDictionary<Guid, string> rationales)
    {
        var result = new List<CardSuggestion>(committed.Count);

        foreach (var group in committed.GroupBy(s => s.PrimaryRole))
        {
            // LLM-selected cards first (have rationale), then stable by name.
            var ordered = group
                .OrderByDescending(s => rationales.ContainsKey(s.Card.OracleId) ? 1 : 0)
                .ThenBy(s => s.Card.Name)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                var slot = ordered[i];
                result.Add(new CardSuggestion
                {
                    Card   = slot.Card,
                    Roles  = slot.Roles,
                    Reason = rationales.TryGetValue(slot.Card.OracleId, out var reason)
                        ? reason
                        : $"Selected from the top EDHREC recommendations for {slot.PrimaryRole}.",
                    Rank = i + 1,
                });
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<CardRole, IReadOnlyList<CardSuggestion>> BuildCutSuggestions(
        BuildContext context,
        IReadOnlyList<CardSuggestion> deck,
        BuildState state)
    {
        var result = new Dictionary<CardRole, IReadOnlyList<CardSuggestion>>();

        foreach (var (role, target) in context.NetTargets)
        {
            if (state.Coverage.GetValueOrDefault(role) <= target.Max) continue;

            var weakest = deck
                .Where(s => s.Roles.Primary == role)
                .OrderByDescending(s => s.Rank)   // highest rank number = weakest fit
                .Take(5)
                .ToList();

            if (weakest.Count > 0)
                result[role] = weakest;
        }

        return result;
    }
}
