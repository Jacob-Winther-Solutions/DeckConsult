using EdhDeckBuilder.Agent.Instrumentation;
using EdhDeckBuilder.Agent.Models;

namespace EdhDeckBuilder.Agent.Interfaces;

public interface ICommanderDiscovery
{
    Task<CommanderDiscoveryResult> DiscoverAsync(
        CommanderDiscoveryRequest request,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    void SetUsageTracker(UsageTracker tracker);
}
