using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Tests.Core;

public sealed class TemplateResolverTests
{
    // --- baseline behaviour -------------------------------------------------

    [Fact]
    public void Balanced_template_coverage_ideals_exceed_deck_size()
    {
        // Coverage targets intentionally sum above 99; overlap fills the gap physically.
        var result = TemplateResolver.Resolve(DeckTemplate.Balanced, []);
        Assert.True(result.Targets.Values.Sum(t => t.Ideal) > 99);
    }

    [Fact]
    public void Balanced_template_has_positive_plan_coverage()
    {
        var result = TemplateResolver.Resolve(DeckTemplate.Balanced, []);
        Assert.True(result.Targets[CardRole.Plan].Ideal > 0);
    }

    [Fact]
    public void Land_count_is_within_default_min_max_range()
    {
        var result = TemplateResolver.Resolve(DeckTemplate.Balanced, []);
        Assert.InRange(result.Targets[CardRole.Land].Ideal, 30, 45);
    }

    // --- archetypes ---------------------------------------------------------

    [Fact]
    public void Aggro_archetype_stays_within_land_range()
    {
        // Aggro applies -3 to lands (38-3=35); clamped floor is 30
        var result = TemplateResolver.Resolve(DeckTemplate.Balanced,
            [new WeightedArchetype(ArchetypeLibrary.Get(Archetype.Aggro))]);

        Assert.InRange(result.Targets[CardRole.Land].Ideal, 30, 45);
    }

    [Fact]
    public void Control_archetype_increases_targeted_disruption_above_baseline()
    {
        var baseline = TemplateResolver.Resolve(DeckTemplate.Balanced, []);
        var control  = TemplateResolver.Resolve(DeckTemplate.Balanced,
            [new WeightedArchetype(ArchetypeLibrary.Get(Archetype.Control))]);

        Assert.True(control.Targets[CardRole.TargetedDisruption].Ideal
                    > baseline.Targets[CardRole.TargetedDisruption].Ideal);
    }

    [Fact]
    public void Blended_archetypes_each_contribute_plan_coverage()
    {
        var result = TemplateResolver.Resolve(DeckTemplate.Balanced,
        [
            new WeightedArchetype(ArchetypeLibrary.Get(Archetype.Aggro)),
            new WeightedArchetype(ArchetypeLibrary.Get(Archetype.Control)),
        ]);
        Assert.True(result.Targets[CardRole.Plan].Ideal > 0);
    }

    [Fact]
    public void Result_name_contains_each_archetype_name()
    {
        var result = TemplateResolver.Resolve(DeckTemplate.Balanced,
        [
            new WeightedArchetype(ArchetypeLibrary.Get(Archetype.Aggro)),
            new WeightedArchetype(ArchetypeLibrary.Get(Archetype.Control)),
        ]);
        Assert.Contains("Aggro",   result.Name);
        Assert.Contains("Control", result.Name);
    }

    [Fact]
    public void Partial_weight_archetype_applies_proportional_adjustment()
    {
        var full     = TemplateResolver.Resolve(DeckTemplate.Balanced,
            [new WeightedArchetype(ArchetypeLibrary.Get(Archetype.Control), Weight: 1.0)]);
        var half     = TemplateResolver.Resolve(DeckTemplate.Balanced,
            [new WeightedArchetype(ArchetypeLibrary.Get(Archetype.Control), Weight: 0.5)]);
        var baseline = TemplateResolver.Resolve(DeckTemplate.Balanced, []);

        // Half-weight disruption should be between baseline and full-weight Control
        Assert.True(half.Targets[CardRole.TargetedDisruption].Ideal
                    >= baseline.Targets[CardRole.TargetedDisruption].Ideal);
        Assert.True(half.Targets[CardRole.TargetedDisruption].Ideal
                    <= full.Targets[CardRole.TargetedDisruption].Ideal);
    }

    // --- themes -------------------------------------------------------------

    [Fact]
    public void BigMana_theme_increases_ramp_above_baseline()
    {
        var baseline = TemplateResolver.Resolve(DeckTemplate.Balanced, []);
        var bigMana  = TemplateResolver.Resolve(DeckTemplate.Balanced, [],
            themes: [new WeightedTheme(ThemeLibrary.Get(Theme.BigMana))]);

        Assert.True(bigMana.Targets[CardRole.Ramp].Ideal > baseline.Targets[CardRole.Ramp].Ideal);
    }

    [Fact]
    public void Reanimator_theme_increases_recursion_above_baseline()
    {
        var baseline    = TemplateResolver.Resolve(DeckTemplate.Balanced, []);
        var reanimator  = TemplateResolver.Resolve(DeckTemplate.Balanced, [],
            themes: [new WeightedTheme(ThemeLibrary.Get(Theme.Reanimator))]);

        Assert.True(reanimator.Targets[CardRole.Recursion].Ideal
                    > baseline.Targets.GetValueOrDefault(CardRole.Recursion).Ideal);
    }

    [Fact]
    public void Tokens_theme_increases_plan_coverage()
    {
        var baseline = TemplateResolver.Resolve(DeckTemplate.Balanced, []);
        var tokens   = TemplateResolver.Resolve(DeckTemplate.Balanced, [],
            themes: [new WeightedTheme(ThemeLibrary.Get(Theme.Tokens))]);

        Assert.True(tokens.Targets[CardRole.Plan].Ideal > baseline.Targets[CardRole.Plan].Ideal);
    }

    [Fact]
    public void Archetype_and_theme_combined_coverage_still_exceeds_deck_size()
    {
        var result = TemplateResolver.Resolve(DeckTemplate.Balanced,
            archetypes: [new WeightedArchetype(ArchetypeLibrary.Get(Archetype.Aggro))],
            themes:     [new WeightedTheme(ThemeLibrary.Get(Theme.Tokens))]);

        Assert.True(result.Targets.Values.Sum(t => t.Ideal) > 99);
    }

    [Fact]
    public void Result_name_includes_both_archetype_and_theme_name()
    {
        var result = TemplateResolver.Resolve(DeckTemplate.Balanced,
            archetypes: [new WeightedArchetype(ArchetypeLibrary.Get(Archetype.Aggro))],
            themes:     [new WeightedTheme(ThemeLibrary.Get(Theme.Tokens))]);

        Assert.Contains("Aggro",  result.Name);
        Assert.Contains("Tokens", result.Name);
    }

    // --- custom land bounds -------------------------------------------------

    [Fact]
    public void Custom_minLands_is_respected()
    {
        // Force a very high floor; baseline 38 lands → clamped up to 44
        var result = TemplateResolver.Resolve(DeckTemplate.Balanced, [], minLands: 44, maxLands: 48);
        Assert.InRange(result.Targets[CardRole.Land].Ideal, 44, 48);
    }

    [Fact]
    public void Custom_maxLands_is_respected_for_BigMana()
    {
        // BigMana pushes lands to 40; with maxLands: 39 it should be clamped down
        var result = TemplateResolver.Resolve(DeckTemplate.Balanced, [],
            themes:   [new WeightedTheme(ThemeLibrary.Get(Theme.BigMana))],
            maxLands: 39);
        Assert.True(result.Targets[CardRole.Land].Ideal <= 39);
    }
}
