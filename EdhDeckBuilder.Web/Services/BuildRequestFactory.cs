using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Web.Services;

public sealed record BuildParameters(
    DeckTemplate Template,
    IReadOnlyList<WeightedArchetype> Archetypes,
    IReadOnlyList<WeightedTheme>? Themes,
    BracketProfile? BracketProfile,
    SoftConstraints Constraints);

public static class BuildRequestFactory
{
    public static BuildParameters ForGuided(
        IReadOnlyDictionary<Archetype, double> archetypeWeights,
        IReadOnlyList<WeightedTheme> themes,
        BracketSelection bracket,
        BudgetSelection budget)
    {
        var archetypes    = archetypeWeights
            .Select(kv => new WeightedArchetype(ArchetypeLibrary.All[kv.Key], kv.Value))
            .ToList();
        var themeList     = themes.Count > 0 ? themes : null;
        var bracketProfile = bracket.Enabled ? BracketLibrary.All[bracket.Bracket] : null;
        var curveNote     = archetypeWeights.TryGetValue(Archetype.Aggro, out var w) && w >= 0.5
            ? "Strongly favor threats with mana value ≤3."
            : "";
        var hints = themes
            .Where(wt => !string.IsNullOrWhiteSpace(wt.Profile.Description))
            .Select(wt => $"Theme: {wt.Profile.Name} — {wt.Profile.Description}")
            .ToList();
        return new(
            DeckTemplate.Balanced,
            archetypes,
            themeList,
            bracketProfile,
            new SoftConstraints
            {
                Bracket         = bracket.Enabled ? bracket.Bracket : Bracket.Three,
                CurveNote       = curveNote,
                AdditionalHints = hints,
                MaxCardPriceUsd = budget.MaxCardPriceUsd,
                TotalBudgetUsd  = budget.TotalBudgetUsd,
            });
    }

    public static BuildParameters ForCustom(
        string description,
        IReadOnlyDictionary<CardRole, int> templateValues,
        BudgetSelection budget)
    {
        return new(
            TemplateResolver.FromIdeals("Custom", templateValues),
            [],
            null,
            null,
            new SoftConstraints
            {
                Bracket         = Bracket.Three,
                DeckDescription = description.Trim(),
                MaxCardPriceUsd = budget.MaxCardPriceUsd,
                TotalBudgetUsd  = budget.TotalBudgetUsd,
            });
    }
}
