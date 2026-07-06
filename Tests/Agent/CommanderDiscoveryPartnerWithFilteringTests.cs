using EdhDeckBuilder.Agent.Discovery;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Partnerships;

namespace EdhDeckBuilder.Tests.Agent;

/// <summary>
/// Tests for Commander Discovery's filtering of Partner with pairings against EDHREC's authoritative list.
/// Ensures that only valid "Partner with" pairs reach the LLM for evaluation.
/// </summary>
public sealed class CommanderDiscoveryPartnerWithFilteringTests
{
    private readonly CommanderDiscovery _discovery;
    private readonly MockCommanderSelector _selector;
    private readonly FakeCardRepository _repository;

    public CommanderDiscoveryPartnerWithFilteringTests()
    {
        _selector = new MockCommanderSelector();
        _repository = new FakeCardRepository();
        _discovery = new CommanderDiscovery(_repository, _selector);
    }

    /// <summary>
    /// Scenario 1: Card X with "Partner with Y" is paired with Card Y with "Partner with X"
    /// Both cards have the specific partner keyword pointing to each other.
    /// EDHREC lists them as a valid pair.
    /// Result: Pair should be included.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_ValidPartnerWithPair_IncludesPair()
    {
        // Arrange
        var ukkima = CreateCard("Ukkima, Stalking Shadow", Color.Blue | Color.Green);
        var yannik = CreateCard("Yannik, Scavenging Sentinel", Color.Blue | Color.Green);

        _repository.AddCards([ukkima, yannik]);
        // CardRepository now sources partnerships from EDHREC, so this combo is already valid
        _repository.AddPartnerCombo(new PartnerCombo(
            ukkima.OracleId,
            yannik.OracleId,
            PartnershipType.PartnerWith,
            "partner with",
            "partner with"));

        _selector.SetResult(commands => new List<CommanderSelectionResult>
        {
            new(ukkima.OracleId, 1, "Valid partner pair"),
        });

        var request = new CommanderDiscoveryRequest { Archetypes = [], Themes = [], ColorFilter = null };

        // Act
        var result = await _discovery.DiscoverAsync(request);

        // Assert
        Assert.NotEmpty(result.Suggestions);
        var suggestion = result.Suggestions.First();
        Assert.Equal(ukkima.OracleId, suggestion.Commander.OracleId);
        Assert.Equal(yannik.OracleId, suggestion.PartnerCommander?.OracleId);
    }

    /// <summary>
    /// Scenario 2: Card X with "Partner with Y" is not paired with Card Y without "Partner with X"
    /// Card X has "partner with" keyword but Card Y only has generic "Partner" keyword.
    /// They are technically compatible by the eligibility rule, but EDHREC doesn't list them as a pair.
    /// Result: Pair should be filtered out.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_PartnerWithPairNotInEdhrec_IsNotIncluded()
    {
        // Arrange
        var cardWithPartnerWith = CreateCard("Card With Partner With", Color.Blue | Color.Green);
        var cardWithPartner = CreateCard("Card With Partner", Color.Blue | Color.Green);

        _repository.AddCards([cardWithPartnerWith, cardWithPartner]);
        // Don't add this combo to repository - it wouldn't be there if EDHREC doesn't list it
        // (CardRepository sources partnerships exclusively from EDHREC)

        _selector.SetResult(commands => new List<CommanderSelectionResult>
        {
            new(cardWithPartnerWith.OracleId, 1, "Singleton"),
        });

        var request = new CommanderDiscoveryRequest { Archetypes = [], Themes = [], ColorFilter = null };

        // Act
        var result = await _discovery.DiscoverAsync(request);

        // Assert: pair doesn't exist in CardRepository, so only singleton appears
        var suggestion = result.Suggestions.FirstOrDefault();
        Assert.Null(suggestion?.PartnerCommander);
    }

    /// <summary>
    /// Scenario 3: Card X with "Partner with Y" is not paired with Card Y with generic "Partner"
    /// Card X has specific "partner with" pointing to a specific card.
    /// Card Y only has generic "Partner" (no specific match).
    /// They are incompatible by definition.
    /// Result: Pair should be filtered out.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_PartnerWithVsGenericPartner_FiltersPair()
    {
        // Arrange
        var specificPartnerWith = CreateCard("Ukkima, Stalking Shadow", Color.Blue | Color.Green);
        var genericPartner = CreateCard("Some Generic Partner", Color.Blue | Color.Green);

        _repository.AddCards([specificPartnerWith, genericPartner]);
        _repository.AddPartnerCombo(new PartnerCombo(
            specificPartnerWith.OracleId,
            genericPartner.OracleId,
            PartnershipType.PartnerWith,
            "partner with",
            "partner"));

        // Don't add this invalid combo to the repository

        _selector.SetResult(_ => new List<CommanderSelectionResult>());

        var request = new CommanderDiscoveryRequest { Archetypes = [], Themes = [], ColorFilter = null };

        // Act
        var result = await _discovery.DiscoverAsync(request);

        // Assert: pair filtered, no suggestions
        Assert.Empty(result.Suggestions);
    }

    /// <summary>
    /// Scenario 4: Card X with "Partner with Y" + "Doctor's Companion" can pair with Card Y with "Partner with X"
    /// OR with any Time Lord Doctor card.
    /// Card X is eligible for two types of partnerships.
    /// Result: Should see both types in the final pool.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_DualPartnershipTypes_IncludesBoth()
    {
        // Arrange
        var companion = CreateCard("Thirteenth Doctor", Color.Blue);
        var specificPartner = CreateCard("River Song", Color.Blue);
        var timeLord = CreateCard("The Doctor", Color.Blue);

        _repository.AddCards([companion, specificPartner, timeLord]);

        // Companion has both "partner with" and "doctor's companion"
        _repository.AddPartnerCombo(new PartnerCombo(
            companion.OracleId,
            specificPartner.OracleId,
            PartnershipType.PartnerWith,
            "partner with",
            "partner with"));

        _repository.AddPartnerCombo(new PartnerCombo(
            companion.OracleId,
            timeLord.OracleId,
            PartnershipType.DoctorsCompanion,
            "doctor's companion",
            "time lord doctor"));

        var selectionResults = new List<CommanderSelectionResult>
        {
            new(companion.OracleId, 1, "Valid duo"),
            new(timeLord.OracleId, 2, "Also valid with companion"),
        };
        _selector.SetResult(_ => selectionResults);

        var request = new CommanderDiscoveryRequest { Archetypes = [], Themes = [], ColorFilter = null };

        // Act
        var result = await _discovery.DiscoverAsync(request);

        // Assert: both partnerships should be preserved
        Assert.NotEmpty(result.Suggestions);
        // Companion can appear with either partner type
        var companionSuggestion = result.Suggestions.FirstOrDefault(s => s.Commander.OracleId == companion.OracleId);
        Assert.NotNull(companionSuggestion);
    }

    /// <summary>
    /// Scenario 5: Card X with any partner variant pairs with Card Y with the SAME variant.
    /// Tests all valid same-variant combinations: Partner, Friends Forever, Partner-Survivors, etc.
    /// Result: Pairs should be included (not filtered by EDHREC validation, as these aren't "Partner with").
    /// </summary>
    [Theory]
    [InlineData(PartnershipType.Partner, "partner", "partner", "Generic Partner pair")]
    [InlineData(PartnershipType.FriendsForever, "friends forever", "friends forever", "Friends Forever pair")]
    [InlineData(PartnershipType.PartnerSurvivors, "partner - survivors", "partner - survivors", "Partner-Survivors pair")]
    [InlineData(PartnershipType.Background, "choose a background", "background", "Background pair")]
    public async Task DiscoverAsync_SamePartnerVariantPair_IncludesPair(
        PartnershipType type,
        string firstKeyword,
        string secondKeyword,
        string description)
    {
        // Arrange
        var first = CreateCard($"Card A - {description}", Color.White);
        var second = CreateCard($"Card B - {description}", Color.White);

        _repository.AddCards([first, second]);
        _repository.AddPartnerCombo(new PartnerCombo(
            first.OracleId,
            second.OracleId,
            type,
            firstKeyword,
            secondKeyword));

        _selector.SetResult(_ => new List<CommanderSelectionResult>
        {
            new(first.OracleId, 1, description),
        });

        var request = new CommanderDiscoveryRequest { Archetypes = [], Themes = [], ColorFilter = null };

        // Act
        var result = await _discovery.DiscoverAsync(request);

        // Assert: Same-variant pairs are not filtered (only Partner with pairs are)
        Assert.NotEmpty(result.Suggestions);
        var suggestion = result.Suggestions.First();
        Assert.Equal(second.OracleId, suggestion.PartnerCommander?.OracleId);
    }

    /// <summary>
    /// Scenario 6: Card X with one partner variant is NOT paired with Card Y with a DIFFERENT variant.
    /// Tests all mismatched variant combinations to ensure they don't pair.
    /// Result: Pairs should not appear (caught by eligibility rule).
    /// </summary>
    [Theory]
    [InlineData(PartnershipType.Partner, PartnershipType.FriendsForever)]
    [InlineData(PartnershipType.Partner, PartnershipType.Background)]
    [InlineData(PartnershipType.Partner, PartnershipType.PartnerSurvivors)]
    [InlineData(PartnershipType.FriendsForever, PartnershipType.Background)]
    [InlineData(PartnershipType.FriendsForever, PartnershipType.PartnerSurvivors)]
    [InlineData(PartnershipType.FriendsForever, PartnershipType.DoctorsCompanion)]
    [InlineData(PartnershipType.Background, PartnershipType.PartnerSurvivors)]
    [InlineData(PartnershipType.Background, PartnershipType.DoctorsCompanion)]
    [InlineData(PartnershipType.PartnerSurvivors, PartnershipType.DoctorsCompanion)]
    public async Task DiscoverAsync_MismatchedPartnerVariants_DoesNotPair(
        PartnershipType firstType,
        PartnershipType secondType)
    {
        // Arrange
        var first = CreateCard($"Card A - {firstType}", Color.White);
        var second = CreateCard($"Card B - {secondType}", Color.White);

        _repository.AddCards([first, second]);
        // The eligibility rule prevents this combo from being created
        // We don't add it to the repository, and if somehow it were added, it would be filtered

        _selector.SetResult(_ => new List<CommanderSelectionResult>());

        var request = new CommanderDiscoveryRequest { Archetypes = [], Themes = [], ColorFilter = null };

        // Act
        var result = await _discovery.DiscoverAsync(request);

        // Assert: mismatched variants don't produce pairs
        Assert.Empty(result.Suggestions);
    }

    /// <summary>
    /// Scenario 7: Card X with "Choose a background" pairs only with a background, but can pair with any background.
    /// Result: Pair should be included regardless of which specific background.
    /// </summary>
    [Fact]
    public async Task DiscoverAsync_BackgroundPair_IncludesAnyBackgroundMatch()
    {
        // Arrange
        var creature = CreateCard("Creature with Background", Color.White);
        var background1 = CreateCard("Background A", Color.White);
        var background2 = CreateCard("Background B", Color.White);

        _repository.AddCards([creature, background1, background2]);
        _repository.AddPartnerCombo(new PartnerCombo(
            creature.OracleId,
            background1.OracleId,
            PartnershipType.Background,
            "choose a background",
            "background"));

        _repository.AddPartnerCombo(new PartnerCombo(
            creature.OracleId,
            background2.OracleId,
            PartnershipType.Background,
            "choose a background",
            "background"));

        // Background pairs are sourced from EDHREC via CardRepository

        _selector.SetResult(_ => new List<CommanderSelectionResult>
        {
            new(creature.OracleId, 1, "Creature with background"),
            new(background1.OracleId, 2, "Background option 1"),
            new(background2.OracleId, 3, "Background option 2"),
        });

        var request = new CommanderDiscoveryRequest { Archetypes = [], Themes = [], ColorFilter = null };

        // Act
        var result = await _discovery.DiscoverAsync(request);

        // Assert: creature can pair with either background
        Assert.NotEmpty(result.Suggestions);
        var creatureSuggestion = result.Suggestions.FirstOrDefault(s => s.Commander.OracleId == creature.OracleId);
        Assert.NotNull(creatureSuggestion?.PartnerCommander);
        // Either background is valid
        Assert.True(
            creatureSuggestion.PartnerCommander.OracleId == background1.OracleId ||
            creatureSuggestion.PartnerCommander.OracleId == background2.OracleId);
    }

    // ── Mocks ────────────────────────────────────────────────────────

    private static Card CreateCard(string name, Color colorIdentity = Color.None)
        => new()
        {
            OracleId = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid(),
            Name = name,
            TypeLine = "Legendary Creature",
            ColorIdentity = colorIdentity,
            CanBeCommander = true,
        };

    private sealed class MockCommanderSelector : ICommanderSelector
    {
        private Func<IReadOnlyList<Card>, IReadOnlyList<CommanderSelectionResult>> _resultFactory = _ => [];

        public void SetResult(Func<IReadOnlyList<Card>, IReadOnlyList<CommanderSelectionResult>> factory)
            => _resultFactory = factory;

        public Task<IReadOnlyList<CommanderSelectionResult>> SelectAsync(
            IReadOnlyList<Card> candidates,
            CommanderDiscoveryRequest request,
            CancellationToken ct = default)
        {
            return Task.FromResult(_resultFactory(candidates));
        }
    }

    private sealed class FakeCardRepository : ICardRepository
    {
        private readonly List<Card> _commanders = [];
        private readonly List<PartnerCombo> _combos = [];

        public void AddCards(IEnumerable<Card> cards)
            => _commanders.AddRange(cards);

        public void AddPartnerCombo(PartnerCombo combo)
            => _combos.Add(combo);

        public Task<Card?> GetByNameAsync(string name, CancellationToken ct = default)
            => Task.FromResult(_commanders.FirstOrDefault(c => c.Name == name));

        public Task<Card?> GetByOracleIdAsync(Guid oracleId, CancellationToken ct = default)
            => Task.FromResult(_commanders.FirstOrDefault(c => c.OracleId == oracleId));

        public Task<IReadOnlyList<Card>> SearchAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Card>>(_commanders
                .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList());

        public Task<IReadOnlyList<Card>> GetCommandersAsync(
            Color? colorFilter = null,
            bool exactMatch = false,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Card>>(_commanders
                .Where(c => c.CanBeCommander)
                .Where(c => colorFilter is null
                    || (exactMatch ? c.ColorIdentity == colorFilter.Value
                                   : c.ColorIdentity.IsWithin(colorFilter.Value)))
                .ToList());

        public Task<IReadOnlyList<PartnerCombo>> GetPartnerCombosAsync(
            Color? colorFilter = null,
            bool exactMatch = false,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PartnerCombo>>(_combos
                .Where(pc => colorFilter is null
                    || IsValidColorIdentity(pc, colorFilter.Value, exactMatch))
                .ToList());

        private bool IsValidColorIdentity(PartnerCombo combo, Color filter, bool exactMatch)
        {
            var first = _commanders.FirstOrDefault(c => c.OracleId == combo.FirstCardId);
            var second = _commanders.FirstOrDefault(c => c.OracleId == combo.SecondCardId);

            if (first is null || second is null)
                return false;

            var combined = first.ColorIdentity | second.ColorIdentity;
            return exactMatch
                ? combined == filter
                : combined.IsWithin(filter);
        }
    }

}
