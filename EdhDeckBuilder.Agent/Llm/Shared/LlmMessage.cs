using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Agent.Llm.Shared;

public enum LlmRole { User, Assistant }

public abstract class LlmContentBlock { }

public sealed class LlmTextBlock : LlmContentBlock
{
    public required string Text { get; init; }
}

public sealed class LlmToolUseBlock : LlmContentBlock
{
    public required string Id { get; init; }
    public required string ToolName { get; init; }
    public required JsonNode Input { get; init; }
}

public sealed class LlmToolResultBlock : LlmContentBlock
{
    public required string ToolUseId { get; init; }
    public required string Result { get; init; }
}

public sealed class LlmMessage
{
    public required LlmRole Role { get; init; }
    public required IReadOnlyList<LlmContentBlock> Content { get; init; }
}
