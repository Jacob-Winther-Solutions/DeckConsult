using System.Collections.Concurrent;

namespace EdhDeckBuilder.Web.Services;

/// <summary>
/// Singleton in-memory cache for deck build results. Used to pass results from the builder
/// to the results page without round-tripping through SignalR for same-session navigations.
/// Page reloads fall back to localStorage (requires the SignalR receive limit to be raised).
/// </summary>
public sealed class DeckResultStore
{
    private readonly ConcurrentDictionary<string, StoredDeckResult> _results = new();

    public void Put(string id, StoredDeckResult result) => _results[id] = result;

    public StoredDeckResult? Get(string id) =>
        _results.TryGetValue(id, out var r) ? r : null;
}
