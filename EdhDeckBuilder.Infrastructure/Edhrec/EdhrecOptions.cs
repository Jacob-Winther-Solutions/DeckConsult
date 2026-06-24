namespace EdhDeckBuilder.Infrastructure.Edhrec;

public sealed class EdhrecOptions
{
    public string CacheDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EdhDeckBuilder", "edhrec");

    public TimeSpan CacheMaxAge { get; set; } = TimeSpan.FromDays(7);
}
