using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Core.Cards;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EdhDeckBuilder.Agent.Llm;

/// <summary>
/// Classification cache keyed by OracleId, persisted to disk across sessions.
/// Plan and Synergy roles are commander-dependent and are never cached.
/// All other roles are global-stable and reused across builds and restarts.
/// </summary>
public sealed class ClassificationCache
{
    private static readonly string DefaultCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EdhDeckBuilder", "classification_cache.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    private static readonly IReadOnlySet<CardRole> NeverCache =
        new HashSet<CardRole> { CardRole.Plan, CardRole.Synergy, CardRole.Payoff };

    private readonly string _cachePath;
    private readonly ConcurrentDictionary<Guid, ClassificationResult> _cache;
    private readonly object _writeLock = new();

    public ClassificationCache() : this(DefaultCachePath) { }

    internal ClassificationCache(string cachePath)
    {
        _cachePath = cachePath;
        _cache = LoadFromDisk(cachePath);
    }

    public void Partition(
        IReadOnlyList<CardCandidate> candidates,
        out IReadOnlyList<ClassificationResult> hits,
        out IReadOnlyList<CardCandidate> misses)
    {
        var hitList = new List<ClassificationResult>(candidates.Count);
        var missList = new List<CardCandidate>(candidates.Count);

        foreach (var candidate in candidates)
        {
            if (_cache.TryGetValue(candidate.Card.OracleId, out var cached)
                && !NeverCache.Contains(cached.PrimaryRole))
            {
                hitList.Add(cached);
            }
            else
            {
                missList.Add(candidate);
            }
        }

        hits = hitList;
        misses = missList;
    }

    public void Store(IReadOnlyList<ClassificationResult> results)
    {
        var added = false;
        foreach (var r in results)
        {
            if (!NeverCache.Contains(r.PrimaryRole) && _cache.TryAdd(r.OracleId, r))
                added = true;
        }

        if (added)
            _ = Task.Run(PersistToDisk);
    }

    private void PersistToDisk()
    {
        lock (_writeLock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
                var snapshot = _cache.Values.ToList();
                var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
                var tmp = _cachePath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, _cachePath, overwrite: true);
            }
            catch { /* best-effort — a failed write just means next session re-classifies */ }
        }
    }

    private static ConcurrentDictionary<Guid, ClassificationResult> LoadFromDisk(string path)
    {
        if (!File.Exists(path))
            return new();
        try
        {
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<ClassificationResult>>(json, SerializerOptions) ?? [];
            return new ConcurrentDictionary<Guid, ClassificationResult>(
                list.ToDictionary(r => r.OracleId));
        }
        catch { return new(); }
    }
}
