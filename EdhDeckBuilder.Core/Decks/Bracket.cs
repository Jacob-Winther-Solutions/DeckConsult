using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Core.Decks;

/// <summary>
/// The five Commander power-level brackets defined by the Rules Committee.
/// Brackets describe the social contract at the table — they are not format-legal enforcement.
/// A deck with any Game Changer card is at minimum Bracket 3; multiple Game Changers imply
/// Bracket 4; a maximally optimised combo deck is Bracket 5 (cEDH).
/// </summary>
public enum Bracket
{
    One   = 1,  // Casual: simple strategies, no combos, no fast mana, no MLD
    Two   = 2,  // Mid Power: some efficiency, occasional powerful cards
    Three = 3,  // Optimised: game changers present, consistent combos acceptable
    Four  = 4,  // Powerful: multiple game changers, efficient tutors and fast mana
    Five  = 5,  // cEDH: maximally optimised, fast mana + tutors as baseline
}

/// <summary>
/// A bracket's effect on deck construction targets — expressed as a delta over the neutral
/// baseline, parallel to <see cref="ArchetypeProfile"/> and <see cref="ThemeProfile"/>.
/// Higher brackets tighten land counts (fast mana compensates) and add tutors/protection.
/// </summary>
public sealed record BracketProfile
{
    public required Bracket Bracket { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required IReadOnlyDictionary<CardRole, int> Adjustments { get; init; }
}

public static class BracketLibrary
{
    public static IReadOnlyDictionary<Bracket, BracketProfile> All { get; } =
        new[]
        {
            new BracketProfile
            {
                Bracket = Bracket.One,
                Name = "Casual",
                Description = "Simple strategies. No combos, no MLD, no fast mana, no game changers.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Land]               = +2,
                    [CardRole.TargetedDisruption] = -2,
                    [CardRole.MassDisruption]     = -1,
                    [CardRole.Tutor]              = -3,
                    [CardRole.Protection]         = -1,
                },
            },
            new BracketProfile
            {
                Bracket = Bracket.Two,
                Name = "Mid Power",
                Description = "Slightly above precon. Occasional powerful cards, limited tutors.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Land]               = +1,
                    [CardRole.TargetedDisruption] = -1,
                    [CardRole.Tutor]              = -1,
                },
            },
            new BracketProfile
            {
                Bracket = Bracket.Three,
                Name = "Optimised",
                Description = "Efficient decks. Game changers expected, consistent combos acceptable.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Tutor]         = +1,
                    [CardRole.CardAdvantage] = +1,
                },
            },
            new BracketProfile
            {
                Bracket = Bracket.Four,
                Name = "Powerful",
                Description = "Multiple game changers, efficient fast mana, tutor-dense.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Land]               = -3,
                    [CardRole.Tutor]              = +4,
                    [CardRole.Protection]         = +2,
                    [CardRole.CardAdvantage]      = +2,
                },
            },
            new BracketProfile
            {
                Bracket = Bracket.Five,
                Name = "cEDH",
                Description = "Maximally optimised. Fast mana and tutors are table stakes.",
                Adjustments = new Dictionary<CardRole, int>
                {
                    [CardRole.Land]               = -7,
                    [CardRole.Ramp]               = -2,  // fast mana artifacts replace ramp spells
                    [CardRole.Tutor]              = +6,
                    [CardRole.Protection]         = +3,
                    [CardRole.CardAdvantage]      = +3,
                    [CardRole.MassDisruption]     = -3,  // board wipes are slow in a fast meta
                },
            },
        }.ToDictionary(p => p.Bracket);

    public static BracketProfile Get(Bracket bracket) => All[bracket];
}
