using MagicAssistant.Data.Model.NavigatorNodes;
using Dapper;
using Microsoft.Data.Sqlite;

namespace MagicAssistant.Tests.DataDb;

/// <summary>
/// The Locations multi-chip path of GetPagedAsync: EXISTS over Deck with an optional node list,
/// collection filters riding inside EXISTS, and the CopyTotals page aggregate.
/// </summary>
[TestFixture]
public class GetPagedLocationsFilterTests
{
    private string _dbPath = "";
    private SqliteCardStore _cardStore = null!;
    private Guid _cardA, _cardB, _cardC, _cardOrphan;
    private int _deck1, _deck2;

    [SetUp]
    public async Task SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test-locations-{Guid.NewGuid()}.db");
        await InitializeDb.EnsureCreatedAsync(_dbPath);
        _cardStore = new SqliteCardStore(_dbPath);

        _cardA = Guid.NewGuid();
        _cardB = Guid.NewGuid();
        _cardC = Guid.NewGuid();
        _cardOrphan = Guid.NewGuid();   // KB-only card — in no node, must never appear in locations browse

        await _cardStore.AddAllAsync(new[]
        {
            Card(_cardA, "Card A"), Card(_cardB, "Card B"),
            Card(_cardC, "Card C"), Card(_cardOrphan, "Orphan"),
        });

        using var conn = new SqliteConnection($"Data Source={_dbPath};Default Timeout=30");
        await conn.OpenAsync();
        _deck1 = (int)await conn.ExecuteScalarAsync<long>(
            "INSERT INTO Collection (Name, ParentId, NodeType) VALUES ('Deck One', NULL, 2) RETURNING Id");
        _deck2 = (int)await conn.ExecuteScalarAsync<long>(
            "INSERT INTO Collection (Name, ParentId, NodeType) VALUES ('Deck Two', NULL, 2) RETURNING Id");

        // deck1: A×2 (owned), B×1 (owned).  deck2: B×3 (borrowed), C×1 (owned).
        await conn.ExecuteAsync("""
            INSERT INTO Deck (CollectionId, CardId, Count, IsOwn) VALUES
                (@d1, @a, 2, 1), (@d1, @b, 1, 1),
                (@d2, @b, 3, 0), (@d2, @c, 1, 1)
            """, new { d1 = _deck1, d2 = _deck2, a = _cardA, b = _cardB, c = _cardC });
    }

    [TearDown]
    public async Task TearDown()
    {
        await _cardStore.DisposeAsync();
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            var file = _dbPath + suffix;
            if (File.Exists(file)) File.Delete(file);
        }
    }

    private static Card Card(Guid id, string name) => new()
    {
        Id = id, Name = name, ManaCost = "{1}", Type = "Instant", SetCode = "m19", Rarity = "Common",
    };

    private static CardListFilter Filter(List<int>? nodeIds) => new()
    {
        Page = 1,
        PageSize = 50,
        LocationNodeIds = nodeIds,
    };

    [Test]
    public async Task TwoNodes_ReturnsUnionWithoutDuplicates_AndLocationNamesPerCard()
    {
        var result = await _cardStore.GetPagedAsync(Filter(new List<int> { _deck1, _deck2 }));

        // B lives in both decks — EXISTS collapses it to one row (a JOIN would have produced two).
        Assert.That(result.Cards.Select(c => c.Id), Is.EquivalentTo(new[] { _cardA, _cardB, _cardC }));
        Assert.That(result.TotalCount, Is.EqualTo(3), "TotalCount counts unique cards, not node rows");

        Assert.That(result.LocationNames, Is.Not.Null);
        Assert.That(result.LocationNames[_cardA], Is.EqualTo("Deck One"));
        var bNames = result.LocationNames[_cardB].Split(", ");
        Assert.That(bNames, Is.EquivalentTo(new[] { "Deck One", "Deck Two" }),
            "a card from two selected decks shows both names");
        Assert.That(result.LocationNames[_cardC], Is.EqualTo("Deck Two"));
    }

    [Test]
    public async Task EmptyNodeList_SearchesEveryLocation_KbOnlyCardStaysOut()
    {
        var result = await _cardStore.GetPagedAsync(Filter(new List<int>()));

        Assert.That(result.Cards.Select(c => c.Id), Is.EquivalentTo(new[] { _cardA, _cardB, _cardC }),
            "0 chips = every collection/deck/sideboard — the KB-only card must not leak in");
        Assert.That(result.LocationNames![_cardB].Split(", "), Is.EquivalentTo(new[] { "Deck One", "Deck Two" }));
    }

    [Test]
    public async Task NullNodeList_Off_NoLocationFilter()
    {
        var result = await _cardStore.GetPagedAsync(Filter(null));

        Assert.That(result.TotalCount, Is.EqualTo(4), "null = plain KB query, orphan included");
        Assert.That(result.LocationNames, Is.Null, "LocationNames belongs to the locations mode only");
    }

    [Test]
    public async Task OwnershipFilter_RidesInsideExists_MeansOwnedInAtLeastOneSelectedNode()
    {
        var ownedInDeck1 = new CardListFilter
        {
            Page = 1, PageSize = 50,
            LocationNodeIds = new List<int> { _deck1 },
            Ownership = "owned",
        };
        var onlyDeck1 = await _cardStore.GetPagedAsync(ownedInDeck1);
        // B is owned in deck1 → present; C lives only in deck2 → out of scope entirely.
        Assert.That(onlyDeck1.Cards.Select(c => c.Id), Is.EquivalentTo(new[] { _cardA, _cardB }));

        // Borrow B in deck2 only: restricting to deck1 + owned must drop B…
        var deck1Owned = new CardListFilter
        {
            Page = 1, PageSize = 50,
            LocationNodeIds = new List<int> { _deck1 },
            Ownership = "owned",
        };
        // …but deck1 has B owned, so B stays. The discriminating case: borrowed-only card.
        var borrowedOnly = await _cardStore.GetPagedAsync(new CardListFilter
        {
            Page = 1, PageSize = 50,
            LocationNodeIds = new List<int> { _deck2 },
            Ownership = "owned",
        });
        Assert.That(borrowedOnly.Cards.Select(c => c.Id), Is.EquivalentTo(new[] { _cardC }),
            "B in deck2 is borrowed — an owned-only search over deck2 must exclude it");

        // Across both nodes B qualifies via its owned deck1 copy ("at least one selected node").
        var bothOwned = await _cardStore.GetPagedAsync(new CardListFilter
        {
            Page = 1, PageSize = 50,
            LocationNodeIds = new List<int> { _deck1, _deck2 },
            Ownership = "owned",
        });
        Assert.That(bothOwned.Cards.Select(c => c.Id), Is.EquivalentTo(new[] { _cardA, _cardB, _cardC }));
        _ = deck1Owned;
    }

    [Test]
    public async Task SingleNode_LocationFilter_KeepsJoinPath_AndNoCopyTotals()
    {
        // Regression guard: the single-chip deck path (LocationFilter) is untouched by the feature.
        var result = await _cardStore.GetPagedAsync(new CardListFilter
        {
            Page = 1, PageSize = 50,
            LocationFilter = _deck1,
        });

        Assert.That(result.Cards.Select(c => c.Id), Is.EquivalentTo(new[] { _cardA, _cardB }));
        Assert.That(result.LocationNames, Is.Null, "deck mode shows its own counts, not LocationNames");
    }

    [Test]
    public async Task Query_WithLocationFilter_FtsRespectsNodeScope()
    {
        // FTS inside a deck (single-chip mode) must not leak matches from other nodes —
        // the contract behind "search works in deck mode too".
        var bolt = Guid.NewGuid();
        var greaves = Guid.NewGuid();
        await _cardStore.AddAllAsync(new[] { Card(bolt, "Lightning Bolt"), Card(greaves, "Lightning Greaves") });

        using var conn = new SqliteConnection($"Data Source={_dbPath};Default Timeout=30");
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            "INSERT INTO Deck (CollectionId, CardId, Count, IsOwn) VALUES (@d1, @b, 1, 1), (@d2, @g, 1, 1)",
            new { d1 = _deck1, d2 = _deck2, b = bolt, g = greaves });

        var result = await _cardStore.GetPagedAsync(new CardListFilter
        {
            Page = 1, PageSize = 50, Query = "lightning", LocationFilter = _deck1,
        });

        Assert.That(result.Cards.Select(c => c.Id), Is.EquivalentTo(new[] { bolt }),
            "deck1 search finds Bolt but not Greaves from deck2");
    }

    [Test]
    public async Task GetLocationNodesAndSetCodes_FlatNodesAndUnionScopes()
    {
        var collectionStore = new SqliteCollectionStore(_dbPath);

        var nodes = (await collectionStore.GetLocationNodesAsync()).ToList();
        Assert.That(nodes.Select(n => n.Id), Is.SupersetOf(new[] { _deck1, _deck2 }));
        Assert.That(nodes.First(n => n.Id == _deck1).NodeType, Is.EqualTo(NodeType.Deck));

        var union = (await collectionStore.GetNodesSetCodesAsync(new List<int> { _deck1, _deck2 })).ToList();
        Assert.That(union, Is.EquivalentTo(new[] { "m19" }));   // one set here — distinctness is the contract

        var single = (await collectionStore.GetNodesSetCodesAsync(new List<int> { _deck1 })).ToList();
        Assert.That(single, Is.EquivalentTo(new[] { "m19" }));

        var all = (await collectionStore.GetNodesSetCodesAsync(null)).ToList();
        Assert.That(all, Is.EquivalentTo(new[] { "m19" }), "null = every node, one query");
    }
}
