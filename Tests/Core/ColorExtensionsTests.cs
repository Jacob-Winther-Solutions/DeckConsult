using EdhDeckBuilder.Core.Cards;

namespace EdhDeckBuilder.Tests.Core;

public sealed class ColorExtensionsTests
{
    // --- IsWithin -----------------------------------------------------------

    [Fact]
    public void Colorless_fits_any_identity()
    {
        Assert.True(Color.None.IsWithin(Color.None));
        Assert.True(Color.None.IsWithin(Color.White));
        Assert.True(Color.None.IsWithin(Color.White | Color.Blue | Color.Black | Color.Red | Color.Green));
    }

    [Fact]
    public void Single_color_fits_matching_identity()
        => Assert.True(Color.White.IsWithin(Color.White));

    [Fact]
    public void Single_color_fits_superset_identity()
        => Assert.True(Color.White.IsWithin(Color.White | Color.Blue));

    [Fact]
    public void Single_color_does_not_fit_disjoint_identity()
        => Assert.False(Color.Red.IsWithin(Color.White | Color.Blue));

    [Fact]
    public void Full_identity_fits_exact_match()
    {
        var atraxa = Color.White | Color.Blue | Color.Black | Color.Green;
        Assert.True(atraxa.IsWithin(atraxa));
    }

    [Fact]
    public void Identity_with_extra_color_does_not_fit()
    {
        var commander = Color.White | Color.Blue;
        var card      = Color.White | Color.Blue | Color.Red;
        Assert.False(card.IsWithin(commander));
    }

    // --- FromScryfall -------------------------------------------------------

    [Fact]
    public void Empty_array_returns_colorless()
        => Assert.Equal(Color.None, ColorExtensions.FromScryfall([]));

    [Theory]
    [InlineData("W", Color.White)]
    [InlineData("U", Color.Blue)]
    [InlineData("B", Color.Black)]
    [InlineData("R", Color.Red)]
    [InlineData("G", Color.Green)]
    public void Single_symbol_maps_to_correct_color(string symbol, Color expected)
        => Assert.Equal(expected, ColorExtensions.FromScryfall([symbol]));

    [Fact]
    public void Multiple_symbols_combine_correctly()
    {
        var result = ColorExtensions.FromScryfall(["W", "U"]);
        Assert.Equal(Color.White | Color.Blue, result);
    }

    [Fact]
    public void All_five_colors_parse()
    {
        var result = ColorExtensions.FromScryfall(["W", "U", "B", "R", "G"]);
        Assert.Equal(Color.White | Color.Blue | Color.Black | Color.Red | Color.Green, result);
    }

    [Fact]
    public void Unknown_symbol_is_ignored()
        => Assert.Equal(Color.None, ColorExtensions.FromScryfall(["X", "Y"]));
}
