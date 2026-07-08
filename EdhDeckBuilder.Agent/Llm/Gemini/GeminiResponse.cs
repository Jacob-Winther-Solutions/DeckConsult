using System.Text.Json.Serialization;

namespace EdhDeckBuilder.Agent.Llm.Gemini;

/// <summary>
/// Response envelope from Gemini's <c>generateContent</c>. The structured JSON payload
/// lives at <c>Candidates[0].Content.Parts[0].Text</c> and must be <c>JsonSerializer.Deserialize</c>d
/// by the caller against its own DTO.
/// </summary>
public sealed record GeminiResponse
{
    [JsonPropertyName("candidates")]
    public GeminiCandidate[] Candidates { get; init; } = [];

    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; init; }

    /// <summary>
    /// Extracts the first candidate's text payload — the JSON string produced by structured
    /// output — or null if the response is empty, was blocked, or hit MAX_TOKENS mid-JSON.
    /// Callers should read <see cref="Candidates"/>[0].FinishReason when this returns null to
    /// distinguish truncation (raise <c>MaxOutputTokens</c>) from safety blocks.
    /// </summary>
    public string? GetPayloadText()
    {
        if (Candidates.Length == 0)
            return null;

        var candidate = Candidates[0];

        // MAX_TOKENS: the JSON is guaranteed to be truncated mid-token. Handing the
        // fragment to System.Text.Json produces an opaque "unexpected end of data"
        // exception — treat as no payload so the caller can log the real reason.
        if (candidate.FinishReason is "SAFETY" or "RECITATION" or "OTHER" or "MAX_TOKENS")
            return null;

        if (candidate.Content is null || candidate.Content.Parts.Length == 0)
            return null;

        return candidate.Content.Parts[0].Text;
    }
}

public sealed record GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiCandidateContent? Content { get; init; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; init; }
}

public sealed record GeminiCandidateContent
{
    [JsonPropertyName("parts")]
    public GeminiCandidatePart[] Parts { get; init; } = [];

    [JsonPropertyName("role")]
    public string? Role { get; init; }
}

public sealed record GeminiCandidatePart
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";
}

public sealed record GeminiUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; init; }

    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; init; }

    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; init; }
}
