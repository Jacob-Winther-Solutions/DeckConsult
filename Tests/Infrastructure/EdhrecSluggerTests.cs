using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Infrastructure.Edhrec;

namespace EdhDeckBuilder.Tests.Infrastructure;

public sealed class EdhrecSluggerTests
{
    [Theory]
    [InlineData("Edgar Markov",              "edgar-markov")]
    [InlineData("Atraxa, Praetors' Voice",   "atraxa-praetors-voice")]
    [InlineData("Yuriko, the Tiger's Shadow","yuriko-the-tigers-shadow")]
    [InlineData("Zur the Enchanter",         "zur-the-enchanter")]
    [InlineData("Ob Nixilis, the Adversary", "ob-nixilis-the-adversary")]
    [InlineData("Sharuum the Hegemon",       "sharuum-the-hegemon")]
    [InlineData("Korvold, Fae-Cursed King",  "korvold-fae-cursed-king")]
    public void Known_card_names_produce_correct_slugs(string name, string expectedSlug)
        => Assert.Equal(expectedSlug, EdhrecSlugger.ToSlug(name));

    [Fact]
    public void DFC_name_uses_front_face_only()
    {
        var commander = MakeDfc("Erinis, Gloom Stalker // Street Urchin");
        Assert.Equal("erinis-gloom-stalker", EdhrecSlugger.FromCard(commander));
    }

    [Fact]
    public void Non_DFC_name_is_slugged_directly()
    {
        var commander = MakeCard("Edgar Markov");
        Assert.Equal("edgar-markov", EdhrecSlugger.FromCard(commander));
    }

    [Fact]
    public void Consecutive_punctuation_does_not_produce_double_hyphen()
    {
        // e.g. a comma immediately followed by a space: "Name, Title" → "name-title" not "name--title"
        Assert.DoesNotContain("--", EdhrecSlugger.ToSlug("Atraxa, Praetors' Voice"));
    }

    // --- helpers ------------------------------------------------------------

    private static Card MakeCard(string name) => new()
    {
        ScryfallId  = Guid.NewGuid(),
        OracleId    = Guid.NewGuid(),
        Name        = name,
        TypeLine    = "Legendary Creature",
    };

    private static Card MakeDfc(string name) => MakeCard(name);
}
