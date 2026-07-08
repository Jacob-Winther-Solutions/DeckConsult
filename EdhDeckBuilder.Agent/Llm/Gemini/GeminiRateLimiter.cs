namespace EdhDeckBuilder.Agent.Llm.Gemini;

/// <summary>
/// Enforces a minimum spacing between Gemini API calls to stay under free-tier RPM limits.
/// Registered Scoped so each Blazor circuit (i.e. each user's key) gets its own pacing state —
/// two users pointing at the same deployment don't share a semaphore that would otherwise
/// throttle unrelated traffic together.
/// </summary>
/// <remarks>
/// Default spacing: 1050 ms — targets 60 RPM (1 req/s) with a small margin so we don't race
/// Google's counter and 429 anyway. Configure via <see cref="MinSpacingMs"/> if the user
/// upgrades to a paid tier with a higher RPM cap.
/// </remarks>
public sealed class GeminiRateLimiter
{
    /// <summary>Minimum milliseconds between consecutive requests. Adjust for tier/RPM.</summary>
    public int MinSpacingMs { get; set; } = 1050;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastCallUtc = DateTime.MinValue;

    /// <summary>
    /// Blocks until enough time has passed since the last call, then records this call's
    /// timestamp. Serializes so multiple concurrent classifier/selector calls all queue behind
    /// the same spacing rule.
    /// </summary>
    public async Task WaitForSlotAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var elapsed = DateTime.UtcNow - _lastCallUtc;
            var min = TimeSpan.FromMilliseconds(MinSpacingMs);
            if (elapsed < min)
                await Task.Delay(min - elapsed, ct);
            _lastCallUtc = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }
}
