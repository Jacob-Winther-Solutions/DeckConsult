using EdhDeckBuilder.Infrastructure.Scryfall;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace EdhDeckBuilder.Tests.Infrastructure;

public sealed class ScryfallBulkClientTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task GetOracleCardsFileAsync_returns_cached_path_when_fresh()
    {
        _dir.WriteFile("oracle_cards.json", Fixtures.ScryfallOracleCards);
        // File was just written → last-write time is now → within 24 h → fresh.
        // HttpClient should never be called; passing a bare instance is safe.
        var client = BuildClient(new HttpClient());

        var path = await client.GetOracleCardsFileAsync();

        Assert.Equal(_dir.FilePath("oracle_cards.json"), path);
    }

    [Fact]
    public async Task GetOracleCardsFileAsync_downloads_when_cache_is_stale()
    {
        // Write a file whose last-write time is older than the max age.
        var cachePath = _dir.FilePath("oracle_cards.json");
        File.WriteAllText(cachePath, "[]");
        File.SetLastWriteTimeUtc(cachePath, DateTime.UtcNow.AddDays(-2));

        const string DownloadUri = "https://example.com/oracle_cards.json";
        int callCount = 0;

        var handler = new FakeHttpHandler(req =>
        {
            callCount++;
            var json = req.RequestUri!.Host == "api.scryfall.com"
                ? Fixtures.ScryfallBulkManifest(DownloadUri)   // manifest request
                : Fixtures.ScryfallOracleCards;                 // download request

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        var client = BuildClient(new HttpClient(handler));
        await client.GetOracleCardsFileAsync();

        Assert.Equal(2, callCount);  // manifest + download
    }

    [Fact]
    public async Task GetOracleCardsFileAsync_creates_cache_directory_when_missing()
    {
        var nestedDir = Path.Combine(_dir.Path, "sub", "cache");
        const string DownloadUri = "https://example.com/oracle_cards.json";

        var handler = new FakeHttpHandler(req =>
        {
            var json = req.RequestUri!.Host == "api.scryfall.com"
                ? Fixtures.ScryfallBulkManifest(DownloadUri)
                : Fixtures.ScryfallOracleCards;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        var client = new ScryfallBulkClient(
            new HttpClient(handler),
            Options.Create(new ScryfallOptions { CacheDirectory = nestedDir, CacheMaxAge = TimeSpan.FromHours(24) }),
            NullLogger<ScryfallBulkClient>.Instance);

        await client.GetOracleCardsFileAsync();

        Assert.True(Directory.Exists(nestedDir));
        Assert.True(File.Exists(Path.Combine(nestedDir, "oracle_cards.json")));
    }

    private ScryfallBulkClient BuildClient(HttpClient http) => new(
        http,
        Options.Create(new ScryfallOptions { CacheDirectory = _dir.Path, CacheMaxAge = TimeSpan.FromHours(24) }),
        NullLogger<ScryfallBulkClient>.Instance);
}
