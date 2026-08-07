using EdhDeckBuilder.Web.Services;

namespace EdhDeckBuilder.Tests.Web;

public sealed class CreatureTypeCatalogTests
{
    // ── Pluralize ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Dragon",   "Dragons")]
    [InlineData("Elf",      "Elves")]
    [InlineData("Dwarf",    "Dwarves")]
    [InlineData("Wolf",     "Wolves")]
    [InlineData("Goblin",   "Goblins")]
    [InlineData("Zombie",   "Zombies")]
    [InlineData("Vampire",  "Vampires")]
    [InlineData("Warrior",  "Warriors")]
    [InlineData("Wizard",   "Wizards")]
    [InlineData("Knight",   "Knights")]
    [InlineData("Merfolk",  "Merfolk")]  // unchanged irregular
    [InlineData("Sheep",    "Sheep")]    // unchanged irregular
    [InlineData("Mouse",    "Mice")]
    [InlineData("Ox",       "Oxen")]
    [InlineData("Harpy",    "Harpies")] // consonant+y → ies
    [InlineData("Faery",    "Faeries")] // consonant+y → ies
    // The two types the user reported as broken
    [InlineData("Monk",     "Monks")]
    [InlineData("Ally",     "Allies")]
    public void Pluralize_returns_expected_form(string singular, string expected)
        => Assert.Equal(expected, CreatureTypeCatalog.Pluralize(singular));

    // ── Tribe slug matching ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Monk",  "monks")]
    [InlineData("Ally",  "allies")]
    [InlineData("Elf",   "elves")]
    [InlineData("Dragon","dragons")]
    public void Tribe_slug_derived_from_scryfall_name_matches_edhrec_slug(string scryfallSingular, string expectedSlug)
    {
        // The slug is: pluralize → ToLowerInvariant → Replace(' ', '-')
        var slug = CreatureTypeCatalog.Pluralize(scryfallSingular).ToLowerInvariant().Replace(' ', '-');
        Assert.Equal(expectedSlug, slug);
    }

    [Theory]
    [InlineData("monks")]
    [InlineData("allies")]
    [InlineData("elves")]
    [InlineData("dragons")]
    public void Tribe_slug_is_present_in_set_built_from_scryfall_names(string slug)
    {
        // Simulate the set construction in the component: pluralize all names, lowercase, replace spaces
        var scryfallNames = new[] { "Monk", "Ally", "Elf", "Dragon", "Wizard", "Goblin" };
        var slugSet = scryfallNames
            .Select(t => CreatureTypeCatalog.Pluralize(t).ToLowerInvariant().Replace(' ', '-'))
            .ToHashSet();

        Assert.Contains(slug, slugSet);
    }

    // ── Fallback list coverage ─────────────────────────────────────────────────

    [Theory]
    [InlineData("monks")]   // reported missing
    [InlineData("allies")]  // reported missing
    [InlineData("elves")]
    [InlineData("dragons")]
    [InlineData("wizards")]
    [InlineData("goblins")]
    [InlineData("vampires")]
    [InlineData("zombies")]
    [InlineData("clerics")]
    [InlineData("shamans")]
    [InlineData("warriors")]
    [InlineData("knights")]
    public void Fallback_list_slug_set_contains_common_tribal_types(string slug)
    {
        var slugSet = CreatureTypeCatalog.FallbackSlugSet;
        Assert.Contains(slug, slugSet);
    }
}
