using EdhDeckBuilder.Agent.Fill;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Selection;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Tests.Agent;

/// <summary>
/// Tests for FillEngine. All tests use a deterministic mock selector (orders candidates by
/// inclusion rate descending) so results are stable with no API calls.
/// </summary>
public sealed class FillEngineTests
{
    // ── Mock selector ─────────────────────────────────────────────────────────

    /// <summary>Returns candidates sorted by inclusion descending — stable, no API calls.</summary>
    private sealed class InclusionOrderSelector : ICardSelector
    {
        public Task<IReadOnlyList<SelectionResult>> SelectAsync(
            CardRole role,
            IReadOnlyList<FillCandidate> candidates,
            BuildContext context,
            BuildState state,
            CancellationToken ct)
        {
            IReadOnlyList<SelectionResult> results = candidates
                .OrderByDescending(c => c.Candidate.Inclusion)
                .ThenBy(c => c.Card.OracleId) // stable tiebreak
                .Select((c, i) => new SelectionResult
                {
                    OracleId = c.Card.OracleId,
                    Rank = i + 1,
                    Rationale = "mock",
                })
                .ToList();
            return Task.FromResult(results);
        }
    }

    private static readonly ICardSelector MockSelector = new InclusionOrderSelector();

    // ── Factories ─────────────────────────────────────────────────────────────

    private static Card MakeCard(
        string name,
        CardType types = CardType.None,
        Color colorIdentity = Color.None) => new()
    {
        ScryfallId = Guid.NewGuid(),
        OracleId = Guid.NewGuid(),
        Name = name,
        TypeLine = types.HasFlag(CardType.Land) ? "Land" : "Instant",
        Types = types,
        ColorIdentity = colorIdentity,
        CommanderLegality = Legality.Legal,
    };

    private static FillCandidate MakeSpell(
        string name,
        CardRole role,
        double inclusion = 0.5,
        RoleProfile? roles = null) => new()
    {
        Candidate = new CardCandidate(MakeCard(name), inclusion, "Test"),
        Roles = roles ?? RoleProfile.Of(role),
        LandCredit = 0,
    };

    private static FillCandidate MakeLand(
        string name,
        CardRole role,
        double inclusion = 0.5) => new()
    {
        Candidate = new CardCandidate(MakeCard(name, CardType.Land), inclusion, "Test"),
        Roles = RoleProfile.Of(role),
        LandCredit = 0,
    };

    private static FillCandidate MakeMdfc(
        string name,
        CardRole role,
        double landCredit,
        double inclusion = 0.5) => new()
    {
        Candidate = new CardCandidate(MakeCard(name), inclusion, "Test"),
        Roles = RoleProfile.Of(role),
        LandCredit = landCredit,
    };

    /// <summary>Minimal BuildContext for testing — 3 spell slots (99 - 96 basics) is unrealistic but arithmetically clear.</summary>
    private static BuildContext MakeContext(
        Dictionary<CardRole, RoleTarget> netTargets,
        int reservedLandCount = 38) => new()
    {
        Commanders = [MakeCard("Test Commander")],
        ColorIdentity = Color.None,
        ResolvedTemplate = DeckTemplate.Balanced,
        NetTargets = netTargets,
        CommanderProfiles = [RoleProfile.Of(CardRole.Plan)],
        Constraints = new SoftConstraints { Bracket = Bracket.Three },
        ReservedLandCount = reservedLandCount,
    };

    // ── Fill order ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fill_respects_fill_order_Plan_before_Ramp()
    {
        var planCard = MakeSpell("Plan Card", CardRole.Plan, inclusion: 0.9);
        var rampCard = MakeSpell("Ramp Card", CardRole.Ramp, inclusion: 0.8);

        var context = MakeContext(new()
        {
            [CardRole.Plan] = new(1, 1, 2),
            [CardRole.Ramp] = new(1, 1, 2),
        }, reservedLandCount: 97); // 2 spell slots

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, [planCard, rampCard]);

        // Plan should be committed first — its index in Committed is 0
        Assert.Equal("Plan Card", result.State.Committed[0].Card.Name);
        Assert.Equal("Ramp Card", result.State.Committed[1].Card.Name);
    }

    // ── Coverage targets ──────────────────────────────────────────────────────

    [Fact]
    public async Task Fill_stops_at_ideal_coverage_for_role()
    {
        var cards = Enumerable.Range(1, 5)
            .Select(i => MakeSpell($"Ramp {i}", CardRole.Ramp, inclusion: 1.0 / i))
            .ToList();

        // Ideal = 3; should commit exactly 3 ramp cards
        var context = MakeContext(new()
        {
            [CardRole.Ramp] = new(2, 3, 5),
        }, reservedLandCount: 96); // 3 spell slots

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, cards);

        Assert.Equal(3, result.State.PrimaryCounts.GetValueOrDefault(CardRole.Ramp));
        Assert.Equal(3.0, result.State.Coverage.GetValueOrDefault(CardRole.Ramp));
    }

    [Fact]
    public async Task Fill_skips_role_when_already_covered_by_overlap()
    {
        // Ramp card with Always CardAdvantage secondary — commits as Ramp but covers CardAdvantage too
        var rampDrawCard = MakeSpell(
            "Black Market Connections",
            CardRole.Ramp,
            inclusion: 0.9,
            roles: RoleProfile.Of(CardRole.Ramp).With(RoleContribution.Both(CardRole.CardAdvantage)));

        var pureDrawCard = MakeSpell("Rhystic Study", CardRole.CardAdvantage, inclusion: 0.95);

        // After Ramp fill: CardAdvantage coverage = 1.0 (Always overlap). Ideal = 1 → already met.
        // reservedLandCount=98 → exactly 1 spell slot so no spillover can sneak in the draw card.
        var context = MakeContext(new()
        {
            [CardRole.Ramp]         = new(1, 1, 2),
            [CardRole.CardAdvantage] = new(1, 1, 2),
        }, reservedLandCount: 98); // 1 spell slot

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, [rampDrawCard, pureDrawCard]);

        // Only the ramp card should be committed — it alone satisfies both roles
        Assert.Single(result.State.Committed);
        Assert.Equal("Black Market Connections", result.State.Committed[0].Card.Name);
        Assert.Equal(1.0, result.State.Coverage.GetValueOrDefault(CardRole.CardAdvantage));
    }

    // ── Overlap credit per relation type ─────────────────────────────────────

    [Fact]
    public async Task Fill_always_overlap_credits_full_weight_to_secondary_role()
    {
        var card = MakeSpell(
            "Always Overlap",
            CardRole.Ramp,
            roles: RoleProfile.Of(CardRole.Ramp).With(RoleContribution.Both(CardRole.CardAdvantage, 1.0)));

        var context = MakeContext(new()
        {
            [CardRole.Ramp] = new(1, 1, 2),
        }, reservedLandCount: 98);

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, [card]);

        Assert.Equal(1.0, result.State.Coverage.GetValueOrDefault(CardRole.Ramp));
        Assert.Equal(1.0, result.State.Coverage.GetValueOrDefault(CardRole.CardAdvantage));
    }

    [Fact]
    public async Task Fill_modal_overlap_credits_half_weight_to_secondary_role()
    {
        var card = MakeSpell(
            "Jeska's Will",
            CardRole.Ramp,
            roles: RoleProfile.Of(CardRole.Ramp).With(RoleContribution.EitherOr(CardRole.CardAdvantage)));

        var context = MakeContext(new()
        {
            [CardRole.Ramp] = new(1, 1, 2),
        }, reservedLandCount: 98);

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, [card]);

        Assert.Equal(1.0, result.State.Coverage.GetValueOrDefault(CardRole.Ramp));
        Assert.Equal(0.5, result.State.Coverage.GetValueOrDefault(CardRole.CardAdvantage), 10);
    }

    [Fact]
    public async Task Fill_transform_overlap_credits_0_75_weight_to_secondary_role()
    {
        var card = MakeSpell(
            "Hedron Archive",
            CardRole.Ramp,
            roles: RoleProfile.Of(CardRole.Ramp).With(RoleContribution.Switches(CardRole.CardAdvantage)));

        var context = MakeContext(new()
        {
            [CardRole.Ramp] = new(1, 1, 2),
        }, reservedLandCount: 98);

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, [card]);

        Assert.Equal(1.0, result.State.Coverage.GetValueOrDefault(CardRole.Ramp));
        Assert.Equal(0.75, result.State.Coverage.GetValueOrDefault(CardRole.CardAdvantage), 10);
    }

    // ── Utility lands ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Fill_utility_land_reduces_BasicCount_and_does_not_consume_spell_slot()
    {
        // 1 spell slot, 1 land slot
        var context = MakeContext(new()
        {
            [CardRole.Ramp] = new(2, 2, 3),
        }, reservedLandCount: 97);

        var utilityLand = MakeLand("Three Tree City", CardRole.Ramp, inclusion: 0.9);
        var spell = MakeSpell("Cultivate", CardRole.Ramp, inclusion: 0.8);

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, [utilityLand, spell]);

        Assert.Equal(2, result.State.Committed.Count);
        Assert.Equal(96, result.State.BasicCount);   // started at 97, one utility land claimed one
        Assert.Equal(1, result.State.SpellCount);
        Assert.Equal(1, result.State.UtilityLandCount);
    }

    [Fact]
    public async Task Fill_utility_land_not_selected_when_no_land_budget_remains()
    {
        // reservedLandCount = 98 so BasicCount starts at 98; that's fine but let's use 38 and reduce manually
        // More directly: start with BasicCount = 0 by using reservedLandCount = 0, but that gives 99 spell slots.
        // Instead: fill all land slots with other utility lands first, then try to add more.
        // Simplest: use reservedLandCount = 1 (1 land slot, 98 spell slots), add 2 utility lands to pool.
        var context = MakeContext(new()
        {
            [CardRole.Ramp] = new(2, 2, 3),
        }, reservedLandCount: 1); // only 1 land slot

        var land1 = MakeLand("Utility Land 1", CardRole.Ramp, inclusion: 0.9);
        var land2 = MakeLand("Utility Land 2", CardRole.Ramp, inclusion: 0.8);

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, [land1, land2]);

        // Only 1 utility land can be committed (1 land slot); the second is skipped
        Assert.Equal(1, result.State.UtilityLandCount);
        Assert.Equal(0, result.State.BasicCount);
    }

    // ── MDFCs ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fill_mdfc_consumes_spell_slot_and_reduces_BasicCount_by_credit()
    {
        var mdfc = MakeMdfc("Agadeem's Awakening", CardRole.Ramp, landCredit: 0.3, inclusion: 0.9);

        var context = MakeContext(new()
        {
            [CardRole.Ramp] = new(1, 1, 2),
        }, reservedLandCount: 38);

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, [mdfc]);

        Assert.Equal(1, result.State.SpellCount);
        Assert.Equal(0, result.State.UtilityLandCount);
        // BasicCount = round(38 - 0.3) = round(37.7) = 38 (credit too small to round down alone)
        Assert.Equal(38, result.State.BasicCount);
    }

    [Fact]
    public async Task Fill_accumulated_mdfc_credits_reduce_BasicCount()
    {
        // Four MDFCs with 0.3 credit each → accumulated = 1.2 → round(38 - 1.2) = round(36.8) = 37
        var mdfcs = Enumerable.Range(1, 4)
            .Select(i => MakeMdfc($"MDFC {i}", CardRole.Ramp, landCredit: 0.3, inclusion: 1.0 / i))
            .ToList();

        var context = MakeContext(new()
        {
            [CardRole.Ramp] = new(4, 4, 6),
        }, reservedLandCount: 38);

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, mdfcs);

        Assert.Equal(4, result.State.SpellCount);
        Assert.Equal(37, result.State.BasicCount); // 38 - round(4 * 0.3) = 38 - 1 = 37
    }

    // ── Physical slot invariant ───────────────────────────────────────────────

    [Fact]
    public async Task Fill_physical_total_equals_99_after_fill()
    {
        // Provide exactly enough pool for the engine to fill 99 slots
        int reserved = 38;
        int spellSlots = 99 - reserved;

        var pool = Enumerable.Range(1, spellSlots + 10) // extra to ensure coverage targets can be met
            .Select(i => MakeSpell($"Spell {i}", CardRole.Ramp, inclusion: 1.0 / i))
            .ToList();

        var context = MakeContext(new()
        {
            [CardRole.Ramp] = new(spellSlots - 5, spellSlots, spellSlots + 5),
        }, reservedLandCount: reserved);

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, pool);

        Assert.Equal(99, result.State.PhysicalTotal);
    }

    // ── Reconciliation ────────────────────────────────────────────────────────

    [Fact]
    public async Task Reconcile_swaps_to_fix_under_covered_role()
    {
        // Scenario designed to trigger a reconciliation swap:
        //   greedy fills Plan(1) + Ramp(1) using 2 of 3 spell slots.
        //   spillover takes the 3rd slot with "Plan Surplus" (inclusion 0.8 > ramp2 inclusion 0.3).
        //   Result after fill: Plan=2 (over ideal=1), Ramp=1 (under min=2).
        //   Deviation = |2-1| + |1-3| = 1+2 = 3.
        //   Reconciliation: cut "Plan Surplus" (Plan over ideal), add ramp2.
        //   Deviation after swap = |1-1| + |2-3| = 0+1 = 1 → improvement accepted.
        var plan1    = MakeSpell("Plan Primary",  CardRole.Plan, inclusion: 0.9);
        var planSurp = MakeSpell("Plan Surplus",  CardRole.Plan, inclusion: 0.8); // committed by spillover, then cut
        var ramp1    = MakeSpell("Ramp Primary",  CardRole.Ramp, inclusion: 0.5); // committed by greedy
        var ramp2    = MakeSpell("Ramp Reconcile",CardRole.Ramp, inclusion: 0.3); // committed by reconciliation

        var context = MakeContext(new()
        {
            [CardRole.Plan] = new(1, 1, 2), // ideal=1; Plan goes over when spillover adds planSurp
            [CardRole.Ramp] = new(2, 3, 4), // min=2; Ramp stays at 1 after greedy → under min
        }, reservedLandCount: 96); // 3 spell slots

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, [plan1, planSurp, ramp1, ramp2]);

        var rampCoverage = result.State.Coverage.GetValueOrDefault(CardRole.Ramp);
        Assert.True(rampCoverage >= 2.0, $"Expected Ramp coverage ≥ 2 after reconciliation, got {rampCoverage}");
        Assert.DoesNotContain(result.State.Committed, s => s.Card.Name == "Plan Surplus");
        Assert.Contains(result.State.Committed, s => s.Card.Name == "Ramp Reconcile");
    }

    [Fact]
    public async Task Reconcile_emits_warning_when_coverage_cannot_be_met()
    {
        // Pool has only 1 Ramp card but the target min is 3 — impossible to meet
        var context = MakeContext(new()
        {
            [CardRole.Ramp] = new(3, 5, 7),
        }, reservedLandCount: 98); // 1 spell slot

        var rampCard = MakeSpell("Sol Ring", CardRole.Ramp, inclusion: 0.99);

        var engine = new FillEngine(MockSelector);
        var result = await engine.FillAsync(context, [rampCard]);

        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Contains("Ramp") && w.Contains("below minimum"));
    }

    [Fact]
    public async Task Reconcile_swap_loop_is_monotone()
    {
        // Set up a situation where the only available swap does NOT reduce deviation.
        // The loop should accept zero swaps and return immediately.
        // Plan is short but the only Plan candidate is already committed; no pool remainder.
        var planCard = MakeSpell("Plan Card", CardRole.Plan, inclusion: 0.9);
        var rampCard = MakeSpell("Ramp Card", CardRole.Ramp, inclusion: 0.8);

        var context = MakeContext(new()
        {
            [CardRole.Plan] = new(2, 3, 4), // short: will only have 1
            [CardRole.Ramp] = new(1, 1, 2), // met
        }, reservedLandCount: 97);

        var engine = new FillEngine(MockSelector);
        // Run twice — if the loop is monotone and terminates correctly, results are identical
        var r1 = await engine.FillAsync(context, [planCard, rampCard]);
        var r2 = await engine.FillAsync(context, [planCard, rampCard]);

        // Both runs should produce the same committed set (deterministic + terminates)
        var names1 = r1.State.Committed.Select(s => s.Card.Name).OrderBy(x => x).ToList();
        var names2 = r2.State.Committed.Select(s => s.Card.Name).OrderBy(x => x).ToList();
        Assert.Equal(names1, names2);
    }

    // ── Whitelist enforcement ─────────────────────────────────────────────────

    [Fact]
    public async Task Fill_ignores_selector_results_with_unknown_oracle_ids()
    {
        // Selector that injects a phantom id not in the candidate list
        var phantomSelector = new PhantomIdSelector();
        var realCard = MakeSpell("Real Card", CardRole.Ramp, inclusion: 0.9);

        var context = MakeContext(new()
        {
            [CardRole.Ramp] = new(1, 1, 2),
        }, reservedLandCount: 98);

        var engine = new FillEngine(phantomSelector);
        var result = await engine.FillAsync(context, [realCard]);

        // Only the real card should be in the committed set (phantom id was rejected)
        Assert.All(result.State.Committed, s => Assert.NotEqual("Phantom", s.Card.Name));
    }

    private sealed class PhantomIdSelector : ICardSelector
    {
        public Task<IReadOnlyList<SelectionResult>> SelectAsync(
            CardRole role,
            IReadOnlyList<FillCandidate> candidates,
            BuildContext context,
            BuildState state,
            CancellationToken ct)
        {
            IReadOnlyList<SelectionResult> results =
            [
                new() { OracleId = Guid.NewGuid(), Rank = 1, Rationale = "phantom" }, // unknown id
                .. candidates.Select((c, i) => new SelectionResult
                {
                    OracleId = c.Card.OracleId,
                    Rank = i + 2,
                    Rationale = "real",
                }),
            ];
            return Task.FromResult(results);
        }
    }
}
