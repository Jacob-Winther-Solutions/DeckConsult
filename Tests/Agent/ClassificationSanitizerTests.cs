using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Tests.Agent;

public sealed class ClassificationSanitizerTests
{
    private static ClassificationResult Make(
        CardRole primary,
        params RoleContribution[] secondary) => new()
    {
        OracleId    = Guid.NewGuid(),
        PrimaryRole = primary,
        Secondary   = secondary,
        LandCredit  = 0,
    };

    [Fact]
    public void SanitizeLandRole_corrects_land_primary_on_artifact_to_ramp()
    {
        var result = Make(CardRole.Land);
        var corrected = ClassificationSanitizer.SanitizeLandRole(result, CardType.Artifact);

        Assert.Equal(CardRole.Ramp, corrected.PrimaryRole);
    }

    [Fact]
    public void SanitizeLandRole_strips_land_from_secondary_on_non_land_card()
    {
        var landSecondary = new RoleContribution(CardRole.Land, RoleRelation.Always, 0.5);
        var rampSecondary = new RoleContribution(CardRole.Ramp, RoleRelation.Always, 0.8);
        var result = Make(CardRole.Synergy, landSecondary, rampSecondary);

        var corrected = ClassificationSanitizer.SanitizeLandRole(result, CardType.Artifact);

        Assert.DoesNotContain(corrected.Secondary, s => s.Role == CardRole.Land);
        Assert.Contains(corrected.Secondary, s => s.Role == CardRole.Ramp);
    }

    [Fact]
    public void SanitizeLandRole_leaves_actual_land_card_unchanged()
    {
        var result = Make(CardRole.Land);
        var corrected = ClassificationSanitizer.SanitizeLandRole(result, CardType.Land);

        Assert.Equal(CardRole.Land, corrected.PrimaryRole);
        Assert.Same(result, corrected);
    }

    [Fact]
    public void SanitizeLandRole_returns_same_instance_when_no_land_role_present()
    {
        var result = Make(CardRole.Ramp, new RoleContribution(CardRole.Synergy, RoleRelation.Always, 0.3));
        var corrected = ClassificationSanitizer.SanitizeLandRole(result, CardType.Artifact);

        Assert.Same(result, corrected);
    }
}
