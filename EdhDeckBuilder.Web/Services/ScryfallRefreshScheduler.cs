namespace EdhDeckBuilder.Web.Services;

internal static class ScryfallRefreshScheduler
{
    /// <summary>
    /// How long to wait before the first background refresh run.
    /// Returns zero when no cache exists or when the cache is at or past the interval age.
    /// </summary>
    internal static TimeSpan ComputeInitialDelay(
        DateTimeOffset? lastRefreshed,
        TimeSpan interval,
        DateTimeOffset now)
    {
        if (lastRefreshed is null) return TimeSpan.Zero;
        var age = now - lastRefreshed.Value;
        return age >= interval ? TimeSpan.Zero : interval - age;
    }
}
