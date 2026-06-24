namespace EdhDeckBuilder.Infrastructure.Scryfall;

public sealed class ScryfallOptions
{
    public string CacheDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EdhDeckBuilder", "scryfall");

    public TimeSpan CacheMaxAge { get; set; } = TimeSpan.FromHours(24);
}
