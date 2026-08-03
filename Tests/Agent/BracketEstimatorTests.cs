using EdhDeckBuilder.Agent.Analysis;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Tests.Agent;

public sealed class BracketEstimatorTests
{
    private static Dictionary<CardRole, double> Coverage(
        int tutors = 0, int lands = 38, int protection = 3, int cardAdvantage = 10, int massDisrupt = 6)
        => new()
        {
            [CardRole.Tutor]             = tutors,
            [CardRole.Land]              = lands,
            [CardRole.Protection]        = protection,
            [CardRole.CardAdvantage]     = cardAdvantage,
            [CardRole.MassDisruption]    = massDisrupt,
        };

    [Fact]
    public void Estimate_NoTutors_ReturnsBracketOne()
    {
        var (bracket, _) = BracketEstimator.Estimate(Coverage(tutors: 0));

        Assert.Equal(Bracket.One, bracket);
    }

    [Fact]
    public void Estimate_OneTutor_ReturnsBracketTwo()
    {
        var (bracket, _) = BracketEstimator.Estimate(Coverage(tutors: 1));

        Assert.Equal(Bracket.Two, bracket);
    }

    [Fact]
    public void Estimate_TwoTutors_ReturnsBracketThree()
    {
        var (bracket, _) = BracketEstimator.Estimate(Coverage(tutors: 2));

        Assert.Equal(Bracket.Three, bracket);
    }

    [Fact]
    public void Estimate_FiveTutors_ReturnsBracketFour()
    {
        var (bracket, _) = BracketEstimator.Estimate(Coverage(tutors: 5));

        Assert.Equal(Bracket.Four, bracket);
    }

    [Fact]
    public void Estimate_EightTutors_ReturnsBracketFive()
    {
        var (bracket, _) = BracketEstimator.Estimate(Coverage(tutors: 8));

        Assert.Equal(Bracket.Five, bracket);
    }

    [Fact]
    public void Estimate_FourTutorsHighProtection_ReturnsBracketFour()
    {
        // 4 tutors + 5 protection → Bracket 4
        var (bracket, _) = BracketEstimator.Estimate(Coverage(tutors: 4, protection: 5));

        Assert.Equal(Bracket.Four, bracket);
    }

    [Fact]
    public void Estimate_OneTutorLowLands_ReturnsBracketThree()
    {
        // 1 tutor but only 35 lands (fast mana) → Bracket 3
        var (bracket, _) = BracketEstimator.Estimate(Coverage(tutors: 1, lands: 35));

        Assert.Equal(Bracket.Three, bracket);
    }

    [Fact]
    public void Estimate_AlwaysReturnsExplanation()
    {
        foreach (var tutorCount in new[] { 0, 1, 2, 5, 8 })
        {
            var (_, explanation) = BracketEstimator.Estimate(Coverage(tutors: tutorCount));
            Assert.False(string.IsNullOrWhiteSpace(explanation));
        }
    }

    [Fact]
    public void Estimate_EmptyCoverage_ReturnsBracketOne()
    {
        var (bracket, _) = BracketEstimator.Estimate(new Dictionary<CardRole, double>());

        Assert.Equal(Bracket.One, bracket);
    }
}
