using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Core.Cards;
using System.Collections.Concurrent;

namespace EdhDeckBuilder.Agent.Llm;

/// <summary>
/// In-memory cache for classification results, keyed by OracleId.
///
/// Plan and Synergy roles are commander-dependent: the same card may classify as Plan in a
/// Spellslinger deck but as Synergy in a Tokens deck. These are never served from cache.
/// All other roles are global-stable and safe to reuse across builds.
/// </summary>
public sealed class ClassificationCache
{
    private readonly ConcurrentDictionary<Guid, ClassificationResult> _cache = new();

    private static readonly IReadOnlySet<CardRole> NeverCache =
        new HashSet<CardRole> { CardRole.Plan, CardRole.Synergy };

    /// <summary>
    /// Splits <paramref name="candidates"/> into cache hits and misses.
    /// Candidates whose cached result has a Plan or Synergy primary role always go to misses,
    /// even if a cache entry exists.
    /// </summary>
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

    /// <summary>
    /// Stores results that are safe to cache (not Plan or Synergy primary roles).
    /// Results for Plan and Synergy are silently ignored.
    /// </summary>
    public void Store(IReadOnlyList<ClassificationResult> results)
    {
        foreach (var r in results)
        {
            if (!NeverCache.Contains(r.PrimaryRole))
                _cache.TryAdd(r.OracleId, r);
        }
    }
}
