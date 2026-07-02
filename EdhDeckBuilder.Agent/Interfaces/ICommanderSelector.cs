using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Interfaces;

public interface ICommanderSelector
{
    Task<IReadOnlyList<CommanderSelectionResult>> SelectAsync(
        IReadOnlyList<Card> candidates,
        CommanderDiscoveryRequest request,
        CancellationToken ct = default);
}

public sealed record CommanderSelectionResult(Guid OracleId, int Rank, string Rationale);
