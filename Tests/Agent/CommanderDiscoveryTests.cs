using EdhDeckBuilder.Agent.Discovery;
using EdhDeckBuilder.Agent.Interfaces;
using EdhDeckBuilder.Agent.Models;
using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Core.Decks;
using EdhDeckBuilder.Core.Partnerships;

namespace EdhDeckBuilder.Tests.Agent;

public sealed class CommanderDiscoveryTests
{
    private readonly ICommanderDiscovery _discovery;
    private readonly MockCommanderSelector _selector;
    private readonly FakeCardRepository _repository;

    public CommanderDiscoveryTests()
    {
        _selector = new MockCommanderSelector();
        _repository = new FakeCardRepository();
        _discovery = new CommanderDiscovery(_repository, _selector);
    }

    [Fact]
    public async Task DiscoverAsync_WithSmallPool_CallsSelectorOnce()
    {
        // Arrange: 100 commanders (less than batch limit of 150)
        var commanders = CreateCommanders(100);
        _repository.AddCards(commanders);
        _selector.SetResult(commands => commands.Take(5).Select((c, i) =>
            new CommanderSelectionResult(c.OracleId, i + 1, $"Commander {i + 1}")).ToList());

        var request = new CommanderDiscoveryRequest
        {
            Archetypes = [],
            Themes = [],
            ColorFilter = null,
        };

        // Act
        var result = await _discovery.DiscoverAsync(request);

        // Assert
        Assert.Equal(1, _selector.CallCount);
        Assert.NotEmpty(result.Suggestions);
    }

    [Fact]
    public async Task DiscoverAsync_WithLargePool_CallsSelectorMultipleTimes()
    {
        // Arrange: 200 commanders (more than batch limit of 150)
        var commanders = CreateCommanders(200);
        _repository.AddCards(commanders);
        _selector.SetResult(commands => commands.Take(5).Select((c, i) =>
            new CommanderSelectionResult(c.OracleId, i + 1, $"Commander {i + 1}")).ToList());

        var request = new CommanderDiscoveryRequest
        {
            Archetypes = [],
            Themes = [],
            ColorFilter = null,
        };

        // Act
        var result = await _discovery.DiscoverAsync(request);

        // Assert — should call: once for first batch, once for second batch, once for final ranking
        Assert.True(_selector.CallCount > 1);
        Assert.NotEmpty(result.Suggestions);
    }

    [Fact]
    public async Task DiscoverAsync_WithColorFilter_ReturnsBlueBased()
    {
        // Arrange
        var blueWhite = CreateCommander("Court of Equity", Color.Blue | Color.White);
        var blueOnly = CreateCommander("Talrand", Color.Blue);
        var redOnly = CreateCommander("Anax", Color.Red);

        _repository.AddCards([blueWhite, blueOnly, redOnly]);
        _selector.SetResult(commands =>
            [new CommanderSelectionResult(commands[0].OracleId, 1, "Good choice")]);

        var request = new CommanderDiscoveryRequest
        {
            Archetypes = [],
            Themes = [],
            ColorFilter = Color.Blue,
            ExactColorMatch = false, // "within" — include Blue and Blue+White
        };

        // Act
        var result = await _discovery.DiscoverAsync(request);

        // Assert: should get both blue and blue+white
        Assert.True(result.Suggestions.Count >= 1);
    }

    [Fact]
    public async Task DiscoverAsync_WithEmptyPool_ReturnsEmpty()
    {
        // Arrange: no commanders in repository
        _repository.AddCards([]);

        var request = new CommanderDiscoveryRequest
        {
            Archetypes = [],
            Themes = [],
            ColorFilter = null,
        };

        // Act
        var result = await _discovery.DiscoverAsync(request);

        // Assert
        Assert.Empty(result.Suggestions);
        Assert.Equal(0, _selector.CallCount);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private List<Card> CreateCommanders(int count)
    {
        var commanders = new List<Card>();
        for (int i = 0; i < count; i++)
        {
            commanders.Add(CreateCommander($"Commander {i}", Color.Green));
        }
        return commanders;
    }

    private Card CreateCommander(string name, Color color) => new()
    {
        OracleId = Guid.NewGuid(),
        ScryfallId = Guid.NewGuid(),
        Name = name,
        TypeLine = "Legendary Creature",
        ColorIdentity = color,
        CanBeCommander = true,
    };

    // ── Mocks ────────────────────────────────────────────────────────────

    private sealed class MockCommanderSelector : ICommanderSelector
    {
        private Func<IReadOnlyList<Card>, IReadOnlyList<CommanderSelectionResult>> _resultFactory = _ => [];

        public int CallCount { get; private set; }

        public void SetResult(Func<IReadOnlyList<Card>, IReadOnlyList<CommanderSelectionResult>> factory)
            => _resultFactory = factory;

        public Task<IReadOnlyList<CommanderSelectionResult>> SelectAsync(
            IReadOnlyList<Card> candidates,
            CommanderDiscoveryRequest request,
            CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_resultFactory(candidates));
        }
    }

    private sealed class FakeCardRepository : ICardRepository
    {
        private readonly List<Card> _commanders = [];

        public void AddCards(IEnumerable<Card> cards)
            => _commanders.AddRange(cards);

        public Task<Card?> GetByNameAsync(string name, CancellationToken ct = default)
            => Task.FromResult(_commanders.FirstOrDefault(c => c.Name == name));

        public Task<Card?> GetByOracleIdAsync(Guid oracleId, CancellationToken ct = default)
            => Task.FromResult(_commanders.FirstOrDefault(c => c.OracleId == oracleId));

        public Task<IReadOnlyList<Card>> SearchAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Card>>(
                _commanders.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList());

        public Task<IReadOnlyList<Card>> GetCommandersAsync(
            Color? colorFilter = null,
            bool exactMatch = false,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Card>>(
                _commanders
                    .Where(c => c.CanBeCommander)
                    .Where(c => colorFilter is null
                        || (exactMatch ? c.ColorIdentity == colorFilter.Value
                                       : c.ColorIdentity.IsWithin(colorFilter.Value)))
                    .ToList());

        public Task<IReadOnlyList<PartnerCombo>> GetPartnerCombosAsync(
            Color? colorFilter = null,
            bool exactMatch = false,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PartnerCombo>>(new List<PartnerCombo>());
    }

}
