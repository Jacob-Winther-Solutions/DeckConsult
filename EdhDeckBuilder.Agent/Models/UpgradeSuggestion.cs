using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Models;

public sealed record UpgradeSuggestion
{
    public required Card AddCard { get; init; }
    public required string AddRationale { get; init; }
    public required CardRole TargetRole { get; init; }
    public Card? CutCard { get; init; }
    public string? CutRationale { get; init; }
}

public sealed record RoleUpgrade
{
    public required RoleGap Gap { get; init; }
    public required IReadOnlyList<UpgradeSuggestion> Suggestions { get; init; }
}

public sealed record DeckUpgradeResult
{
    public required IReadOnlyList<RoleUpgrade> RoleUpgrades { get; init; }
}
