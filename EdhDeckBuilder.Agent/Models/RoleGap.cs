using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Models;

public sealed record RoleGap
{
    public required CardRole Role { get; init; }
    public required double ActualCoverage { get; init; }
    public required int IdealTarget { get; init; }
    public required double Shortfall { get; init; }
}
