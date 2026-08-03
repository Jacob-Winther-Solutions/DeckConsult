using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Models;

public sealed record AnalyzedCard
{
    public required Card Card { get; init; }
    public required RoleProfile Roles { get; init; }
    public string? ClassifierReasoning { get; init; }
    /// <summary>True when this card is the commander, not one of the 99.</summary>
    public bool IsCommander { get; init; } = false;
}
