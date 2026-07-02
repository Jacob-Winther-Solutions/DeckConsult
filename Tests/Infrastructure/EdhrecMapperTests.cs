using EdhDeckBuilder.Core.Abstractions;
using EdhDeckBuilder.Core.Cards;
using EdhDeckBuilder.Infrastructure.Edhrec;
using EdhDeckBuilder.Infrastructure.Edhrec.Dto;
using Microsoft.Extensions.Logging.Abstractions;

namespace EdhDeckBuilder.Tests.Infrastructure;

public sealed class EdhrecMapperTests
{
    // --- helpers ------------------------------------------------------------

    private static Card MakeCard(string name) => new()
    {
        ScryfallId = Guid.NewGuid(),
        OracleId   = Guid.NewGuid(),
        Name       = name,
        TypeLine   = "Sorcery",
    };

    private static EdhrecCardlist MakeList(string header, params (string name, int num, int potential)[] views) =>
        new()
        {
            Header    = header,
            Cardviews = views.Select(v => new EdhrecCardView
            {
                Name           = v.name,
                NumDecks       = v.num,
                PotentialDecks = v.potential,
            }).ToList(),
        };

    private static FakeCardRepository RepoWith(params Card[] cards) => new(cards);

    // --- basic resolution ---------------------------------------------------

    [Fact]
    public async Task Cards_are_resolved_from_repository()
    {
        var solRing = MakeCard("Sol Ring");
        var list    = MakeList("Top Cards", ("Sol Ring", 1000, 2000));
        var repo    = RepoWith(solRing);

        var result = await EdhrecMapper.ToCardCandidatesAsync([list], repo, NullLogger.Instance, default);

        Assert.Single(result);
        Assert.Equal("Sol Ring", result[0].Card.Name);
    }

    [Fact]
    public async Task Cards_not_in_repository_are_skipped()
    {
        var list   = MakeList("Top Cards", ("Unknown Card", 1000, 2000));
        var repo   = RepoWith(); // empty

        var result = await EdhrecMapper.ToCardCandidatesAsync([list], repo, NullLogger.Instance, default);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Empty_cardlists_return_empty_result()
    {
        var result = await EdhrecMapper.ToCardCandidatesAsync([], RepoWith(), NullLogger.Instance, default);
        Assert.Empty(result);
    }

    // --- inclusion calculation ----------------------------------------------

    [Fact]
    public async Task Inclusion_is_num_decks_over_potential_decks()
    {
        var card = MakeCard("Sol Ring");
        var list = MakeList("Top Cards", ("Sol Ring", 500, 1000));

        var result = await EdhrecMapper.ToCardCandidatesAsync([list], RepoWith(card), NullLogger.Instance, default);

        Assert.Equal(0.5, result[0].Inclusion, precision: 5);
    }

    [Fact]
    public async Task Cards_ordered_by_descending_inclusion()
    {
        var high = MakeCard("High Card");
        var low  = MakeCard("Low Card");
        var list = MakeList("Top Cards",
            ("Low Card",  100, 1000),   // 10%
            ("High Card", 800, 1000));  // 80%

        var result = await EdhrecMapper.ToCardCandidatesAsync([list], RepoWith(high, low), NullLogger.Instance, default);

        Assert.Equal("High Card", result[0].Card.Name);
        Assert.Equal("Low Card",  result[1].Card.Name);
    }

    // --- deduplication across sections -------------------------------------

    [Fact]
    public async Task Card_appearing_in_two_sections_is_returned_once()
    {
        var card   = MakeCard("Sol Ring");
        var list1  = MakeList("High Synergy Cards", ("Sol Ring", 500, 1000));
        var list2  = MakeList("Top Cards",          ("Sol Ring", 800, 1000));

        var result = await EdhrecMapper.ToCardCandidatesAsync([list1, list2], RepoWith(card), NullLogger.Instance, default);

        Assert.Single(result);
    }

    [Fact]
    public async Task Deduplication_keeps_highest_inclusion_entry()
    {
        var card   = MakeCard("Sol Ring");
        var list1  = MakeList("High Synergy Cards", ("Sol Ring", 500, 1000)); // 50%
        var list2  = MakeList("Top Cards",          ("Sol Ring", 800, 1000)); // 80%

        var result = await EdhrecMapper.ToCardCandidatesAsync([list1, list2], RepoWith(card), NullLogger.Instance, default);

        Assert.Equal(0.8, result[0].Inclusion, precision: 5);
    }

    [Fact]
    public async Task Section_comes_from_the_highest_inclusion_occurrence()
    {
        var card   = MakeCard("Sol Ring");
        var list1  = MakeList("High Synergy Cards", ("Sol Ring", 500, 1000)); // 50%
        var list2  = MakeList("Spellslinger Cards", ("Sol Ring", 800, 1000)); // 80% — should win

        var result = await EdhrecMapper.ToCardCandidatesAsync([list1, list2], RepoWith(card), NullLogger.Instance, default);

        Assert.Equal("Spellslinger Cards", result[0].Section);
    }
}

// ---------------------------------------------------------------------------

/// <summary>Simple in-memory ICardRepository for use in tests.</summary>
internal sealed class FakeCardRepository(params Card[] cards) : ICardRepository
{
    private readonly Dictionary<string, Card> _byName =
        cards.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<Guid, Card> _byOracleId =
        cards.ToDictionary(c => c.OracleId);

    public Task<Card?> GetByNameAsync(string name, CancellationToken ct = default)
        => Task.FromResult(_byName.GetValueOrDefault(name.Trim()));

    public Task<Card?> GetByOracleIdAsync(Guid oracleId, CancellationToken ct = default)
        => Task.FromResult(_byOracleId.GetValueOrDefault(oracleId));

    public Task<IReadOnlyList<Card>> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Card>>(
            _byName.Values.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList());

    public Task<IReadOnlyList<Card>> GetCommandersAsync(
        Color? colorFilter = null,
        bool exactMatch = false,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Card>>(
            _byOracleId.Values
                .Where(c => c.CanBeCommander)
                .Where(c => colorFilter is null
                    || (exactMatch ? c.ColorIdentity == colorFilter.Value
                                   : c.ColorIdentity.IsWithin(colorFilter.Value)))
                .ToList());
}
