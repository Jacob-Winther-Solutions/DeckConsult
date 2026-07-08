namespace EdhDeckBuilder.Agent.Instrumentation;

/// <summary>
/// Per-model USD price lookup for token-cost estimation in the usage report.
/// Rates are the vendor's <b>standard paid-tier</b> prices per 1M tokens.
/// <para>
/// Free-tier Gemini calls aren't discounted here — the reported "cost" is what the tokens
/// would have cost at paid rates. That's still useful: it tells you what you'd owe if you
/// weren't on free tier, and what the tokens will cost once you attach billing.
/// </para>
/// <para>
/// Unknown model IDs return <see cref="Zero"/> rather than throwing, so a newly added model
/// quietly reports $0 in the usage summary until its price is added to <see cref="Prices"/>.
/// </para>
/// </summary>
public static class ModelPricing
{
    /// <summary>USD price per 1,000,000 tokens, split by direction.</summary>
    public sealed record Rate(decimal InputPerMTokUsd, decimal OutputPerMTokUsd);

    /// <summary>Fallback for models not yet in the table.</summary>
    public static readonly Rate Zero = new(0m, 0m);

    // Rates below are the vendors' standard tier prices at time of writing. Update as
    // published prices change; the 3.x Gemini rows are estimates matched to the 2.5 tier
    // pattern (official pricing not yet published).
    private static readonly Dictionary<string, Rate> Prices = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Anthropic Claude ────────────────────────────────────────────────
        ["claude-haiku-4-5-20251001"] = new(1m,   5m),
        ["claude-sonnet-5"]           = new(3m,  15m),
        ["claude-opus-4-8"]           = new(15m, 75m),

        // ── Google Gemini (standard paid rates; free-tier usage costs $0 in practice) ─
        ["gemini-2.5-flash"]          = new(0.30m,  2.50m),
        ["gemini-2.5-flash-lite"]     = new(0.10m,  0.40m),
        ["gemini-2.0-flash"]          = new(0.10m,  0.40m),
        ["gemini-2.0-flash-lite"]     = new(0.075m, 0.30m),

        // 3.x Gemini rates estimated pending official prices — pattern-matched to 2.5 tier.
        ["gemini-3-flash"]            = new(0.30m,  2.50m),
        ["gemini-3.5-flash"]          = new(0.30m,  2.50m),
        ["gemini-3.1-flash-lite"]     = new(0.10m,  0.40m),
    };

    /// <summary>Returns the rate for a model, or <see cref="Zero"/> if unknown.</summary>
    public static Rate GetRate(string modelId) =>
        Prices.GetValueOrDefault(modelId, Zero);

    /// <summary>
    /// Estimates the USD cost of one call given its input/output token counts.
    /// Unknown models cost $0 (see class remarks).
    /// </summary>
    public static decimal EstimateCost(string modelId, int inputTokens, int outputTokens)
    {
        var rate = GetRate(modelId);
        return (inputTokens * rate.InputPerMTokUsd + outputTokens * rate.OutputPerMTokUsd) / 1_000_000m;
    }
}
