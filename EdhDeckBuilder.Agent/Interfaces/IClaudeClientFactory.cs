using Anthropic;

namespace EdhDeckBuilder.Agent.Interfaces;

/// <summary>
/// The sole seam touching the Anthropic SDK constructor. Builds a client bound to the
/// current user's API key. Scoped so each circuit gets its own instance.
/// </summary>
public interface IClaudeClientFactory
{
    /// <summary>Creates a client for the current user's key. Throws <see cref="Authentication.MissingApiKeyException"/> if no key is set.</summary>
    AnthropicClient CreateForCurrentUser();

    /// <summary>The Claude model to use for card selection (user-configurable via settings).</summary>
    string SelectionModel { get; }
}
