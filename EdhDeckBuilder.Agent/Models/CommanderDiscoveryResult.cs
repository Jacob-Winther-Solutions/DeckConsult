using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Models;

public sealed record CommanderSuggestion
{
    public required Card Commander { get; init; }
    public required int Rank { get; init; }
    public required string Rationale { get; init; }

    /// <summary>
    /// If this suggestion is a partner pair, this holds the second commander.
    /// Null if this is a singleton commander.
    /// </summary>
    public Card? PartnerCommander { get; init; }
}

public sealed record CommanderDiscoveryResult
{
    public required IReadOnlyList<CommanderSuggestion> Suggestions { get; init; }
}
