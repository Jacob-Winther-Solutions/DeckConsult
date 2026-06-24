namespace EdhDeckBuilder.Core.Cards;

/// <summary>
/// The five colors of Magic (WUBRG), modeled as flags so a color identity is just a
/// combination of these. Colorless is represented by <see cref="None"/>. Treating identity
/// as a bit set makes the core EDH legality check a single bitwise operation.
/// </summary>
[Flags]
public enum Color
{
    None  = 0,
    White = 1 << 0,
    Blue  = 1 << 1,
    Black = 1 << 2,
    Red   = 1 << 3,
    Green = 1 << 4,
}

public static class ColorExtensions
{
    /// <summary>
    /// True if <paramref name="identity"/> fits inside the <paramref name="commander"/>'s
    /// color identity — the central EDH legality check. A colorless card (None) always fits.
    /// </summary>
    public static bool IsWithin(this Color identity, Color commander)
        => (identity & ~commander) == Color.None;

    /// <summary>Parses a Scryfall <c>color_identity</c> array, e.g. ["U","R"]. Never compute this ourselves.</summary>
    public static Color FromScryfall(IEnumerable<string> symbols)
    {
        var result = Color.None;
        foreach (var s in symbols)
        {
            result |= s switch
            {
                "W" => Color.White,
                "U" => Color.Blue,
                "B" => Color.Black,
                "R" => Color.Red,
                "G" => Color.Green,
                _   => Color.None,
            };
        }
        return result;
    }
}
