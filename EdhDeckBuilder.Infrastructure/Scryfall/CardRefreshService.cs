using Microsoft.Extensions.Options;

namespace EdhDeckBuilder.Infrastructure.Scryfall;

internal sealed class CardRefreshService(
    ScryfallBulkClient bulkClient,
    IOptions<ScryfallOptions> options) : ICardRefreshService
{
    public DateTimeOffset? GetLastRefreshed()
    {
        var path = Path.Combine(options.Value.CacheDirectory, ScryfallBulkClient.CacheFileName);
        return File.Exists(path) ? (DateTimeOffset)File.GetLastWriteTimeUtc(path) : null;
    }

    public Task RefreshAsync(CancellationToken ct = default)
        => bulkClient.ForceRefreshAsync(ct);
}
