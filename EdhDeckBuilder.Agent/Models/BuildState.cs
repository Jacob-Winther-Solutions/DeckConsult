using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;

namespace EdhDeckBuilder.Agent.Models;

/// <summary>
/// Mutable accumulator for the fill engine. Tracks committed cards, physical slot counts,
/// role coverage, and the remaining basic land reserve in one place so every update stays
/// consistent. Call <see cref="Commit"/> for each card the fill engine selects.
/// </summary>
public sealed class BuildState
{
    private double _basicCountRaw;
    private int _spellCount;
    private readonly Dictionary<Guid, FillCandidate> _committedCandidates = new();
    private readonly HashSet<Guid> _lockedIds = new();

    public BuildState(int initialBasicCount)
    {
        _basicCountRaw = initialBasicCount;
    }

    /// <summary>All non-commander cards committed so far (spell slots and utility land slots).</summary>
    public List<DeckSlot> Committed { get; } = [];

    /// <summary>Physical slot count by primary role — each card counted exactly once.</summary>
    public Dictionary<CardRole, int> PrimaryCounts { get; } = new();

    /// <summary>
    /// Running coverage per role, including secondary contributions. May legitimately exceed the
    /// physical slot count because Always-relation overlaps are credited to both roles simultaneously.
    /// </summary>
    public Dictionary<CardRole, double> Coverage { get; } = new();

    /// <summary>
    /// Cards committed as spell slots (non-land cards, including MDFCs). Does not count utility
    /// lands — those consume land slots. The fill engine checks this against the spell budget
    /// (99 − <c>ReservedLandCount</c>) to know when the non-land deck is full.
    /// </summary>
    public int SpellCount => _spellCount;

    /// <summary>Cards committed as land slots (utility lands that replaced a basic).</summary>
    public int UtilityLandCount => Committed.Count - _spellCount;

    /// <summary>
    /// Remaining basic land slots after subtracting utility land claims and MDFC land credits.
    /// Rounded to the nearest integer; floored at zero.
    /// </summary>
    public int BasicCount => (int)Math.Max(0, Math.Round(_basicCountRaw));

    /// <summary>
    /// Physical total of all committed cards plus remaining basics. Should equal 99 when full.
    /// </summary>
    public int PhysicalTotal => Committed.Count + BasicCount;

    /// <summary>All committed candidates, keyed by oracle id. Used by the reconciliation loop.</summary>
    public IReadOnlyDictionary<Guid, FillCandidate> CommittedCandidates => _committedCandidates;

    /// <summary>Oracle IDs of user-locked cards. These must never be removed during repair or reconciliation.</summary>
    public IReadOnlySet<Guid> LockedIds => _lockedIds;

    /// <summary>
    /// Commits a classified candidate to the build. Updates primary counts, coverage, and the
    /// basic land reserve according to the candidate's card type and land credit.
    /// </summary>
    public void Commit(
        FillCandidate candidate,
        ClassificationSource source = ClassificationSource.Llm,
        double confidence = 1.0,
        bool isLocked = false)
    {
        var slot = new DeckSlot
        {
            Card = candidate.Card,
            Quantity = 1,
            Roles = candidate.Roles,
            RoleSource = source,
            RoleConfidence = confidence,
            IsLocked = isLocked,
        };
        if (isLocked) _lockedIds.Add(candidate.Card.OracleId);

        Committed.Add(slot);
        _committedCandidates[candidate.Card.OracleId] = candidate;
        PrimaryCounts[slot.PrimaryRole] = PrimaryCounts.GetValueOrDefault(slot.PrimaryRole) + 1;

        foreach (var role in slot.Roles.AllRoles())
            Coverage[role] = Coverage.GetValueOrDefault(role) + slot.Roles.CoverageFor(role);

        // Slot accounting (see FillCandidate.LandCredit for the full model):
        // - Utility land: physically a land, claims one land slot (reduces basic reserve).
        // - Spell/MDFC:   claims one spell slot; MDFC also offsets part of the basic reserve.
        if (candidate.Card.Types.HasFlag(CardType.Land))
            _basicCountRaw--;
        else
        {
            _spellCount++;
            if (candidate.LandCredit > 0)
                _basicCountRaw -= candidate.LandCredit;
        }
    }

    /// <summary>
    /// Reverses a prior <see cref="Commit"/>. Used by the reconciliation swap loop.
    /// No-op if the oracle id is not in the committed set, or if the card is locked.
    /// </summary>
    public void Remove(Guid oracleId)
    {
        if (_lockedIds.Contains(oracleId)) return;
        if (!_committedCandidates.TryGetValue(oracleId, out var candidate)) return;

        var slot = Committed.FirstOrDefault(s => s.Card.OracleId == oracleId);
        if (slot is null) return;

        Committed.Remove(slot);
        _committedCandidates.Remove(oracleId);
        PrimaryCounts[slot.PrimaryRole]--;

        foreach (var role in slot.Roles.AllRoles())
            Coverage[role] = Coverage.GetValueOrDefault(role) - slot.Roles.CoverageFor(role);

        if (candidate.Card.Types.HasFlag(CardType.Land))
            _basicCountRaw++;
        else
        {
            _spellCount--;
            if (candidate.LandCredit > 0)
                _basicCountRaw += candidate.LandCredit;
        }
    }
}
