namespace EdhDeckBuilder.Agent.Llm.Shared;

public sealed class LlmUsage
{
    public required int InputTokens { get; init; }
    public required int OutputTokens { get; init; }

    /// <summary>
    /// Anthropic-specific: tokens written to the prompt cache on this call.
    /// Null for providers without this concept.
    /// </summary>
    public int? CacheCreationInputTokens { get; init; }

    /// <summary>
    /// Anthropic-specific: tokens read from the prompt cache on this call.
    /// Null for providers without this concept.
    /// </summary>
    public int? CacheReadInputTokens { get; init; }
}

public sealed class LlmResponse
{
    public required IReadOnlyList<LlmContentBlock> Content { get; init; }
    public required LlmUsage Usage { get; init; }
    public required string StopReason { get; init; }
}
