using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Agent.Selection;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Interfaces;

/// <summary>
/// Ranks a pool of classified candidates for a specific role, given the current build context
/// and state. The fill engine calls this once per role to get an ordered list, then takes the
/// top N according to the resolved coverage target — the selector never dictates count.
/// </summary>
/// <remarks>
/// <para>
/// A mock returning a stable sort (e.g. by EDHREC inclusion rate) is used in tests.
/// </para>
/// <para>
/// Every <see cref="SelectionResult.OracleId"/> in the returned list must echo an id
/// from the <paramref name="candidates"/> list. Results with unknown ids are discarded
/// by the implementation before they reach the fill engine.
/// </para>
/// <para>
/// The selector receives the current <see cref="BuildState"/> so it can write rationale
/// that references what is already covered — e.g. "fills the remaining CardAdvantage gap
/// since Black Market Connections already handles most of your draw."
/// </para>
/// </remarks>
public interface ICardSelector
{
    /// <param name="role">The role bucket being filled in this call.</param>
    /// <param name="candidates">Pre-classified candidates filtered to this role. Must not be empty.</param>
    /// <param name="context">Immutable build context (commanders, constraints, net targets).</param>
    /// <param name="state">Current fill state, so the selector can reference existing coverage in rationale.</param>
    Task<IReadOnlyList<SelectionResult>> SelectAsync(
        CardRole role,
        IReadOnlyList<FillCandidate> candidates,
        BuildContext context,
        BuildState state,
        CancellationToken ct = default);
}
