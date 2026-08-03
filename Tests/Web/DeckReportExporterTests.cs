using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Web.Services;

namespace EdhDeckBuilder.Tests.Web;

public sealed class DeckReportExporterTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Card MakeCard(string name, CardType type = CardType.Instant,
        Color identity = Color.Green, decimal? price = null) => new()
    {
        ScryfallId        = Guid.NewGuid(),
        OracleId          = Guid.NewGuid(),
        Name              = name,
        TypeLine          = type.ToString(),
        Types             = type,
        ColorIdentity     = identity,
        CommanderLegality = Legality.Legal,
        PriceUsd          = price,
    };

    private static CardSuggestion MakeSuggestion(Card card, CardRole role, string reason, int rank = 1,
        IReadOnlyList<RoleContribution>? secondary = null) => new()
    {
        Card   = card,
        Roles  = new RoleProfile { Primary = role, Secondary = secondary ?? [] },
        Reason = reason,
        Rank   = rank,
    };

    private static DeckBuildResult MakeResult(
        IReadOnlyList<CardSuggestion> deck,
        IReadOnlyDictionary<string, int>? basics = null,
        IReadOnlyList<CardCandidate>? runnerUps = null,
        decimal totalPrice = 0m) => new()
    {
        Deck             = deck,
        BasicLandCounts  = basics ?? new Dictionary<string, int> { ["Forest"] = 20 },
        RunnerUps        = runnerUps ?? [],
        PlannedTemplate  = DeckTemplate.Balanced,
        ActualCoverage   = deck
            .GroupBy(s => s.Roles.Primary)
            .ToDictionary(g => g.Key, g => (double)g.Count()),
        CoverageWarnings = [],
        CutSuggestions   = new Dictionary<CardRole, IReadOnlyList<CardSuggestion>>(),
        TotalPriceUsd    = totalPrice > 0 ? totalPrice : deck.Sum(s => s.Card.PriceUsd ?? 0m),
        BudgetWarnings   = [],
    };

    private static IReadOnlyDictionary<Archetype, double> NoArchetypes =>
        new Dictionary<Archetype, double>();

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Export_ContainsHeader_WithCommanderAndDate()
    {
        var commander = MakeCard("Atraxa, Praetors' Voice", CardType.Creature);
        var card      = MakeSuggestion(MakeCard("Sol Ring", CardType.Artifact), CardRole.Ramp,
            "Fast mana for any deck.");
        var result = MakeResult([card]);

        var report = DeckReportExporter.Export(
            result, [commander],
            new Dictionary<Archetype, double> { [Archetype.Midrange] = 1.0 },
            themes: null, Bracket.Three, maxCardPriceUsd: null, totalBudgetUsd: null,
            new DateOnly(2026, 7, 1));

        Assert.Contains("# Build Report — Atraxa, Praetors' Voice", report);
        Assert.Contains("**Date:** 2026-07-01", report);
        Assert.Contains("**Bracket:** 3 —", report);
        Assert.Contains("**Archetype:** Midrange", report);
    }

    [Fact]
    public void Export_ContainsRoleBucket_WithCardAndReason()
    {
        var card   = MakeSuggestion(MakeCard("Sol Ring", CardType.Artifact), CardRole.Ramp,
            "Fast mana for any deck.");
        var result = MakeResult([card]);

        var report = DeckReportExporter.Export(
            result, [MakeCard("Commander X", CardType.Creature)],
            NoArchetypes, themes: null, Bracket.Three,
            maxCardPriceUsd: null, totalBudgetUsd: null,
            new DateOnly(2026, 7, 1));

        Assert.Contains("### Ramp", report);
        Assert.Contains("**Sol Ring**", report);
        Assert.Contains("Fast mana for any deck.", report);
    }

    [Fact]
    public void Export_ContainsCoverageSummaryTable()
    {
        var card   = MakeSuggestion(MakeCard("Sol Ring", CardType.Artifact), CardRole.Ramp, "Fast mana.");
        var result = MakeResult([card]);

        var report = DeckReportExporter.Export(
            result, [MakeCard("Commander X", CardType.Creature)],
            NoArchetypes, themes: null, Bracket.Three,
            maxCardPriceUsd: null, totalBudgetUsd: null,
            new DateOnly(2026, 7, 1));

        Assert.Contains("## Coverage Summary", report);
        Assert.Contains("| Role | Target", report);
        Assert.Contains("| Ramp |", report);
    }

    [Fact]
    public void Export_ContainsBasicLands()
    {
        var card   = MakeSuggestion(MakeCard("Cultivate", CardType.Sorcery), CardRole.Ramp, "Ramp.");
        var result = MakeResult([card], new Dictionary<string, int> { ["Forest"] = 18, ["Plains"] = 4 });

        var report = DeckReportExporter.Export(
            result, [MakeCard("Commander X", CardType.Creature)],
            NoArchetypes, themes: null, Bracket.Three,
            maxCardPriceUsd: null, totalBudgetUsd: null,
            new DateOnly(2026, 7, 1));

        Assert.Contains("18× Forest", report);
        Assert.Contains("4× Plains", report);
    }

    [Fact]
    public void Export_ContainsRawDecklist_ReadyToPaste()
    {
        var commander = MakeCard("Korvold, Fae-Cursed King", CardType.Creature);
        var card      = MakeSuggestion(MakeCard("Sol Ring", CardType.Artifact), CardRole.Ramp, "Fast mana.");
        var result    = MakeResult([card]);

        var report = DeckReportExporter.Export(
            result, [commander],
            NoArchetypes, themes: null, Bracket.Three,
            maxCardPriceUsd: null, totalBudgetUsd: null,
            new DateOnly(2026, 7, 1));

        Assert.Contains("## Raw Decklist", report);
        Assert.Contains("1 Korvold, Fae-Cursed King", report);
        Assert.Contains("1 Sol Ring", report);
        Assert.Contains("20 Forest", report);
    }

    [Fact]
    public void Export_ShowsBudget_WhenSet()
    {
        var card   = MakeSuggestion(MakeCard("Mox Diamond", CardType.Artifact, price: 150m),
            CardRole.Ramp, "Expensive but powerful.");
        var result = MakeResult([card], totalPrice: 150m);

        var report = DeckReportExporter.Export(
            result, [MakeCard("Commander X", CardType.Creature)],
            NoArchetypes, themes: null, Bracket.Four,
            maxCardPriceUsd: 5m, totalBudgetUsd: 200m,
            new DateOnly(2026, 7, 1));

        Assert.Contains("**Budget:** Max per card: $5.00 | Total deck: $200.00", report);
        Assert.Contains("**Total price:** $150.00", report);
    }

    [Fact]
    public void Export_ShowsThemes_WhenProvided()
    {
        var themes = new List<WeightedTheme>
        {
            new(ThemeLibrary.All[Theme.Aristocrats], 1.0),
            new(ThemeLibrary.All[Theme.BigMana], 0.5),
        };
        var card   = MakeSuggestion(MakeCard("Sol Ring", CardType.Artifact), CardRole.Ramp, "Ramp.");
        var result = MakeResult([card]);

        var report = DeckReportExporter.Export(
            result, [MakeCard("Commander X", CardType.Creature)],
            NoArchetypes, themes, Bracket.Three,
            maxCardPriceUsd: null, totalBudgetUsd: null,
            new DateOnly(2026, 7, 1));

        Assert.Contains("**Themes:**", report);
        Assert.Contains("Aristocrats", report);
        Assert.Contains("Big Mana (half)", report);
    }

    [Fact]
    public void ExportDecklist_ContainsCommanderAndDeckCards()
    {
        var commander = MakeCard("Korvold, Fae-Cursed King", CardType.Creature);
        var card      = MakeSuggestion(MakeCard("Sol Ring", CardType.Artifact), CardRole.Ramp, "Ramp.");
        var result    = MakeResult([card]);

        var decklist = DeckReportExporter.ExportDecklist(result, [commander]);

        Assert.Contains("1 Korvold, Fae-Cursed King", decklist);
        Assert.Contains("1 Sol Ring", decklist);
        Assert.Contains("20 Forest", decklist);
    }

    [Fact]
    public void ExportDecklist_CommanderAppearsBeforeDeckCards()
    {
        var commander = MakeCard("Atraxa, Praetors' Voice", CardType.Creature);
        var card      = MakeSuggestion(MakeCard("Sol Ring", CardType.Artifact), CardRole.Ramp, "Ramp.");
        var result    = MakeResult([card]);

        var decklist = DeckReportExporter.ExportDecklist(result, [commander]);

        Assert.True(decklist.IndexOf("Atraxa", StringComparison.Ordinal)
                  < decklist.IndexOf("Sol Ring", StringComparison.Ordinal));
    }

    [Fact]
    public void ExportDecklist_DoesNotContainMarkdownOrReportContent()
    {
        var commander = MakeCard("Atraxa, Praetors' Voice", CardType.Creature);
        var card      = MakeSuggestion(MakeCard("Sol Ring", CardType.Artifact), CardRole.Ramp, "Ramp.");
        var result    = MakeResult([card]);

        var decklist = DeckReportExporter.ExportDecklist(result, [commander]);

        Assert.DoesNotContain("#", decklist);
        Assert.DoesNotContain("**", decklist);
        Assert.DoesNotContain("Coverage", decklist);
    }

    [Fact]
    public void ExportDecklist_PartnerPair_IncludesBothCommanders()
    {
        var commanders = new[]
        {
            MakeCard("Akiri, Line-Slinger",    CardType.Creature),
            MakeCard("Bruse Tarl, Boorish Herder", CardType.Creature),
        };
        var card   = MakeSuggestion(MakeCard("Sol Ring", CardType.Artifact), CardRole.Ramp, "Ramp.");
        var result = MakeResult([card]);

        var decklist = DeckReportExporter.ExportDecklist(result, commanders);

        Assert.Contains("1 Akiri, Line-Slinger", decklist);
        Assert.Contains("1 Bruse Tarl, Boorish Herder", decklist);
    }

    [Fact]
    public void SlugifyFilename_ProducesCleanSlug()
    {
        var commanders = new[] { MakeCard("Atraxa, Praetors' Voice") };
        Assert.Equal("atraxa-praetors-voice", DeckReportExporter.SlugifyFilename(commanders));
    }

    [Fact]
    public void SlugifyFilename_PartnerPair_IncludesBothNames()
    {
        var commanders = new[]
        {
            MakeCard("Akiri, Line-Slinger"),
            MakeCard("Bruse Tarl, Boorish Herder"),
        };
        var slug = DeckReportExporter.SlugifyFilename(commanders);
        Assert.Contains("akiri", slug);
        Assert.Contains("bruse", slug);
    }
}
