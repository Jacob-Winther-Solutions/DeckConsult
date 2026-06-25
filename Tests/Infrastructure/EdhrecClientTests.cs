using EdhDeckBuilder.Infrastructure.Edhrec;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace EdhDeckBuilder.Tests.Infrastructure;

public sealed class EdhrecClientTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task GetCommanderPageAsync_returns_deserialized_page_from_fresh_cache()
    {
        _dir.WriteFile("korvold-fae-cursed-king.json", Fixtures.EdhrecKorvoldPage);

        var client = BuildClient(new HttpClient());  // never called — cache is fresh
        var page = await client.GetCommanderPageAsync("korvold-fae-cursed-king");

        Assert.NotNull(page);
        var cardlists = page.Container?.JsonDict?.Cardlists;
        Assert.NotNull(cardlists);
        Assert.Equal(2, cardlists.Count);
    }

    [Fact]
    public async Task GetCommanderPageAsync_fetches_writes_and_returns_page_when_stale()
    {
        bool fetched = false;
        var handler = new FakeHttpHandler(_ =>
        {
            fetched = true;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Fixtures.EdhrecKorvoldPage, Encoding.UTF8, "application/json"),
            };
        });

        var client = BuildClient(new HttpClient(handler));
        var page = await client.GetCommanderPageAsync("korvold-fae-cursed-king");

        Assert.True(fetched);
        Assert.NotNull(page);
        // The response must also be persisted so the next call hits cache
        Assert.True(File.Exists(_dir.FilePath("korvold-fae-cursed-king.json")));
    }

    [Fact]
    public async Task GetCommanderPageAsync_returns_null_when_edhrec_has_no_page()
    {
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = BuildClient(new HttpClient(handler));

        var page = await client.GetCommanderPageAsync("unknown-commander");

        Assert.Null(page);
    }

    [Fact]
    public async Task GetAverageDeckPageAsync_uses_different_cache_key_than_commander_page()
    {
        // Pre-populate both; they must live in separate files.
        _dir.WriteFile("korvold-fae-cursed-king.json",     Fixtures.EdhrecKorvoldPage);
        _dir.WriteFile("avg-korvold-fae-cursed-king.json", Fixtures.EdhrecKorvoldPage);

        var client = BuildClient(new HttpClient());

        var commander = await client.GetCommanderPageAsync("korvold-fae-cursed-king");
        var average   = await client.GetAverageDeckPageAsync("korvold-fae-cursed-king");

        Assert.NotNull(commander);
        Assert.NotNull(average);
        // If they shared a cache key, writing one would overwrite the other.
        Assert.True(File.Exists(_dir.FilePath("korvold-fae-cursed-king.json")));
        Assert.True(File.Exists(_dir.FilePath("avg-korvold-fae-cursed-king.json")));
    }

    private EdhrecClient BuildClient(HttpClient http) => new(
        http,
        Options.Create(new EdhrecOptions { CacheDirectory = _dir.Path, CacheMaxAge = TimeSpan.FromDays(7) }),
        NullLogger<EdhrecClient>.Instance);
}
