using EdhDeckBuilder.Agent.Fill;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Tests.Agent;

/// <summary>
/// Tests for ColorFixingPass (Pass C). BuildState is mutated in place; all tests verify
/// the committed set and BasicCount after Apply returns.
/// </summary>
public sealed class ColorFixingPassTests
{
    // ── Factories ─────────────────────────────────────────────────────────────

    private static Card MakeCard(
        string name,
        CardType types = CardType.None,
        Color colorIdentity = Color.None,
        bool isBasicLand = false) => new()
    {
        ScryfallId = Guid.NewGuid(),
        OracleId = Guid.NewGuid(),
        Name = name,
        TypeLine = types.HasFlag(CardType.Land) ? "Land" : "Instant",
        Types = types,
        ColorIdentity = colorIdentity,
        IsBasicLand = isBasicLand,
        CommanderLegality = Legality.Legal,
    };

    private static FillCandidate MakeSpell(
        string name,
        Color colorIdentity = Color.None,
        double inclusion = 0.5) => new()
    {
        Candidate = new CardCandidate(MakeCard(name, CardType.None, colorIdentity), inclusion, "Test"),
        Roles = RoleProfile.Of(CardRole.Ramp),
        LandCredit = 0,
    };

    private static FillCandidate MakeLand(
        string name,
        Color colorIdentity = Color.None,
        double inclusion = 0.5,
        bool isBasicLand = false) => new()
    {
        Candidate = new CardCandidate(
            MakeCard(name, CardType.Land, colorIdentity, isBasicLand), inclusion, "Test"),
        Roles = RoleProfile.Of(CardRole.Land),
        LandCredit = 0,
    };

    private static BuildContext MakeContext(
        Color colorIdentity,
        int reservedLandCount = 38) => new()
    {
        Commanders = [MakeCard("Test Commander")],
        ColorIdentity = colorIdentity,
        ResolvedTemplate = DeckTemplate.Balanced,
        NetTargets = new Dictionary<CardRole, RoleTarget>
        {
            [CardRole.Ramp] = new(3, 8, 12),
            [CardRole.Land] = new(reservedLandCount, reservedLandCount, reservedLandCount),
        },
        CommanderProfiles = [RoleProfile.Of(CardRole.Plan)],
        Constraints = new SoftConstraints { Bracket = Bracket.Three },
        ReservedLandCount = reservedLandCount,
    };

    // ── Empty pool ────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_does_nothing_when_pool_is_empty()
    {
        var state = new BuildState(38);
        var context = MakeContext(Color.Blue | Color.Green);

        ColorFixingPass.Apply(context, state, []);

        Assert.Empty(state.Committed);
        Assert.Equal(38, state.BasicCount);
    }

    // ── Filtering ─────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_skips_basic_lands_in_pool()
    {
        var state = new BuildState(38);
        var context = MakeContext(Color.Green);
        var pool = new[] { MakeLand("Forest", Color.Green, isBasicLand: true) };

        ColorFixingPass.Apply(context, state, pool);

        Assert.Empty(state.Committed);
        Assert.Equal(38, state.BasicCount);
    }

    [Fact]
    public void Apply_skips_colorless_lands()
    {
        var state = new BuildState(38);
        var context = MakeContext(Color.Blue | Color.Black);
        var pool = new[] { MakeLand("Ancient Tomb", Color.None) };

        ColorFixingPass.Apply(context, state, pool);

        Assert.Empty(state.Committed);
    }

    [Fact]
    public void Apply_skips_land_outside_commander_color_identity()
    {
        var state = new BuildState(38);
        var context = MakeContext(Color.White);      // mono-White
        var pool = new[] { MakeLand("Sacred Foundry", Color.White | Color.Red) };

        ColorFixingPass.Apply(context, state, pool);

        Assert.Empty(state.Committed);
    }

    [Fact]
    public void Apply_skips_already_committed_candidate()
    {
        var state = new BuildState(38);
        var context = MakeContext(Color.Green | Color.White);
        var land = MakeLand("Temple Garden", Color.Green | Color.White);

        state.Commit(land); // pre-committed by FillEngine
        int basicsBefore = state.BasicCount;

        ColorFixingPass.Apply(context, state, [land]);

        Assert.Equal(1, state.Committed.Count(s => s.Card.OracleId == land.Card.OracleId));
        Assert.Equal(basicsBefore, state.BasicCount);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_commits_eligible_land_and_reduces_basics_by_one()
    {
        var state = new BuildState(38);
        var context = MakeContext(Color.Blue | Color.Black);
        var pool = new[] { MakeLand("Underground Sea", Color.Blue | Color.Black) };

        ColorFixingPass.Apply(context, state, pool);

        Assert.Single(state.Committed);
        Assert.Equal("Underground Sea", state.Committed[0].Card.Name);
        Assert.Equal(37, state.BasicCount);
    }

    // ── Hard caps — minimum basics floor ─────────────────────────────────────

    [Fact]
    public void Apply_does_not_commit_when_basics_already_at_minimum()
    {
        var state = new BuildState(8);    // already at floor
        var context = MakeContext(Color.Blue | Color.Green);
        var pool = Enumerable.Range(1, 5)
            .Select(i => MakeLand($"Dual {i}", Color.Blue | Color.Green))
            .ToList();

        ColorFixingPass.Apply(context, state, pool);

        Assert.Empty(state.Committed);
        Assert.Equal(8, state.BasicCount);
    }

    [Fact]
    public void Apply_adds_exactly_up_to_the_minimum_basics_floor()
    {
        // Start: 11 basics, 0 utility. Cap = floor(11 × 0.5) = 5 (not binding here).
        // Floor stops additions at BasicCount = 8 → 3 can be added.
        var state = new BuildState(11);
        var context = MakeContext(Color.Green | Color.Black);
        var pool = Enumerable.Range(1, 10)
            .Select(i => MakeLand($"Dual {i}", Color.Green | Color.Black, inclusion: 1.0 / i))
            .ToList();

        ColorFixingPass.Apply(context, state, pool);

        Assert.Equal(3, state.Committed.Count);
        Assert.Equal(8, state.BasicCount);
    }

    // ── Hard caps — 50 % non-basic cap ───────────────────────────────────────

    [Fact]
    public void Apply_does_not_add_when_fifty_percent_cap_already_met()
    {
        // totalLandBase = 10, cap = 5. Pre-commit 5 utility lands to reach cap.
        var state = new BuildState(10);
        var context = MakeContext(Color.White | Color.Blue);

        for (int i = 0; i < 5; i++)
            state.Commit(MakeLand($"Pre-committed {i}", Color.White | Color.Blue));

        // state: 5 utility lands, 5 basics → cap already met
        var pool = new[] { MakeLand("Extra Dual", Color.White | Color.Blue) };

        ColorFixingPass.Apply(context, state, pool);

        Assert.Equal(5, state.UtilityLandCount);  // unchanged
    }

    [Fact]
    public void Apply_stops_at_fifty_percent_cap_during_addition()
    {
        // Start: 20 basics, 0 utility. Cap = 10. Floor would allow 12 → cap is binding.
        var state = new BuildState(20);
        var context = MakeContext(Color.Blue | Color.Black);
        var pool = Enumerable.Range(1, 20)
            .Select(i => MakeLand($"Dual {i}", Color.Blue | Color.Black, inclusion: 1.0 / i))
            .ToList();

        ColorFixingPass.Apply(context, state, pool);

        Assert.Equal(10, state.UtilityLandCount);
        Assert.Equal(10, state.BasicCount);
    }

    // ── Color demand scoring ──────────────────────────────────────────────────

    [Fact]
    public void Apply_prefers_land_covering_high_demand_color()
    {
        // 4 Blue spells, 1 Black spell → Blue demand (4) > Black demand (1).
        // With only 1 swap available (BasicCount = 9 → 8), Blue land should be picked.
        var state = new BuildState(9);
        var context = MakeContext(Color.Blue | Color.Black);

        for (int i = 0; i < 4; i++)
            state.Commit(MakeSpell($"Blue Spell {i}", Color.Blue));
        state.Commit(MakeSpell("Black Spell", Color.Black));

        var blueLand  = MakeLand("Underground River", Color.Blue,  inclusion: 0.5);
        var blackLand = MakeLand("Cabal Coffers",     Color.Black, inclusion: 0.5);

        ColorFixingPass.Apply(context, state, [blueLand, blackLand]);

        var landCommitted = state.Committed
            .Where(s => s.Card.Types.HasFlag(CardType.Land))
            .ToList();

        Assert.Single(landCommitted);
        Assert.Equal("Underground River", landCommitted[0].Card.Name);
    }

    [Fact]
    public void Apply_uses_inclusion_as_tiebreak_for_equal_color_scores()
    {
        // Two lands with identical color identity (same score) → higher inclusion wins.
        // Only 1 swap available (BasicCount = 9).
        var state = new BuildState(9);
        var context = MakeContext(Color.Blue | Color.Black);

        state.Commit(MakeSpell("Blue-Black Spell", Color.Blue | Color.Black));

        var highIncLand = MakeLand("Watery Grave", Color.Blue | Color.Black, inclusion: 0.9);
        var lowIncLand  = MakeLand("Dimir Guildgate", Color.Blue | Color.Black, inclusion: 0.3);

        ColorFixingPass.Apply(context, state, [lowIncLand, highIncLand]); // deliberately reversed order

        var landCommitted = state.Committed
            .Where(s => s.Card.Types.HasFlag(CardType.Land))
            .ToList();

        Assert.Single(landCommitted);
        Assert.Equal("Watery Grave", landCommitted[0].Card.Name);
    }

    // ── Physical invariant ────────────────────────────────────────────────────

    [Fact]
    public void Apply_physical_total_is_unchanged_after_pass()
    {
        var state = new BuildState(20);
        for (int i = 0; i < 5; i++)
            state.Commit(MakeSpell($"Spell {i}", Color.Blue | Color.Green));

        int totalBefore = state.PhysicalTotal;

        var context = MakeContext(Color.Blue | Color.Green);
        var pool = Enumerable.Range(1, 5)
            .Select(i => MakeLand($"Dual {i}", Color.Blue | Color.Green))
            .ToList();

        ColorFixingPass.Apply(context, state, pool);

        Assert.Equal(totalBefore, state.PhysicalTotal);
    }

    // ── Warnings ─────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_emits_floor_warning_when_minimum_basics_hit_with_remaining_candidates()
    {
        // 9 basics → 1 land added → floor at 8 → 2nd candidate would have been added but can't.
        var state = new BuildState(9);
        var context = MakeContext(Color.Blue | Color.Red);
        var pool = Enumerable.Range(1, 3)
            .Select(i => MakeLand($"Fixing {i}", Color.Blue | Color.Red))
            .ToList();

        var warnings = ColorFixingPass.Apply(context, state, pool);

        Assert.Contains(warnings, w => w.Contains("floor") || w.Contains("8"));
    }

    [Fact]
    public void Apply_emits_cap_warning_when_fifty_percent_cap_hit_with_remaining_candidates()
    {
        // 20 basics → cap = 10 (binding over floor of 12) → capped with 10 candidates left.
        var state = new BuildState(20);
        var context = MakeContext(Color.Blue | Color.Black);
        var pool = Enumerable.Range(1, 15)
            .Select(i => MakeLand($"Fixing {i}", Color.Blue | Color.Black))
            .ToList();

        var warnings = ColorFixingPass.Apply(context, state, pool);

        Assert.Contains(warnings, w => w.Contains("50") || w.Contains("cap"));
    }

    [Fact]
    public void Apply_returns_no_warnings_when_pool_exhausted_without_hitting_caps()
    {
        var state = new BuildState(38);
        var context = MakeContext(Color.Blue | Color.Green);
        // Only 2 candidates — both added, both caps remain unmet.
        var pool = Enumerable.Range(1, 2)
            .Select(i => MakeLand($"Dual {i}", Color.Blue | Color.Green))
            .ToList();

        var warnings = ColorFixingPass.Apply(context, state, pool);

        Assert.Empty(warnings);
    }
}
