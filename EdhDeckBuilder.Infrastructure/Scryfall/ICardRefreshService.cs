namespace EdhDeckBuilder.Infrastructure.Scryfall;

public interface ICardRefreshService
{
    DateTimeOffset? GetLastRefreshed();
    Task RefreshAsync(CancellationToken ct = default);
}
