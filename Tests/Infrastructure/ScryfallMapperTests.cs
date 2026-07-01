using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Infrastructure.Scryfall;
using EdhDeckBuilder.Infrastructure.Scryfall.Dto;

namespace EdhDeckBuilder.Tests.Infrastructure;

public sealed class ScryfallMapperTests
{
    // --- helpers ------------------------------------------------------------

    private static ScryfallCard Make(
        string name        = "Test Card",
        string typeLine    = "Sorcery",
        string manaCost    = "{1}",
        decimal cmc        = 1,
        string[]? colors   = null,
        string legality    = "legal",
        string? oracleText = null,   // null → mapper falls back to face oracle text or ""
        ScryfallImageUris? images   = null,
        List<ScryfallCardFace>? faces = null) => new()
    {
        Id              = Guid.NewGuid(),
        OracleId        = Guid.NewGuid(),
        Name            = name,
        ManaCost        = manaCost,
        Cmc             = cmc,
        ColorIdentity   = colors?.ToList() ?? [],
        TypeLine        = typeLine,
        OracleText      = oracleText,
        Legalities      = new ScryfallLegalities { Commander = legality },
        ImageUris       = images,
        CardFaces       = faces,
    };

    // --- type line parsing --------------------------------------------------

    [Theory]
    [InlineData("Creature — Human Wizard",    CardType.Creature)]
    [InlineData("Instant",                    CardType.Instant)]
    [InlineData("Sorcery",                    CardType.Sorcery)]
    [InlineData("Enchantment — Aura",         CardType.Enchantment)]
    [InlineData("Artifact",                   CardType.Artifact)]
    [InlineData("Land",                       CardType.Land)]
    [InlineData("Planeswalker — Jace",        CardType.Planeswalker)]
    [InlineData("Battle — Siege",             CardType.Battle)]
    public void Type_line_maps_to_correct_flag(string typeLine, CardType expected)
    {
        var card = ScryfallMapper.ToCard(Make(typeLine: typeLine));
        Assert.True(card.Types.HasFlag(expected));
    }

    [Fact]
    public void Artifact_creature_sets_both_flags()
    {
        var card = ScryfallMapper.ToCard(Make(typeLine: "Artifact Creature — Construct"));
        Assert.True(card.Types.HasFlag(CardType.Artifact));
        Assert.True(card.Types.HasFlag(CardType.Creature));
    }

    [Theory]
    [InlineData("Kindred Instant — Elf")]
    [InlineData("Tribal Instant — Goblin")]   // legacy "Tribal" keyword
    public void Kindred_and_Tribal_both_map_to_Kindred_flag(string typeLine)
    {
        var card = ScryfallMapper.ToCard(Make(typeLine: typeLine));
        Assert.True(card.Types.HasFlag(CardType.Kindred));
        Assert.True(card.Types.HasFlag(CardType.Instant));
    }

    // --- supertypes ---------------------------------------------------------

    [Fact]
    public void Legendary_creature_sets_IsLegendary()
    {
        var card = ScryfallMapper.ToCard(Make(typeLine: "Legendary Creature — Dragon"));
        Assert.True(card.IsLegendary);
    }

    [Fact]
    public void Basic_land_sets_IsBasicLand()
    {
        var card = ScryfallMapper.ToCard(Make(typeLine: "Basic Land — Forest"));
        Assert.True(card.IsBasicLand);
        Assert.True(card.Types.HasFlag(CardType.Land));
    }

    // --- CanBeCommander -----------------------------------------------------

    [Fact]
    public void Legendary_creature_with_legal_legality_can_be_commander()
    {
        var card = ScryfallMapper.ToCard(
            Make(typeLine: "Legendary Creature — Human Wizard", legality: "legal"));
        Assert.True(card.CanBeCommander);
    }

    [Fact]
    public void Non_legendary_creature_cannot_be_commander()
    {
        var card = ScryfallMapper.ToCard(
            Make(typeLine: "Creature — Human", legality: "legal"));
        Assert.False(card.CanBeCommander);
    }

    [Fact]
    public void Legendary_creature_that_is_banned_cannot_be_commander()
    {
        var card = ScryfallMapper.ToCard(
            Make(typeLine: "Legendary Creature — Eldrazi", legality: "banned"));
        Assert.False(card.CanBeCommander);
    }

    [Fact]
    public void Legendary_planeswalker_without_oracle_text_cannot_be_commander()
    {
        var card = ScryfallMapper.ToCard(
            Make(typeLine: "Legendary Planeswalker — Jace", legality: "legal", oracleText: ""));
        Assert.False(card.CanBeCommander);
    }

    [Fact]
    public void Legendary_planeswalker_with_can_be_your_commander_text_can_be_commander()
    {
        // e.g. Teferi, Temporal Archmage — has explicit "can be your commander" oracle text
        var card = ScryfallMapper.ToCard(Make(
            typeLine: "Legendary Planeswalker — Teferi",
            legality: "legal",
            oracleText: "You may activate loyalty abilities of Teferi, Temporal Archmage on any player's turn. Teferi, Temporal Archmage can be your commander."));
        Assert.True(card.CanBeCommander);
    }

    [Fact]
    public void Card_with_can_be_your_commander_text_but_banned_cannot_be_commander()
    {
        var card = ScryfallMapper.ToCard(Make(
            typeLine: "Legendary Planeswalker — Teferi",
            legality: "banned",
            oracleText: "Teferi, Temporal Archmage can be your commander."));
        Assert.False(card.CanBeCommander);
    }

    [Fact]
    public void DFC_with_can_be_your_commander_on_front_face_oracle_text_can_be_commander()
    {
        // Some DFCs carry the text on the front face; oracle text falls back to face0
        var dto = Make(
            typeLine: "Legendary Planeswalker // Land",
            legality: "legal",
            oracleText: null,   // top-level null — falls back to face
            faces:
            [
                new ScryfallCardFace
                {
                    TypeLine   = "Legendary Planeswalker — Ugin",
                    OracleText = "Ugin, the Spirit Dragon can be your commander.",
                },
                new ScryfallCardFace { TypeLine = "Land" },
            ]);
        var card = ScryfallMapper.ToCard(dto);
        Assert.True(card.CanBeCommander);
    }

    // --- legality mapping ---------------------------------------------------

    [Theory]
    [InlineData("legal",      Legality.Legal)]
    [InlineData("banned",     Legality.Banned)]
    [InlineData("restricted", Legality.Restricted)]
    [InlineData("not_legal",  Legality.NotLegal)]
    [InlineData("unknown",    Legality.NotLegal)]  // unknown values fall back to NotLegal
    public void Legality_string_maps_correctly(string raw, Legality expected)
    {
        var card = ScryfallMapper.ToCard(Make(legality: raw));
        Assert.Equal(expected, card.CommanderLegality);
    }

    // --- double-faced cards (DFCs) ------------------------------------------

    [Fact]
    public void DFC_uses_front_face_type_line_for_types()
    {
        var dto = Make(
            typeLine: "Creature // Sorcery",
            faces:
            [
                new ScryfallCardFace { TypeLine = "Creature — Human Wizard", ManaCost = "{U}" },
                new ScryfallCardFace { TypeLine = "Sorcery" },
            ]);

        var card = ScryfallMapper.ToCard(dto);
        Assert.True(card.Types.HasFlag(CardType.Creature));
        Assert.False(card.Types.HasFlag(CardType.Sorcery));
    }

    [Fact]
    public void DFC_falls_back_to_face_mana_cost_when_top_level_is_null()
    {
        var dto = Make(
            manaCost: null,
            faces: [new ScryfallCardFace { ManaCost = "{2}{U}", TypeLine = "Creature — Human" }]);

        var card = ScryfallMapper.ToCard(dto);
        Assert.Equal("{2}{U}", card.ManaCost);
    }

    [Fact]
    public void DFC_falls_back_to_face_images_when_top_level_has_none()
    {
        var faceImages = new ScryfallImageUris
            { Small = "s", Normal = "n", Large = "l", ArtCrop = "a" };
        var dto = Make(
            images: null,
            faces: [new ScryfallCardFace { TypeLine = "Creature", ImageUris = faceImages }]);

        var card = ScryfallMapper.ToCard(dto);
        Assert.NotNull(card.Images);
        Assert.Equal("n", card.Images!.Normal);
    }

    [Fact]
    public void DFC_legendary_creature_front_face_can_be_commander()
    {
        var dto = Make(
            typeLine: "Legendary Creature // Legendary Artifact",
            faces:
            [
                new ScryfallCardFace { TypeLine = "Legendary Creature — Dragon Elder" },
                new ScryfallCardFace { TypeLine = "Legendary Artifact" },
            ]);

        var card = ScryfallMapper.ToCard(dto);
        Assert.True(card.CanBeCommander);
    }

    // --- MDFC back face text ------------------------------------------------

    [Fact]
    public void MDFC_with_land_back_populates_BackFaceText()
    {
        var dto = Make(
            typeLine: "Sorcery // Land",
            oracleText: null,
            faces:
            [
                new ScryfallCardFace { TypeLine = "Sorcery", OracleText = "Return X creatures from your graveyard." },
                new ScryfallCardFace { TypeLine = "Land — Swamp", OracleText = "({T}: Add {B}.)" },
            ]);

        var card = ScryfallMapper.ToCard(dto);
        Assert.Equal("({T}: Add {B}.)", card.BackFaceText);
    }

    [Fact]
    public void MDFC_without_land_back_populates_BackFaceText()
    {
        var dto = Make(
            typeLine: "Creature // Sorcery",
            faces:
            [
                new ScryfallCardFace { TypeLine = "Creature — Human Wizard", OracleText = "Flying." },
                new ScryfallCardFace { TypeLine = "Sorcery", OracleText = "Deal 3 damage." },
            ]);

        var card = ScryfallMapper.ToCard(dto);
        Assert.Equal("Deal 3 damage.", card.BackFaceText);
    }

    [Fact]
    public void MDFC_populates_BackFaceTypeLine()
    {
        var dto = Make(
            typeLine: "Instant // Land",
            faces:
            [
                new ScryfallCardFace { TypeLine = "Instant", OracleText = "Counter target spell." },
                new ScryfallCardFace { TypeLine = "Land — Island", OracleText = "({T}: Add {U}.)" },
            ]);

        var card = ScryfallMapper.ToCard(dto);
        Assert.Equal("Land — Island", card.BackFaceTypeLine);
        Assert.Equal("({T}: Add {U}.)", card.BackFaceText);
    }

    [Fact]
    public void Single_face_card_leaves_BackFaceText_null()
    {
        var card = ScryfallMapper.ToCard(Make(typeLine: "Instant", oracleText: "Counter target spell."));
        Assert.Null(card.BackFaceText);
        Assert.Null(card.BackFaceTypeLine);
    }

    // --- color identity -----------------------------------------------------

    [Fact]
    public void Color_identity_is_taken_from_Scryfall_array()
    {
        var card = ScryfallMapper.ToCard(Make(colors: ["W", "U"]));
        Assert.Equal(Color.White | Color.Blue, card.ColorIdentity);
    }

    [Fact]
    public void Colorless_card_has_None_identity()
    {
        var card = ScryfallMapper.ToCard(Make(colors: []));
        Assert.Equal(Color.None, card.ColorIdentity);
    }
}
