using EdhDeckBuilder.Agent.Analysis;

namespace EdhDeckBuilder.Tests.Agent;

public sealed class DecklistParserTests
{
    private readonly DecklistParser _parser = new();

    [Fact]
    public void Parse_PlainFormat_ExtractsNames()
    {
        var input = """
            1 Sol Ring
            1 Rhystic Study
            1 Cyclonic Rift
            """;

        var result = _parser.Parse(input);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, e => e.Name == "Sol Ring");
        Assert.Contains(result, e => e.Name == "Rhystic Study");
        Assert.Contains(result, e => e.Name == "Cyclonic Rift");
    }

    [Fact]
    public void Parse_PlainFormat_DefaultsToQuantityOne()
    {
        var input = "1 Sol Ring";

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal(1, result[0].Quantity);
    }

    [Fact]
    public void Parse_QuantityWithX_ExtractsNamesAndQuantities()
    {
        var input = "1x Sol Ring\n2x Swamp";

        var result = _parser.Parse(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("Sol Ring", result[0].Name);
        Assert.Equal(1, result[0].Quantity);
        Assert.Equal("Swamp", result[1].Name);
        Assert.Equal(2, result[1].Quantity);
    }

    [Fact]
    public void Parse_HighQuantity_PreservesCount()
    {
        var input = "30 Plains";

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal("Plains", result[0].Name);
        Assert.Equal(30, result[0].Quantity);
    }

    [Fact]
    public void Parse_SkipsBlankLines()
    {
        var input = "1 Sol Ring\n\n1 Rhystic Study\n\n";

        var result = _parser.Parse(input);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_SkipsCommentLines()
    {
        var input = "// This is a comment\n1 Sol Ring\n// Another comment";

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal("Sol Ring", result[0].Name);
    }

    [Fact]
    public void Parse_SkipsKnownSectionHeaders()
    {
        var input = """
            Lands
            1 Command Tower
            Creatures
            1 Rhystic Study
            """;

        var result = _parser.Parse(input);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Name == "Command Tower");
        Assert.Contains(result, e => e.Name == "Rhystic Study");
    }

    [Fact]
    public void Parse_SectionHeadersCaseInsensitive()
    {
        var input = "LANDS\n1 Command Tower\nlANds\n1 Sol Ring";

        var result = _parser.Parse(input);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_BareCardNameWithoutQuantity_IncludesAsName()
    {
        var input = "Sol Ring\nRhystic Study";

        var result = _parser.Parse(input);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Name == "Sol Ring");
        Assert.Contains(result, e => e.Name == "Rhystic Study");
    }

    [Fact]
    public void Parse_BareCardNameWithoutQuantity_DefaultsToQuantityOne()
    {
        var input = "Sol Ring";

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal(1, result[0].Quantity);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty()
    {
        var result = _parser.Parse(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WhitespaceOnlyInput_ReturnsEmpty()
    {
        var result = _parser.Parse("   \n  \n  ");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_CardWithCommaInName_PreservesFullName()
    {
        var input = "1 Atraxa, Praetors' Voice";

        var result = _parser.Parse(input);

        Assert.Single(result);
        Assert.Equal("Atraxa, Praetors' Voice", result[0].Name);
    }

    [Fact]
    public void Parse_DuplicateNames_BothKept()
    {
        // Dedup happens at resolution time, not parsing
        var input = "1 Sol Ring\n1 Sol Ring";

        var result = _parser.Parse(input);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_WindowsLineEndings_ParsedCorrectly()
    {
        var input = "1 Sol Ring\r\n1 Rhystic Study\r\n";

        var result = _parser.Parse(input);

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.DoesNotContain('\r', e.Name));
    }
}
