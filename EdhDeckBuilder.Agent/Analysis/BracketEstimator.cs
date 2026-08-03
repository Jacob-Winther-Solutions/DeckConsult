using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Agent.Analysis;

/// <summary>
/// Estimates the Commander bracket (1–5) from a deck's role-coverage profile.
/// Tutors are the primary signal because they are the clearest proxy for optimization intent.
/// Land count and protection are secondary signals that confirm higher brackets.
/// </summary>
public static class BracketEstimator
{
    public static (Bracket Bracket, string Explanation) Estimate(
        IReadOnlyDictionary<CardRole, double> coverage)
    {
        int tutors       = (int)Math.Round(coverage.GetValueOrDefault(CardRole.Tutor));
        int lands        = (int)Math.Round(coverage.GetValueOrDefault(CardRole.Land));
        int protection   = (int)Math.Round(coverage.GetValueOrDefault(CardRole.Protection));
        int cardAdvantage = (int)Math.Round(coverage.GetValueOrDefault(CardRole.CardAdvantage));
        int massDisrupt  = (int)Math.Round(coverage.GetValueOrDefault(CardRole.MassDisruption));

        if (tutors >= 8)
            return (Bracket.Five,
                $"{tutors} tutors, {protection} protection pieces, and {lands} lands point to a cEDH-optimized build.");

        if (tutors >= 5 || (tutors >= 4 && protection >= 5))
            return (Bracket.Four,
                $"{tutors} tutors and {protection} protection pieces indicate a powerful, tutor-dense deck.");

        if (tutors >= 2 || (tutors >= 1 && lands <= 35))
            return (Bracket.Three,
                $"{tutors} tutor(s), {cardAdvantage} card advantage sources, and {massDisrupt} board wipe(s) indicate an optimized, consistent deck.");

        if (tutors == 1)
            return (Bracket.Two,
                $"{tutors} tutor alongside focused disruption indicates a mid-power build above pre-constructed level.");

        return (Bracket.One,
            $"No tutors and {massDisrupt} board wipe(s) point to a casual build without combo or fast-mana infrastructure.");
    }
}
