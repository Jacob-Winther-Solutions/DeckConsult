namespace EdhDeckBuilder.Agent.Llm.Shared;

public sealed class LlmRequest
{
    public required string Model { get; init; }
    public required int MaxTokens { get; init; }

    /// <summary>
    /// Sampling temperature. Null means "use provider default".
    /// Classifiers typically use 0.1; selectors 0.6.
    /// </summary>
    public double? Temperature { get; init; }

    public string? SystemPrompt { get; init; }
    public required IReadOnlyList<LlmMessage> Messages { get; init; }
    public IReadOnlyList<LlmToolDefinition>? Tools { get; init; }

    /// <summary>
    /// Forces the model to call the named tool. Both Anthropic and Gemini honor this.
    /// </summary>
    public string? ForcedToolName { get; init; }

    /// <summary>
    /// Hint to the adapter to enable prompt caching on stable prefixes (e.g. system prompt).
    /// Adapters that do not support caching silently no-op this flag.
    /// </summary>
    public bool EnableCaching { get; init; }
}
