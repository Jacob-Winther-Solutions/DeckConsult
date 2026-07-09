using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Fill;

/// <summary>
/// The output of a <see cref="FillEngine"/> run: the populated build state, coverage warnings,
/// and the LLM-generated rationale per committed card (keyed by OracleId).
/// Cards committed via spillover or ColorFixingPass have no entry in <see cref="SelectionRationales"/>
/// and receive a generic reason when assembled into the final result.
/// </summary>
public sealed record FillResult(
    BuildState State,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<Guid, string> SelectionRationales,
    IReadOnlyDictionary<CardRole, (int Input, int Ranked)> SelectorStats);
