using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Models;

public sealed record CommanderSuggestion
{
    public required Card Commander { get; init; }
    public required int Rank { get; init; }
    public required string Rationale { get; init; }
}

public sealed record CommanderDiscoveryResult
{
    public required IReadOnlyList<CommanderSuggestion> Suggestions { get; init; }
}
