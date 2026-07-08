using EdhDeckBuilder.Agent.Authentication;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EdhDeckBuilder.Agent.Llm.Gemini;

/// <summary>
/// Thin HTTP wrapper for Gemini's <c>generateContent</c> endpoint. Handles the request
/// envelope (system instruction, contents, generationConfig with responseSchema) and the
/// response envelope (candidates → parts → text, usageMetadata). One instance is created per
/// Blazor circuit by <see cref="GeminiClientFactory"/>.
/// <para>
/// Applies per-circuit pacing via <see cref="GeminiRateLimiter"/> and retries once on 429,
/// honoring the <c>Retry-After</c> header when Google supplies one. On any non-success status
/// (after retries exhausted) the response body is parsed for Google's structured error and
/// surfaced in the exception message.
/// </para>
/// </summary>
public sealed class GeminiRestClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    private const int MaxAttempts = 3;

    // The transient-failure family — 429 (rate limit), 502 (bad gateway), 503 (model
    // overloaded), 504 (upstream timeout). All are conventionally safe to retry with
    // backoff; Google's own 503 body says "please try again later."
    private static readonly HashSet<HttpStatusCode> RetryableStatuses =
    [
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
    ];

    private static readonly JsonSerializerOptions RequestOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions ResponseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly GeminiRateLimiter _limiter;
    private readonly ILogger _logger;

    public GeminiRestClient(
        HttpClient http,
        string apiKey,
        string model,
        GeminiRateLimiter limiter,
        ILogger logger)
    {
        _http = http;
        _apiKey = apiKey;
        _limiter = limiter;
        _logger = logger;
        Model = model;
    }

    public string Model { get; }

    /// <summary>
    /// Calls <c>generateContent</c> with a JSON-schema-constrained response. Throws
    /// <see cref="ApiKeyRejectedException"/> on 401/403 (matching the Anthropic path so the
    /// UI's reconnect prompt lights up). Retries 429s with backoff up to <see cref="MaxAttempts"/>
    /// times. Any other non-success is surfaced with Google's error body parsed out.
    /// </summary>
    public async Task<GeminiResponse> GenerateContentAsync(
        string systemInstruction,
        string userMessage,
        JsonNode responseSchema,
        double temperature,
        int maxOutputTokens,
        CancellationToken ct)
    {
        var url = $"{BaseUrl}{Model}:generateContent";
        var bodyJson = JsonSerializer.Serialize(new GeminiRequest
        {
            SystemInstruction = new GeminiTextPart { Parts = [new GeminiPart { Text = systemInstruction }] },
            Contents =
            [
                new GeminiContent
                {
                    Role  = "user",
                    Parts = [new GeminiPart { Text = userMessage }],
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature       = temperature,
                MaxOutputTokens   = maxOutputTokens,
                ResponseMimeType  = "application/json",
                ResponseSchema    = responseSchema,
            },
        }, RequestOptions);

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await _limiter.WaitForSlotAsync(ct);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-goog-api-key", _apiKey);
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new ApiKeyRejectedException(new HttpRequestException(
                    $"Gemini API rejected the key ({(int)response.StatusCode}): {ExtractErrorMessage(body)}"));
            }

            if (RetryableStatuses.Contains(response.StatusCode) && attempt < MaxAttempts)
            {
                var delay = GetRetryDelay(response, attempt);
                _logger.LogWarning(
                    "Gemini returned {StatusCode} {ReasonPhrase} (attempt {Attempt}/{Max}); retrying in {DelaySec:F1}s. Error: {Error}",
                    (int)response.StatusCode, response.ReasonPhrase,
                    attempt, MaxAttempts, delay.TotalSeconds, ExtractErrorMessage(body));
                await Task.Delay(delay, ct);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Gemini API returned {(int)response.StatusCode} {response.ReasonPhrase}: {ExtractErrorMessage(body)}");
            }

            return JsonSerializer.Deserialize<GeminiResponse>(body, ResponseOptions)
                ?? throw new InvalidOperationException("Gemini returned an empty response body.");
        }

        // Unreachable: the loop either returns, throws, or continues; the last iteration
        // (attempt == MaxAttempts) falls through to the non-success branch.
        throw new InvalidOperationException("Gemini retry loop exhausted without result.");
    }

    /// <summary>
    /// Extracts a human-readable summary from Google's error envelope. Google returns:
    /// <c>{ "error": { "code": 429, "message": "...", "status": "RESOURCE_EXHAUSTED", "details": [...] } }</c>
    /// The <c>details</c> array typically contains a <c>QuotaFailure</c> naming the exact metric
    /// (e.g. <c>generate_content_free_tier_requests</c>) — critical for diagnosing which limit tripped.
    /// </summary>
    private static string ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "(empty response body)";

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var err))
                return Truncate(body);

            var parts = new List<string>();
            if (err.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String)
                parts.Add($"status={status.GetString()}");
            if (err.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                parts.Add($"message=\"{msg.GetString()}\"");
            if (err.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                parts.Add($"details={details.GetRawText()}");

            return parts.Count > 0 ? string.Join("; ", parts) : Truncate(body);
        }
        catch (JsonException)
        {
            return Truncate(body);
        }
    }

    private static string Truncate(string body) =>
        body.Length > 500 ? body[..500] + "…" : body;

    /// <summary>
    /// Preferred delay for the next retry: honor Google's <c>Retry-After</c> header if
    /// present (either seconds or an HTTP date), otherwise fall back to exponential backoff.
    /// </summary>
    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is not null)
        {
            if (retryAfter.Delta is TimeSpan delta && delta > TimeSpan.Zero)
                return delta;
            if (retryAfter.Date is DateTimeOffset when)
            {
                var wait = when - DateTimeOffset.UtcNow;
                if (wait > TimeSpan.Zero)
                    return wait;
            }
        }
        // Backoff: 2s, 4s
        return TimeSpan.FromSeconds(Math.Pow(2, attempt));
    }
}

// ─── Request envelope ─────────────────────────────────────────────────────────────

internal sealed record GeminiRequest
{
    [JsonPropertyName("systemInstruction")]
    public GeminiTextPart? SystemInstruction { get; init; }

    [JsonPropertyName("contents")]
    public GeminiContent[] Contents { get; init; } = [];

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; init; }
}

internal sealed record GeminiTextPart
{
    [JsonPropertyName("parts")]
    public GeminiPart[] Parts { get; init; } = [];
}

internal sealed record GeminiContent
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "user";

    [JsonPropertyName("parts")]
    public GeminiPart[] Parts { get; init; } = [];
}

internal sealed record GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";
}

internal sealed record GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; init; }

    [JsonPropertyName("responseMimeType")]
    public string? ResponseMimeType { get; init; }

    [JsonPropertyName("responseSchema")]
    public JsonNode? ResponseSchema { get; init; }
}
