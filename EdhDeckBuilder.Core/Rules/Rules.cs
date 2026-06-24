using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Core.Rules;

public enum Severity { Error, Warning }

public sealed record RuleViolation(string Rule, Severity Severity, string Message);

public sealed record DeckValidationResult(IReadOnlyList<RuleViolation> Violations)
{
    public bool IsLegal => !Violations.Any(v => v.Severity == Severity.Error);
    public static DeckValidationResult Ok { get; } = new([]);
}

/// <summary>A single deck-construction rule. Compose many to validate a deck.</summary>
public interface IDeckRule
{
    IEnumerable<RuleViolation> Check(Deck deck);
}

/// <summary>Runs a set of rules and aggregates their violations.</summary>
public sealed class DeckValidator(IEnumerable<IDeckRule> rules)
{
    private readonly IReadOnlyList<IDeckRule> _rules = rules.ToList();

    public DeckValidationResult Validate(Deck deck)
        => new(_rules.SelectMany(r => r.Check(deck)).ToList());

    /// <summary>The standard EDH hard-rule set.</summary>
    public static DeckValidator Standard { get; } = new(
    [
        new DeckSizeRule(),
        new SingletonRule(),
        new ColorIdentityRule(),
        new CommanderRule(),
        new BanlistRule(),
    ]);
}

// --- Hard rules -------------------------------------------------------------

public sealed class DeckSizeRule : IDeckRule
{
    public IEnumerable<RuleViolation> Check(Deck deck)
    {
        if (deck.TotalCards != 100)
            yield return new(nameof(DeckSizeRule), Severity.Error,
                $"Deck has {deck.TotalCards} cards; Commander decks must be exactly 100.");
    }
}

public sealed class SingletonRule : IDeckRule
{
    public IEnumerable<RuleViolation> Check(Deck deck)
    {
        foreach (var slot in deck.Cards)
        {
            if (slot.Card.IsBasicLand) continue;          // basics are exempt from singleton
            if (slot.Quantity > 1)
                yield return new(nameof(SingletonRule), Severity.Error,
                    $"'{slot.Card.Name}' appears {slot.Quantity} times; only one copy is allowed.");
        }

        // Also catch the same non-basic card split across two or more separate slots
        // (the per-slot check above already handles a single slot with quantity > 1).
        var dupes = deck.Cards
            .Where(s => !s.Card.IsBasicLand)
            .GroupBy(s => s.Card.OracleId)
            .Where(g => g.Count() > 1);

        foreach (var g in dupes)
            yield return new(nameof(SingletonRule), Severity.Error,
                $"'{g.First().Card.Name}' appears in more than one slot; only one copy is allowed.");
    }
}

public sealed class ColorIdentityRule : IDeckRule
{
    public IEnumerable<RuleViolation> Check(Deck deck)
    {
        var commander = deck.ColorIdentity;
        foreach (var slot in deck.Cards)
        {
            if (!slot.Card.ColorIdentity.IsWithin(commander))
                yield return new(nameof(ColorIdentityRule), Severity.Error,
                    $"'{slot.Card.Name}' is outside the commander's color identity.");
        }
    }
}

public sealed class CommanderRule : IDeckRule
{
    public IEnumerable<RuleViolation> Check(Deck deck)
    {
        if (deck.Commanders.Count is < 1 or > 2)
            yield return new(nameof(CommanderRule), Severity.Error,
                "A deck must have one commander, or two with partner/background.");

        foreach (var c in deck.Commanders)
            if (!c.CanBeCommander)
                yield return new(nameof(CommanderRule), Severity.Error,
                    $"'{c.Name}' is not a legal commander.");
    }
}

public sealed class BanlistRule : IDeckRule
{
    public IEnumerable<RuleViolation> Check(Deck deck)
    {
        var all = deck.Commanders.Concat(deck.Cards.Select(s => s.Card));
        foreach (var card in all)
            if (card.CommanderLegality is Legality.Banned)
                yield return new(nameof(BanlistRule), Severity.Error,
                    $"'{card.Name}' is banned in Commander.");
    }
}
