using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Tests.Core;

public sealed class RoleProfileTests
{
    // --- CoverageFor --------------------------------------------------------

    [Fact]
    public void CoverageFor_primary_role_is_1()
    {
        var profile = RoleProfile.Of(CardRole.Ramp);
        Assert.Equal(1.0, profile.CoverageFor(CardRole.Ramp));
    }

    [Fact]
    public void CoverageFor_unrelated_role_is_0()
    {
        var profile = RoleProfile.Of(CardRole.Ramp);
        Assert.Equal(0.0, profile.CoverageFor(CardRole.CardAdvantage));
    }

    [Fact]
    public void Always_secondary_contributes_full_weight()
    {
        // e.g. Black Market Connections: Ramp AND CardAdvantage simultaneously
        var profile = RoleProfile.Of(CardRole.Ramp)
            .With(RoleContribution.Both(CardRole.CardAdvantage));
        Assert.Equal(1.0, profile.CoverageFor(CardRole.CardAdvantage));
    }

    [Fact]
    public void Modal_secondary_contributes_half_weight_by_default()
    {
        // e.g. Jeska's Will: either Ramp or CardAdvantage depending on board state at cast
        var profile = RoleProfile.Of(CardRole.Ramp)
            .With(RoleContribution.EitherOr(CardRole.CardAdvantage));
        Assert.Equal(0.5, profile.CoverageFor(CardRole.CardAdvantage));
    }

    [Fact]
    public void Transform_secondary_contributes_0_75_by_default()
    {
        // e.g. Hedron Archive: Ramp early, sacrificed to draw later — both roles, not at once
        var profile = RoleProfile.Of(CardRole.Ramp)
            .With(RoleContribution.Switches(CardRole.CardAdvantage));
        Assert.Equal(0.75, profile.CoverageFor(CardRole.CardAdvantage));
    }

    [Fact]
    public void Custom_weight_is_respected()
    {
        var profile = RoleProfile.Of(CardRole.Ramp)
            .With(new RoleContribution(CardRole.Plan, RoleRelation.Always, 0.6));
        Assert.Equal(0.6, profile.CoverageFor(CardRole.Plan), precision: 10);
    }

    [Fact]
    public void Multiple_secondary_contributions_to_same_role_accumulate()
    {
        // Contrived but validates the sum path in CoverageFor
        var profile = RoleProfile.Of(CardRole.Ramp)
            .With(
                RoleContribution.Both(CardRole.CardAdvantage, 0.4),
                RoleContribution.Both(CardRole.CardAdvantage, 0.6));
        Assert.Equal(1.0, profile.CoverageFor(CardRole.CardAdvantage), precision: 10);
    }

    [Fact]
    public void CoverageFor_role_not_present_returns_0()
    {
        var profile = RoleProfile.Of(CardRole.Ramp)
            .With(RoleContribution.Both(CardRole.CardAdvantage));
        Assert.Equal(0.0, profile.CoverageFor(CardRole.Plan));
    }

    // --- AllRoles -----------------------------------------------------------

    [Fact]
    public void AllRoles_includes_primary()
    {
        var profile = RoleProfile.Of(CardRole.Ramp);
        Assert.Contains(CardRole.Ramp, profile.AllRoles());
    }

    [Fact]
    public void AllRoles_includes_each_secondary_role()
    {
        var profile = RoleProfile.Of(CardRole.Ramp)
            .With(RoleContribution.Both(CardRole.CardAdvantage),
                  RoleContribution.EitherOr(CardRole.Plan));
        Assert.Contains(CardRole.CardAdvantage, profile.AllRoles());
        Assert.Contains(CardRole.Plan,          profile.AllRoles());
    }

    [Fact]
    public void AllRoles_contains_no_duplicates()
    {
        var profile = RoleProfile.Of(CardRole.Ramp)
            .With(RoleContribution.Both(CardRole.CardAdvantage),
                  RoleContribution.Both(CardRole.Plan));
        var roles = profile.AllRoles().ToList();
        Assert.Equal(roles.Count, roles.Distinct().Count());
    }

    [Fact]
    public void AllRoles_primary_only_profile_has_one_role()
    {
        var profile = RoleProfile.Of(CardRole.Ramp);
        Assert.Single(profile.AllRoles());
    }

    // --- Factory methods ----------------------------------------------------

    [Fact]
    public void Of_creates_profile_with_no_secondary_contributions()
    {
        var profile = RoleProfile.Of(CardRole.Ramp);
        Assert.Empty(profile.Secondary);
    }

    [Fact]
    public void With_attaches_secondary_contributions()
    {
        var profile = RoleProfile.Of(CardRole.Ramp)
            .With(RoleContribution.Both(CardRole.CardAdvantage),
                  RoleContribution.EitherOr(CardRole.Plan));
        Assert.Equal(2, profile.Secondary.Count);
    }

    [Fact]
    public void With_preserves_primary_role()
    {
        var profile = RoleProfile.Of(CardRole.Ramp)
            .With(RoleContribution.Both(CardRole.CardAdvantage));
        Assert.Equal(CardRole.Ramp, profile.Primary);
    }
}
