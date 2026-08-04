using EdhDeckBuilder.Agent.Analysis;

namespace EdhDeckBuilder.Tests.Agent;

public sealed class DecklistParserTests
{
    private readonly DecklistParser _parser = new();

    // ── Plain format ────────────────────────────────────────────────────────

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
        var result = _parser.Parse("1 Sol Ring");

        Assert.Single(result);
        Assert.Equal(1, result[0].Quantity);
    }

    [Fact]
    public void Parse_QuantityWithX_ExtractsNamesAndQuantities()
    {
        var result = _parser.Parse("1x Sol Ring\n2x Swamp");

        Assert.Equal(2, result.Count);
        Assert.Equal("Sol Ring", result[0].Name);
        Assert.Equal(1, result[0].Quantity);
        Assert.Equal("Swamp", result[1].Name);
        Assert.Equal(2, result[1].Quantity);
    }

    [Fact]
    public void Parse_HighQuantity_PreservesCount()
    {
        var result = _parser.Parse("30 Plains");

        Assert.Single(result);
        Assert.Equal("Plains", result[0].Name);
        Assert.Equal(30, result[0].Quantity);
    }

    [Fact]
    public void Parse_SkipsBlankLines()
    {
        var result = _parser.Parse("1 Sol Ring\n\n1 Rhystic Study\n\n");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_SkipsCommentLines()
    {
        var result = _parser.Parse("// This is a comment\n1 Sol Ring\n// Another comment");

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
        var result = _parser.Parse("LANDS\n1 Command Tower\nlANds\n1 Sol Ring");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_BareCardNameWithoutQuantity_IncludesAsName()
    {
        var result = _parser.Parse("Sol Ring\nRhystic Study");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Name == "Sol Ring");
        Assert.Contains(result, e => e.Name == "Rhystic Study");
    }

    [Fact]
    public void Parse_BareCardNameWithoutQuantity_DefaultsToQuantityOne()
    {
        var result = _parser.Parse("Sol Ring");

        Assert.Single(result);
        Assert.Equal(1, result[0].Quantity);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(_parser.Parse(string.Empty));
    }

    [Fact]
    public void Parse_WhitespaceOnlyInput_ReturnsEmpty()
    {
        Assert.Empty(_parser.Parse("   \n  \n  "));
    }

    [Fact]
    public void Parse_CardWithCommaInName_PreservesFullName()
    {
        var result = _parser.Parse("1 Atraxa, Praetors' Voice");

        Assert.Single(result);
        Assert.Equal("Atraxa, Praetors' Voice", result[0].Name);
    }

    [Fact]
    public void Parse_DuplicateNames_BothKept()
    {
        // Dedup happens at resolution time, not parsing
        var result = _parser.Parse("1 Sol Ring\n1 Sol Ring");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_WindowsLineEndings_ParsedCorrectly()
    {
        var result = _parser.Parse("1 Sol Ring\r\n1 Rhystic Study\r\n");

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.DoesNotContain('\r', e.Name));
    }

    // ── Arena / Moxfield set-code format ────────────────────────────────────

    [Fact]
    public void Parse_ArenaSetCode_StripsCodeAndNumber()
    {
        var result = _parser.Parse("1 Sol Ring (CMR) 456");

        Assert.Single(result);
        Assert.Equal("Sol Ring", result[0].Name);
        Assert.Equal(1, result[0].Quantity);
    }

    [Fact]
    public void Parse_ArenaSetCode_PreservesQuantity()
    {
        var result = _parser.Parse("2 Swamp (AFR) 270");

        Assert.Single(result);
        Assert.Equal("Swamp", result[0].Name);
        Assert.Equal(2, result[0].Quantity);
    }

    [Fact]
    public void Parse_ArenaSetCode_FoilSuffixStripped()
    {
        var result = _parser.Parse("1 Sol Ring (CMR) 456 *F*");

        Assert.Single(result);
        Assert.Equal("Sol Ring", result[0].Name);
    }

    [Fact]
    public void Parse_ArenaSetCode_CardWithCommaName()
    {
        var result = _parser.Parse("1 Atraxa, Praetors' Voice (CMR) 2");

        Assert.Single(result);
        Assert.Equal("Atraxa, Praetors' Voice", result[0].Name);
    }

    [Fact]
    public void Parse_ArenaSetCode_MdfcCardPreservesDoubleFaceSlash()
    {
        // MDFC card names use " // " — that should be preserved, only the set code stripped
        var result = _parser.Parse("1 Bala Ged Recovery // Bala Ged Sanctuary (ZNR) 180");

        Assert.Single(result);
        Assert.Equal("Bala Ged Recovery // Bala Ged Sanctuary", result[0].Name);
    }

    [Fact]
    public void Parse_ArenaFullExport_CommanderSectionSkipped_DeckSectionParsed()
    {
        var input = """
            Commander
            1 Atraxa, Praetors' Voice (CMR) 2

            Deck
            1 Sol Ring (CMR) 456
            1 Rhystic Study (CMR) 400
            30 Plains (AFR) 265
            """;

        var result = _parser.Parse(input);

        // Commander header is skipped; the commander card line IS parsed (dedup at resolve time)
        Assert.Equal(4, result.Count);
        Assert.Contains(result, e => e.Name == "Atraxa, Praetors' Voice");
        Assert.Contains(result, e => e.Name == "Sol Ring");
        Assert.Contains(result, e => e.Name == "Rhystic Study");
        Assert.Contains(result, e => e.Name == "Plains" && e.Quantity == 30);
        Assert.DoesNotContain(result, e => e.Name.Contains("(CMR)"));
    }

    [Fact]
    public void Parse_ArenaExport_CompanionSectionHeaderSkipped()
    {
        var input = """
            Companion
            1 Lutri, the Spellchaser (IKO) 226

            Commander
            1 Korvold, Fae-Cursed King (ELD) 329

            Deck
            1 Sol Ring (CMR) 456
            """;

        var result = _parser.Parse(input);

        // Companion, Commander section headers are skipped; their card lines ARE included
        Assert.DoesNotContain(result, e => e.Name.Contains("(IKO)") || e.Name.Contains("(ELD)") || e.Name.Contains("(CMR)"));
        Assert.Contains(result, e => e.Name == "Sol Ring");
        Assert.Contains(result, e => e.Name == "Lutri, the Spellchaser");
        Assert.Contains(result, e => e.Name == "Korvold, Fae-Cursed King");
    }

    // ── Bracket set-code format (Archidekt / MTGO) ──────────────────────────

    [Fact]
    public void Parse_BracketSetCode_StripsCode()
    {
        var result = _parser.Parse("1 Sol Ring [CMR]");

        Assert.Single(result);
        Assert.Equal("Sol Ring", result[0].Name);
        Assert.Equal(1, result[0].Quantity);
    }

    [Fact]
    public void Parse_BracketSetCode_FullSetNameInBrackets()
    {
        var result = _parser.Parse("1 Sol Ring [Commander Masters]");

        Assert.Single(result);
        Assert.Equal("Sol Ring", result[0].Name);
    }

    [Fact]
    public void Parse_BracketSetCode_CardWithCommaName()
    {
        var result = _parser.Parse("1 Atraxa, Praetors' Voice [CMR]");

        Assert.Single(result);
        Assert.Equal("Atraxa, Praetors' Voice", result[0].Name);
    }

    [Fact]
    public void Parse_BracketSetCode_BareNameWithCode_StripsCode()
    {
        // Bare name (no quantity) with bracket set code
        var result = _parser.Parse("Sol Ring [CMR]");

        Assert.Single(result);
        Assert.Equal("Sol Ring", result[0].Name);
        Assert.Equal(1, result[0].Quantity);
    }

    // ── Moxfield / Archidekt # section headers ───────────────────────────────

    [Fact]
    public void Parse_HashSectionHeader_Skipped()
    {
        var result = _parser.Parse("# Lands\n1 Command Tower");

        Assert.Single(result);
        Assert.Equal("Command Tower", result[0].Name);
    }

    [Fact]
    public void Parse_HashComment_Skipped()
    {
        var result = _parser.Parse("# Ramp\n1 Sol Ring\n# Card Draw\n1 Rhystic Study");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Name == "Sol Ring");
        Assert.Contains(result, e => e.Name == "Rhystic Study");
    }

    [Fact]
    public void Parse_HashWithSetCode_EntireLineSkipped()
    {
        // Some exports add "# 40 cards" type annotations
        var result = _parser.Parse("# 40 cards\n1 Sol Ring");

        Assert.Single(result);
        Assert.Equal("Sol Ring", result[0].Name);
    }

    [Fact]
    public void Parse_MoxfieldExport_AllFormatsHandled()
    {
        // Realistic Moxfield export combining # headers and (SET) NNN codes
        var input = """
            # Ramp
            1 Sol Ring (CMR) 456
            1 Cultivate (M21) 177

            # Card Draw
            1 Rhystic Study (CMR) 400
            1 Phyrexian Arena (2XM) 107
            """;

        var result = _parser.Parse(input);

        Assert.Equal(4, result.Count);
        Assert.Contains(result, e => e.Name == "Sol Ring");
        Assert.Contains(result, e => e.Name == "Cultivate");
        Assert.Contains(result, e => e.Name == "Rhystic Study");
        Assert.Contains(result, e => e.Name == "Phyrexian Arena");
        Assert.All(result, e => Assert.DoesNotContain("(", e.Name));
    }

    // ── Count-suffixed section headers ───────────────────────────────────────

    [Fact]
    public void Parse_SectionHeaderWithCount_Skipped()
    {
        var result = _parser.Parse("Creatures (24)\n1 Sol Ring");

        Assert.Single(result);
        Assert.Equal("Sol Ring", result[0].Name);
    }

    [Fact]
    public void Parse_SectionHeaderWithCountCaseInsensitive_Skipped()
    {
        var result = _parser.Parse("LANDS (36)\n1 Command Tower");

        Assert.Single(result);
        Assert.Equal("Command Tower", result[0].Name);
    }

    [Fact]
    public void Parse_ArchidektExport_CountSuffixedHeaders()
    {
        // Archidekt-style export with card counts on section headers
        var input = """
            Creatures (12)
            1 Dockside Extortionist [CMR]
            1 Meren of Clan Nel Toth [CMR]

            Instants (8)
            1 Cyclonic Rift [CMR]
            """;

        var result = _parser.Parse(input);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, e => e.Name == "Dockside Extortionist");
        Assert.Contains(result, e => e.Name == "Meren of Clan Nel Toth");
        Assert.Contains(result, e => e.Name == "Cyclonic Rift");
    }

    // ── SB: sideboard prefix ─────────────────────────────────────────────────

    [Fact]
    public void Parse_SideboardPrefix_Skipped()
    {
        var result = _parser.Parse("SB: 1 Sol Ring\n1 Rhystic Study");

        Assert.Single(result);
        Assert.Equal("Rhystic Study", result[0].Name);
    }

    [Fact]
    public void Parse_SideboardPrefixCaseInsensitive_Skipped()
    {
        var result = _parser.Parse("sb: 1 Sol Ring\n1 Rhystic Study");

        Assert.Single(result);
        Assert.Equal("Rhystic Study", result[0].Name);
    }
}
