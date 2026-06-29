using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Agent.Llm;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Tests.Agent;

public class ClassificationCacheTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Card MakeCard(string name) => new()
    {
        ScryfallId = Guid.NewGuid(),
        OracleId   = Guid.NewGuid(),
        Name       = name,
        TypeLine   = "Instant",
    };

    private static CardCandidate MakeCandidate(Card card) =>
        new(card, Inclusion: 0.5, Section: "Top Cards");

    private static ClassificationResult MakeResult(Guid oracleId, CardRole role) =>
        new() { OracleId = oracleId, PrimaryRole = role };

    // ── Partition — no entries ────────────────────────────────────────────────

    [Fact]
    public void Partition_returns_all_as_misses_when_cache_is_empty()
    {
        var cache = new ClassificationCache();
        var card = MakeCard("Sol Ring");
        var candidates = new List<CardCandidate> { MakeCandidate(card) };

        cache.Partition(candidates, out var hits, out var misses);

        Assert.Empty(hits);
        Assert.Single(misses);
    }

    // ── Partition — cache hits ────────────────────────────────────────────────

    [Theory]
    [InlineData(CardRole.Ramp)]
    [InlineData(CardRole.CardAdvantage)]
    [InlineData(CardRole.TargetedDisruption)]
    [InlineData(CardRole.MassDisruption)]
    [InlineData(CardRole.Tutor)]
    [InlineData(CardRole.Protection)]
    [InlineData(CardRole.Recursion)]
    public void Partition_returns_cached_result_for_global_stable_role(CardRole role)
    {
        var cache = new ClassificationCache();
        var card = MakeCard("Test Card");
        var candidate = MakeCandidate(card);

        cache.Store([MakeResult(card.OracleId, role)]);
        cache.Partition([candidate], out var hits, out var misses);

        Assert.Single(hits);
        Assert.Empty(misses);
        Assert.Equal(role, hits[0].PrimaryRole);
    }

    // ── Partition — Plan, Synergy, and Payoff are never served from cache ────

    [Fact]
    public void Partition_never_returns_Payoff_from_cache()
    {
        var cache = new ClassificationCache();
        var card = MakeCard("Exsanguinate");
        var candidate = MakeCandidate(card);

        cache.Store([MakeResult(card.OracleId, CardRole.Payoff)]);
        cache.Partition([candidate], out var hits, out var misses);

        Assert.Empty(hits);
        Assert.Single(misses);
    }

    [Fact]
    public void Partition_never_returns_Plan_from_cache()
    {
        var cache = new ClassificationCache();
        var card = MakeCard("Approach of the Second Sun");
        var candidate = MakeCandidate(card);

        // Manually prime the cache by storing a result with Plan primary — it should be ignored.
        // (Store itself also refuses Plan, but we test Partition's check independently.)
        cache.Store([MakeResult(card.OracleId, CardRole.Plan)]);
        cache.Partition([candidate], out var hits, out var misses);

        Assert.Empty(hits);
        Assert.Single(misses);
    }

    [Fact]
    public void Partition_never_returns_Synergy_from_cache()
    {
        var cache = new ClassificationCache();
        var card = MakeCard("Vanquisher's Banner");
        var candidate = MakeCandidate(card);

        cache.Store([MakeResult(card.OracleId, CardRole.Synergy)]);
        cache.Partition([candidate], out var hits, out var misses);

        Assert.Empty(hits);
        Assert.Single(misses);
    }

    // ── Store — does not cache Plan or Synergy ────────────────────────────────

    [Fact]
    public void Store_does_not_cache_Plan_result()
    {
        var cache = new ClassificationCache();
        var card = MakeCard("Teferi's Ageless Insight");
        var candidate = MakeCandidate(card);

        cache.Store([MakeResult(card.OracleId, CardRole.Plan)]);
        // After Store rejects it, Partition should still see it as a miss.
        cache.Partition([candidate], out var hits, out var misses);

        Assert.Empty(hits);
        Assert.Single(misses);
    }

    [Fact]
    public void Store_does_not_cache_Synergy_result()
    {
        var cache = new ClassificationCache();
        var card = MakeCard("Kindred Discovery");
        var candidate = MakeCandidate(card);

        cache.Store([MakeResult(card.OracleId, CardRole.Synergy)]);
        cache.Partition([candidate], out var hits, out var misses);

        Assert.Empty(hits);
        Assert.Single(misses);
    }

    // ── Mixed batch ───────────────────────────────────────────────────────────

    [Fact]
    public void Partition_correctly_splits_hits_and_misses_in_mixed_batch()
    {
        var cache = new ClassificationCache();

        var rampCard = MakeCard("Cultivate");
        var planCard = MakeCard("Ghired, Conclave Exile");
        var uncachedCard = MakeCard("Austere Command");

        // Cache a Ramp result (should be a hit).
        cache.Store([MakeResult(rampCard.OracleId, CardRole.Ramp)]);
        // Plan is not cached by Store, so it stays a miss.
        cache.Store([MakeResult(planCard.OracleId, CardRole.Plan)]);

        var candidates = new List<CardCandidate>
        {
            MakeCandidate(rampCard),
            MakeCandidate(planCard),
            MakeCandidate(uncachedCard),
        };

        cache.Partition(candidates, out var hits, out var misses);

        Assert.Single(hits);                        // only rampCard
        Assert.Equal(2, misses.Count);              // planCard + uncachedCard
        Assert.Equal(rampCard.OracleId, hits[0].OracleId);
        Assert.Contains(misses, m => m.Card.OracleId == planCard.OracleId);
        Assert.Contains(misses, m => m.Card.OracleId == uncachedCard.OracleId);
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public void Store_is_idempotent_for_same_oracle_id()
    {
        var cache = new ClassificationCache();
        var card = MakeCard("Rhystic Study");
        var candidate = MakeCandidate(card);

        var first  = MakeResult(card.OracleId, CardRole.CardAdvantage);
        var second = MakeResult(card.OracleId, CardRole.Ramp); // different role, same id

        cache.Store([first]);
        cache.Store([second]); // TryAdd should not overwrite

        cache.Partition([candidate], out var hits, out var misses);

        Assert.Single(hits);
        Assert.Equal(CardRole.CardAdvantage, hits[0].PrimaryRole); // first write wins
    }
}
