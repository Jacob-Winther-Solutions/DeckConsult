using EdhDeckBuilder.Infrastructure.Scryfall;
using Microsoft.Extensions.Options;

namespace EdhDeckBuilder.Web.Services;

internal sealed class ScryfallRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<ScryfallOptions> options,
    ILogger<ScryfallRefreshBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.RefreshInterval;

        var initialDelay = ComputeInitialDelay(interval);
        if (initialDelay > TimeSpan.Zero)
        {
            logger.LogInformation("Scryfall cache is fresh; next refresh in {Delay}", initialDelay);
            await Task.Delay(initialDelay, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunRefreshAsync(stoppingToken);
            await Task.Delay(interval, stoppingToken);
        }
    }

    private TimeSpan ComputeInitialDelay(TimeSpan interval)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICardRefreshService>();
        var lastRefreshed = service.GetLastRefreshed();
        return ScryfallRefreshScheduler.ComputeInitialDelay(lastRefreshed, interval, DateTimeOffset.UtcNow);
    }

    private async Task RunRefreshAsync(CancellationToken ct)
    {
        logger.LogInformation("Scryfall background refresh starting");
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ICardRefreshService>();
            await service.RefreshAsync(ct);
            logger.LogInformation("Scryfall background refresh complete");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scryfall background refresh failed");
        }
    }
}
