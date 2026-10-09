using MagicAssistant.Data.Model;
using Dapper;
using MagicAssistant.Data.Model.NavigatorNodes;
using Microsoft.Data.Sqlite;

namespace MagicAssistant.Tests.DataDb;

[TestFixture]
public class SqliteCollectionStoreTests
{
    private string _dbPath = "";
    private static Guid _scryfall_id_1 = Guid.NewGuid();
    private static Guid _id_1 = Guid.NewGuid();
    private static Guid _id_2 = Guid.NewGuid();
    private static Guid _same_id = Guid.NewGuid();

    private SqliteCollectionStore _store = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test-collections-{Guid.NewGuid()}.db");
        await InitializeDb.EnsureCreatedAsync(_dbPath);

        // Seed Cards table — DeckCards(CardId) has FK → Cards(Id), so stub cards must exist
        // before physical cards can be added. Without this, every AddAsync/AddAllAsync fails
        // with "FOREIGN KEY constraint failed".
        await SeedStubCardsAsync(_dbPath);

        _store = new SqliteCollectionStore(_dbPath);
        await _store.InitializeAsync();
    }

    private static async Task SeedStubCardsAsync(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        // Insert minimal stub cards with all CardIds used across tests.
        // Name is NOT NULL, Set is NOT NULL per schema.
        var ids = new[] { _scryfall_id_1, _id_1, _id_2, _same_id };
        foreach (var id in ids)
        {
            await conn.ExecuteAsync(
                "INSERT OR IGNORE INTO Cards (Id, Name, SetCode) VALUES (@Id, @Name, @SetCode)",
                new { Id = id, Name = $"Card {id}", SetCode = "TST" });
        }
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_store != null)
            await _store.DisposeAsync();

        // Microsoft.Data.Sqlite (v6+) pools connections by default.
        // Disposed connections return to the pool, keeping native file handles open on Windows.
        // ClearAllPools() releases all pooled connections so the .db file can be deleted.
        SqliteConnection.ClearAllPools();

        DeleteDbFiles(_dbPath);
    }

    private static void DeleteDbFiles(string dbPath)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            var file = dbPath + suffix;
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    [Test]
    public async Task CreateDeckAsync_CreatesDeckAndSideboard()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");

        Assert.That(deck.Id, Is.GreaterThan(0));
        Assert.That(deck.Name, Is.EqualTo("Test Deck"));
        Assert.That(deck.NodeType, Is.EqualTo(NodeType.Deck));

        // Verify sideboard exists as a child node (query directly — GetChildrenAsync filters out sideboards)
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        var sbRows = (await conn.QueryAsync<CollectionRow>(
            "SELECT Id, Name, ParentId, NodeType, Locked FROM Collection WHERE ParentId = @DeckId AND NodeType = 3",
            new { DeckId = deck.Id })).ToList();
        Assert.That(sbRows, Has.Count.EqualTo(1), "Sideboard should be created as a child of the deck");
        Assert.That(sbRows[0].NodeType, Is.EqualTo(3)); // NodeType.Sideboard
    }

    private class CollectionRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? ParentId { get; set; }
        public int NodeType { get; set; }
        public bool Locked { get; set; }
    }

    [Test]
    public async Task CreateDeckAsync_CreatesWithParent()
    {
        var parent = await _store.CreateCollectionAsync("Parent Folder", null);
        var deck = await _store.CreateDeckAsync("Child Deck", parent.Id);

        Assert.That(deck.ParentId, Is.EqualTo(parent.Id));
    }

    [Test]
    public async Task DeleteAsync_DeletesDeckAndSideboard()
    {
        var deck = await _store.CreateDeckAsync("To Delete");

        // Verify sideboard exists before deletion (query directly — GetChildrenAsync filters out sideboards)
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        var sbRows = (await conn.QueryAsync<int>(
            "SELECT Id FROM Collection WHERE ParentId = @DeckId AND NodeType = 3",
            new { DeckId = deck.Id })).ToList();
        Assert.That(sbRows, Has.Count.EqualTo(1), "Sideboard should exist before deletion");
        var sbId = sbRows[0];

        var result = await _store.DeleteAsync(deck.Id);

        Assert.That(result, Is.True);

        var deletedDeck = await _store.GetByIdAsync(deck.Id);
        Assert.That(deletedDeck, Is.Null, "Deck should be deleted");

        // Verify sideboard was also deleted (CASCADE or explicit delete)
        var deletedSb = await _store.GetByIdAsync(sbId);
        Assert.That(deletedSb, Is.Null, "Sideboard should also be deleted when deck is deleted");
    }

    [Test]
    public async Task RenameAsync_RenamesDeckAndSideboard()
    {
        var deck = await _store.CreateDeckAsync("Old Name");
        var result = await _store.RenameAsync(deck.Id, "New Name");

        Assert.That(result, Is.True);

        var renamed = await _store.GetByIdAsync(deck.Id);
        Assert.That(renamed!.Name, Is.EqualTo("New Name"));

        // Sideboard should also be renamed (query directly — GetChildrenAsync filters out sideboards)
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        var sbRows = (await conn.QueryAsync<CollectionRow>(
            "SELECT Id, Name, ParentId, NodeType, Locked FROM Collection WHERE ParentId = @DeckId AND NodeType = 3",
            new { DeckId = deck.Id })).ToList();
        Assert.That(sbRows, Has.Count.EqualTo(1), "Sideboard child should exist");
        Assert.That(sbRows[0].Name, Is.EqualTo("New Name (sb)"));
    }

    [Test]
    public async Task CreateCollectionAsync_CreatesCollectionWithoutSideboard()
    {
        var collection = await _store.CreateCollectionAsync("My Collection", null);

        Assert.That(collection.Id, Is.GreaterThan(0));
        Assert.That(collection.Name, Is.EqualTo("My Collection"));
        Assert.That(collection.NodeType, Is.EqualTo(NodeType.Collection));

        // Verify NO sideboard was created (unlike CreateDeckAsync which auto-creates one)
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        var sbRows = (await conn.QueryAsync<int>(
            "SELECT Id FROM Collection WHERE ParentId = @CollectionId AND NodeType = 3",
            new { CollectionId = collection.Id })).ToList();
        Assert.That(sbRows, Is.Empty, "CreateCollectionAsync should NOT create a sideboard row");
    }

    [Test]
    public async Task GetDeckCardsAsync_ReturnsEmpty_WhenNoCards()
    {
        var deck = await _store.CreateDeckAsync("Empty Deck");
        var cards = await _store.GetDeckCardsAsync(deck.Id);

        Assert.That(cards, Is.Empty);
    }

    [Test]
    public async Task AddAsync_AddsCardToDeck()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        var card = new MagicCard { Id = _scryfall_id_1, Name = "Lightning Bolt" };
        var physical = new MagicCardPhysical(card, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 1 };

        await _store.AddAsync(physical);

        var cards = await _store.GetDeckCardsAsync(deck.Id);
        Assert.That(cards, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AddAllAsync_AddsMultipleCards()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        var cards = new[]
        {
            new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card 1" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 1 },
            new MagicCardPhysical(new MagicCard { Id = _id_2, Name = "Card 2" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 1 },
        };

        await _store.AddAllAsync(cards);

        var result = await _store.GetDeckCardsAsync(deck.Id);
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AddAllAsync_MergesDuplicateCards()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        var card1 = new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Same Card" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 1 };
        var card2 = new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Same Card" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 2 };

        await _store.AddAllAsync(new[] { card1 });
        await _store.AddAllAsync(new[] { card2 });

        var result = await _store.GetDeckCardsAsync(deck.Id);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.First().Count, Is.EqualTo(3));
    }

    [Test]
    public async Task RemoveAsync_RemovesCard()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        var card = new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 1 };

        await _store.AddAsync(card);
        await _store.RemoveAsync(card);

        var result = await _store.GetDeckCardsAsync(deck.Id);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task RemoveCardsAsync_EmptyList_ClearsAllCards_ReturnsTotalCopies()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        await _store.AddAllAsync(new[]
        {
            new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card 1" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 2 },
            new MagicCardPhysical(new MagicCard { Id = _id_2, Name = "Card 2" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 3 },
        });

        var removed = await _store.RemoveCardsAsync(deck.Id, Array.Empty<Guid>());

        Assert.That(removed, Is.EqualTo(5), "SUM(Count) — общее число копий, а не количество строк");
        Assert.That(await _store.GetDeckCardsAsync(deck.Id), Is.Empty);
    }

    [Test]
    public async Task RemoveCardsAsync_WithCardIds_RemovesAllCopiesOfListedOnly()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        await _store.AddAllAsync(new[]
        {
            new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card 1" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 2 },
            new MagicCardPhysical(new MagicCard { Id = _id_2, Name = "Card 2" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 3 },
        });

        var removed = await _store.RemoveCardsAsync(deck.Id, new[] { _id_1 });

        Assert.That(removed, Is.EqualTo(2));
        var rest = await _store.GetDeckCardsAsync(deck.Id);
        Assert.That(rest, Has.Count.EqualTo(1));
        Assert.That(rest.First().Card.Id, Is.EqualTo(_id_2), "Неперечисленная карта не тронута");
        Assert.That(rest.First().Count, Is.EqualTo(3));
    }

    [Test]
    public async Task RemoveCardsAsync_UnknownCardId_ReturnsZero()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        await _store.AddAsync(new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 2 });

        var removed = await _store.RemoveCardsAsync(deck.Id, new[] { Guid.NewGuid() });

        Assert.That(removed, Is.EqualTo(0));
        Assert.That(await _store.GetDeckCardsAsync(deck.Id), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RemoveCardsAsync_LockedNode_Throws_CardsKept()
    {
        var deck = await _store.CreateDeckAsync("Locked Deck");
        await _store.AddAsync(new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 2 });
        await _store.LockAsync(deck.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await _store.RemoveCardsAsync(deck.Id, Array.Empty<Guid>()));
        Assert.That(ex!.Message, Does.Contain("locked"));
        Assert.That(await _store.GetDeckCardsAsync(deck.Id), Has.Count.EqualTo(1), "Карты не удалены");
    }

    [Test]
    public async Task RemoveCardsAsync_NodeNotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await _store.RemoveCardsAsync(9999, Array.Empty<Guid>()));
        Assert.That(ex!.Message, Does.Contain("not found"));
    }

    [Test]
    public async Task UpdateAsync_UpdatesCardCount()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        var card = new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 1 };

        await _store.AddAsync(card);
        card.Count = 4;
        await _store.UpdateAsync(card, new[] { MagicCardField.Count });

        var result = (await _store.GetDeckCardsAsync(deck.Id)).First();
        Assert.That(result.Count, Is.EqualTo(4));
    }

    [Test]
    public async Task GetOwnCountStatsAsync_ReturnsCorrectStats_ForOwnedCards()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        var card1 = new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card 1" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 2 };
        var card2 = new MagicCardPhysical(new MagicCard { Id = _id_2, Name = "Card 2" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 1 };

        await _store.AddAllAsync(new[] { card1, card2 });

        var stats = await _store.GetOwnCountStatsAsync();

        Assert.That(stats, Has.Count.EqualTo(2));

        // card1: IsOwn=true, Count=2 → OwnCount=2, TotalCount=2, OwnUnique=1 (1 distinct deck)
        Assert.That(stats[_id_1].OwnCount, Is.EqualTo(2), "Owned copies of id-1 should be 2");
        Assert.That(stats[_id_1].TotalCount, Is.EqualTo(2), "Total copies of id-1 should be 2");
        Assert.That(stats[_id_1].OwnUnique, Is.EqualTo(1), "id-1 is owned in 1 distinct deck");

        // card2: IsOwn=true, Count=1 → OwnCount=1, TotalCount=1, OwnUnique=1 (1 distinct deck)
        Assert.That(stats[_id_2].OwnCount, Is.EqualTo(1), "Owned copies of id-2 should be 1");
        Assert.That(stats[_id_2].TotalCount, Is.EqualTo(1), "Total copies of id-2 should be 1");
        Assert.That(stats[_id_2].OwnUnique, Is.EqualTo(1), "id-2 is owned in 1 distinct deck");
    }

    [Test]
    public async Task GetDeckWithSideboardAsync_IncludesSideboardCards()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        // Find sideboard node (query directly — GetChildrenAsync filters out sideboards)
        using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            await conn.OpenAsync();
            var sbRows = (await conn.QueryAsync<CollectionRow>(
                "SELECT Id, Name, ParentId, NodeType, Locked FROM Collection WHERE ParentId = @DeckId AND NodeType = 3",
                new { DeckId = deck.Id })).ToList();
            Assert.That(sbRows, Has.Count.EqualTo(1));

            var sbId = sbRows[0].Id;
            var mainCard = new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Main" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 1 };
            var sbCard = new MagicCardPhysical(new MagicCard { Id = _id_2, Name = "Sideboard" }, new Collection(sbId, "Sideboard", null, NodeType.Sideboard, 0), true) { Count = 1 };

            await _store.AddAsync(mainCard);
            await _store.AddAsync(sbCard);
        }

        var cards = await _store.GetDeckWithSideboardAsync(deck.Id);

        Assert.That(cards, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetDeckWithSideboardAsync_PropagatesCmcNumberFromCardsTable()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            await conn.OpenAsync();
            // Seed stubs have CmcNumber NULL — set a known value so the Deck→Cards join can carry it.
            await conn.ExecuteAsync(
                "UPDATE Cards SET CmcNumber = @Cmc, ManaCost = @Cost WHERE Id = @Id",
                new { Cmc = 3, Cost = "{2}{R}", Id = _id_1 });

            var card = new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Main" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 4 };
            await _store.AddAsync(card);
        }

        var cards = await _store.GetDeckWithSideboardAsync(deck.Id);

        Assert.That(cards, Has.Count.EqualTo(1));
        Assert.That(cards.First().Card.CmcNumber, Is.EqualTo(3), "CmcNumber must survive the Deck→Cards join (mana curve depends on it)");
    }

    [Test]
    public async Task GetDeckCardsAsync_ExcludesSideboard()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        // Find sideboard node (query directly — GetChildrenAsync filters out sideboards)
        int sbId;
        using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            await conn.OpenAsync();
            var sbRows = (await conn.QueryAsync<int>(
                "SELECT Id FROM Collection WHERE ParentId = @DeckId AND NodeType = 3",
                new { DeckId = deck.Id })).ToList();
            Assert.That(sbRows, Has.Count.EqualTo(1));
            sbId = sbRows[0];
        }

        // CardId "id-1" is seeded in Cards table; GetDeckCardsAsync does LEFT JOIN
        // and returns card info from the Cards table, NOT from the MagicCard object.
        var mainCard = new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Main" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 1 };
        var sbCard = new MagicCardPhysical(new MagicCard { Id = _id_2, Name = "Sideboard" }, new Collection(sbId, "Sideboard", null, NodeType.Sideboard, 0), true) { Count = 1 };

        await _store.AddAsync(mainCard);
        await _store.AddAsync(sbCard);

        var cards = await _store.GetDeckCardsAsync(deck.Id);

        Assert.That(cards, Has.Count.EqualTo(1), "Should return only main deck card, excluding sideboard");
        // GetDeckCardsAsync returns card data from Cards table via LEFT JOIN.
        Assert.That(cards.First().Card.Id, Is.EqualTo(_id_1), "Returned card should be id-1");
    }

    [Test]
    public async Task GetCardCountsAsync_SumsCountsForRequestedCardsOnly()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");

        // card1 ×3 (merged across two rows), card2 ×2 — request only card1 + an absent card
        await _store.AddAllAsync(new[]
        {
            new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card 1" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 1 },
            new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card 1" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 2 },
            new MagicCardPhysical(new MagicCard { Id = _id_2, Name = "Card 2" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 2 },
        });

        var result = await _store.GetCardCountsAsync(new[] { deck.Id }, new[] { _id_1, _scryfall_id_1 });

        Assert.That(result, Has.Count.EqualTo(1), "Only requested cards present in the collection should be returned");
        Assert.That(result[(deck.Id, _id_1)], Is.EqualTo(3), "Counts of merged duplicate rows must be summed");
    }

    [Test]
    public async Task GetCardCountsAsync_ReturnsEmpty_ForEmptyIdList()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");

        Assert.That(await _store.GetCardCountsAsync(new[] { deck.Id }, Array.Empty<Guid>()), Is.Empty);
        Assert.That(await _store.GetCardCountsAsync(Array.Empty<int>(), new[] { _id_1 }), Is.Empty);
    }

    [Test]
    public async Task GetCardCountsAsync_ReturnsPerCollectionTotals_ForMultipleCollections()
    {
        var deck1 = await _store.CreateDeckAsync("Deck 1");
        var deck2 = await _store.CreateDeckAsync("Deck 2");

        // Same card in both decks with different counts + a card only in deck1
        await _store.AddAllAsync(new[]
        {
            new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card 1" }, new Collection(deck1.Id, deck1.Name, null, NodeType.Deck, 0), true) { Count = 2 },
            new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card 1" }, new Collection(deck2.Id, deck2.Name, null, NodeType.Deck, 0), true) { Count = 5 },
            new MagicCardPhysical(new MagicCard { Id = _id_2, Name = "Card 2" }, new Collection(deck1.Id, deck1.Name, null, NodeType.Deck, 0), true) { Count = 1 },
        });

        var result = await _store.GetCardCountsAsync(new[] { deck1.Id, deck2.Id }, new[] { _id_1, _id_2 });

        Assert.That(result[(deck1.Id, _id_1)], Is.EqualTo(2));
        Assert.That(result[(deck2.Id, _id_1)], Is.EqualTo(5), "Per-collection totals must not be merged across collections");
        Assert.That(result[(deck1.Id, _id_2)], Is.EqualTo(1));
        Assert.That(result.ContainsKey((deck2.Id, _id_2)), Is.False, "Card absent from deck2 must have no key");
    }

    [Test]
    public async Task GetCardCountsAsync_ExcludesSideboardRows()
    {
        var deck = await _store.CreateDeckAsync("Test Deck");
        // Find sideboard node (query directly — GetChildrenAsync filters out sideboards)
        int sbId;
        using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            await conn.OpenAsync();
            var sbRows = (await conn.QueryAsync<int>(
                "SELECT Id FROM Collection WHERE ParentId = @DeckId AND NodeType = 3",
                new { DeckId = deck.Id })).ToList();
            Assert.That(sbRows, Has.Count.EqualTo(1));
            sbId = sbRows[0];
        }

        var mainCard = new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Main" }, new Collection(deck.Id, deck.Name, null, NodeType.Deck, 0), true) { Count = 2 };
        var sbCard = new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Main" }, new Collection(sbId, "Sideboard", null, NodeType.Sideboard, 0), true) { Count = 5 };

        await _store.AddAsync(mainCard);
        await _store.AddAsync(sbCard);

        var result = await _store.GetCardCountsAsync(new[] { deck.Id }, new[] { _id_1 });

        Assert.That(result[(deck.Id, _id_1)], Is.EqualTo(2), "Sideboard rows must be excluded — same as GetDeckCardsAsync");
    }

    [Test]
    public async Task GetAllAsync_ReturnsAllPhysicalCards()
    {
        var deck1 = await _store.CreateDeckAsync("Deck 1");
        var deck2 = await _store.CreateDeckAsync("Deck 2");
        
        var card1 = new MagicCardPhysical(new MagicCard { Id = _id_1, Name = "Card 1" }, new Collection(deck1.Id, string.Empty, null, NodeType.Deck, 0), true) { Count = 1 };
        var card2 = new MagicCardPhysical(new MagicCard { Id = _id_2, Name = "Card 2" }, new Collection(deck2.Id, string.Empty, null, NodeType.Deck, 0), true) { Count = 1 };

        await _store.AddAsync(card1);
        await _store.AddAsync(card2);

        var all = (await _store.GetAllDeckCardsAsync()).ToList();

        Assert.That(all, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetCardsAsync_ReturnsAllInstances_ById()
    {
        var deck1 = await _store.CreateDeckAsync("Deck 1");
        var deck2 = await _store.CreateDeckAsync("Deck 2");
        
        var card1 = new MagicCardPhysical(new MagicCard { Id = _same_id, Name = "Same" }, new Collection(deck1.Id, string.Empty, null, NodeType.Deck, 0), true) { Count = 1 };
        var card2 = new MagicCardPhysical(new MagicCard { Id = _same_id, Name = "Same" }, new Collection(deck2.Id, string.Empty, null, NodeType.Deck, 0), true) { Count = 1 };

        await _store.AddAsync(card1);
        await _store.AddAsync(card2);

        var instances = await _store.GetCardsAsync(_same_id);

        Assert.That(instances, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetChildrenAsync_ReturnsCorrectCardCounts()
    {
        var parent = await _store.CreateCollectionAsync("Parent", null);
        var deck1 = await _store.CreateDeckAsync("Deck 1", parent.Id);
        var deck2 = await _store.CreateDeckAsync("Deck 2", parent.Id);

        // Add cards to deck1
        var card = new MagicCardPhysical(new MagicCard { Id = _id_1 }, new Collection(deck1.Id, string.Empty, null, NodeType.Deck, 0), true) { Count = 1 };
        await _store.AddAsync(card);

        var children = (await _store.GetChildrenAsync(parent.Id)).ToList();

        Assert.That(children, Has.Count.EqualTo(2));
        Assert.That(children.First(c => c.Id == deck1.Id).CardCount, Is.EqualTo(1));
        Assert.That(children.First(c => c.Id == deck2.Id).CardCount, Is.EqualTo(0));
    }
}
