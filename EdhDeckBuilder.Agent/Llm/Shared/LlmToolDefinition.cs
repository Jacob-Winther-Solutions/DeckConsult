using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Agent.Llm.Shared;

public sealed class LlmToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>
    /// JSON Schema for the tool's input parameters (Anthropic's input_schema format).
    /// Each adapter translates this to its own wire format.
    /// </summary>
    public required JsonNode InputSchema { get; init; }
}
