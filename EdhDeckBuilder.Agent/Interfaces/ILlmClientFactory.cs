using EdhDeckBuilder.Agent.Llm.Shared;

namespace EdhDeckBuilder.Agent.Interfaces;

/// <summary>
/// Per-circuit factory that creates an <see cref="ILlmClient"/> bound to the current user's
/// API key and selected model. Resolved once per Blazor Server circuit; the DI dispatch picks
/// either the Claude or Gemini implementation based on <c>SessionApiKeyProvider.ActiveProvider</c>.
/// </summary>
public interface ILlmClientFactory
{
    /// <summary>
    /// Creates (or returns) an <see cref="ILlmClient"/> for the current circuit's API key.
    /// Throws <see cref="Authentication.MissingApiKeyException"/> if no key is set.
    /// </summary>
    ILlmClient CreateForCurrentUser();

    /// <summary>The model ID to use for card/commander selection (user-configurable).</summary>
    string SelectedModel { get; }

    /// <summary>
    /// The model ID to use for classification. For Claude this is always Haiku (fast, cheap);
    /// for Gemini it matches the user-selected model.
    /// </summary>
    string ClassificationModel { get; }

    /// <summary>Max output tokens for classifier calls. Differs by provider (Haiku caps at 8 192; Gemini supports 32 768 for verbose reasoning).</summary>
    int ClassifierMaxOutputTokens { get; }

    /// <summary>Max output tokens for selector calls. 8 192 is sufficient for both providers.</summary>
    int SelectorMaxOutputTokens { get; }
}
