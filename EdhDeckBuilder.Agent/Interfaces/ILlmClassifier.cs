using EdhDeckBuilder.Agent.Classification;
using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Agent.Interfaces;

/// <summary>
/// Classifies a batch of candidate cards into functional roles, given the commander context.
/// The LLM implementation lives in <c>EdhDeckBuilder.Agent.Llm.LlmClassifier</c>;
/// a mock implementation is used in fill-engine tests to keep them fast and API-free.
/// </summary>
/// <remarks>
/// <para>
/// Cards are classified in batches — one API call per batch, not one per card. The caller
/// is responsible for splitting into sensibly-sized batches if the pool is very large.
/// </para>
/// <para>
/// Classification is cached globally by <c>OracleId</c> for stable roles (Ramp,
/// TargetedDisruption, MassDisruption, etc.). <c>Plan</c> and <c>Synergy</c> are
/// commander-dependent and are re-classified per build — they are never cached.
/// </para>
/// <para>
/// Every <see cref="ClassificationResult.OracleId"/> in the returned list must echo an id
/// from the input batch. Results with unknown ids are discarded by the implementation before
/// they reach the caller.
/// </para>
/// </remarks>
public interface ILlmClassifier
{
    /// <param name="candidates">Cards to classify. Must not be empty.</param>
    /// <param name="commanders">
    /// The commander(s) for this build, included in the prompt so the model can assign
    /// <c>Plan</c> and <c>Synergy</c> roles with commander-specific context.
    /// </param>
    Task<IReadOnlyList<ClassificationResult>> ClassifyBatchAsync(
        IReadOnlyList<CardCandidate> candidates,
        IReadOnlyList<Card> commanders,
        CancellationToken ct = default);
}
