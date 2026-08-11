using EdhDeckBuilder.Web.Services;

namespace EdhDeckBuilder.Tests.Web;

public sealed class ScryfallRefreshSchedulerTests
{
    private static readonly TimeSpan Weekly = TimeSpan.FromDays(7);
    private static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NoCache_ReturnsZero()
    {
        var delay = ScryfallRefreshScheduler.ComputeInitialDelay(null, Weekly, Now);
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void CacheOlderThanInterval_ReturnsZero()
    {
        var lastRefreshed = Now - TimeSpan.FromDays(8);
        var delay = ScryfallRefreshScheduler.ComputeInitialDelay(lastRefreshed, Weekly, Now);
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void CacheExactlyAtInterval_ReturnsZero()
    {
        var lastRefreshed = Now - Weekly;
        var delay = ScryfallRefreshScheduler.ComputeInitialDelay(lastRefreshed, Weekly, Now);
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void CachePartiallyAged_ReturnsRemainingTime()
    {
        var lastRefreshed = Now - TimeSpan.FromDays(1);
        var delay = ScryfallRefreshScheduler.ComputeInitialDelay(lastRefreshed, Weekly, Now);
        Assert.Equal(TimeSpan.FromDays(6), delay);
    }

    [Fact]
    public void CacheJustRefreshed_ReturnsFullInterval()
    {
        var delay = ScryfallRefreshScheduler.ComputeInitialDelay(Now, Weekly, Now);
        Assert.Equal(Weekly, delay);
    }
}
