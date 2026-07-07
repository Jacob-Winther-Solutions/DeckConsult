using System.Text.Json.Serialization;

namespace EdhDeckBuilder.Agent.Llm;

// Internal JSON parsing types. Match the tool output schemas defined in ClassificationPrompt
// and SelectionPrompt. Not exposed outside the Llm/ folder.

internal sealed record ClassificationBatchDto
{
    [JsonPropertyName("classifications")]
    public List<CardClassificationDto> Classifications { get; init; } = [];
}

internal sealed record CardClassificationDto
{
    [JsonPropertyName("oracle_id")]
    public string OracleId { get; init; } = "";

    [JsonPropertyName("primary_role")]
    public string PrimaryRole { get; init; } = "";

    [JsonPropertyName("secondary")]
    public List<SecondaryRoleDto> Secondary { get; init; } = [];

    [JsonPropertyName("land_credit")]
    public double LandCredit { get; init; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; init; }
}

internal sealed record SecondaryRoleDto
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "";

    [JsonPropertyName("relation")]
    public string Relation { get; init; } = "";

    [JsonPropertyName("weight")]
    public double Weight { get; init; } = 1.0;
}

internal sealed record SelectionBatchDto
{
    [JsonPropertyName("selections")]
    public List<CardSelectionDto> Selections { get; init; } = [];
}

internal sealed record CardSelectionDto
{
    [JsonPropertyName("oracle_id")]
    public string OracleId { get; init; } = "";

    [JsonPropertyName("rank")]
    public int Rank { get; init; }

    [JsonPropertyName("rationale")]
    public string Rationale { get; init; } = "";
}

internal sealed record CommanderRankingDto
{
    [JsonPropertyName("oracle_id")]
    public string OracleId { get; init; } = "";

    [JsonPropertyName("rank")]
    public int Rank { get; init; }

    [JsonPropertyName("rationale")]
    public string Rationale { get; init; } = "";
}
