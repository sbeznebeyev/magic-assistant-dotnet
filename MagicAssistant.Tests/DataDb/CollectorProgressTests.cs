using MagicAssistant.Data.Model.NavigatorNodes;
using Microsoft.Data.Sqlite;
using Dapper;

namespace MagicAssistant.Tests.DataDb;

[TestFixture]
public class CollectorProgressTests
{
    private string _dbPath = "";
    private SqliteCardStore _cardStore = null!;
    private SqliteCollectionStore _collectionStore = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test-collector-{Guid.NewGuid()}.db");
        await InitializeDb.EnsureCreatedAsync(_dbPath);
        _cardStore = new SqliteCardStore(_dbPath);
        _collectionStore = new SqliteCollectionStore(_dbPath);
        await _cardStore.InitializeAsync();
        await _collectionStore.InitializeAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _cardStore.DisposeAsync();
        await _collectionStore.DisposeAsync();

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

    /* ═══════════════════════════════════════════════════════
     * Collector Progress — CTE-запросы на корректность
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task CollectorProgress_EmptyDatabase_ReturnsEditionsWithZeroCounts()
    {
        // Add edition only, no cards
        await _cardStore.AddSetAsync(new CardSet("m19", "Magic 2019", null, "core"));

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        Assert.That(progress, Has.Count.EqualTo(1));
        var m19 = progress.First(p => p.SetAbbreviation == "m19");
        Assert.That(m19.TotalCards, Is.EqualTo(0L));
        Assert.That(m19.OwnedCards, Is.EqualTo(0L));
    }

    [Test]
    public async Task CollectorProgress_CardsInKB_CountsDistinctNames()
    {
        // Add 2 printings of the same card name + 1 different card
        await _cardStore.AddAllAsync(new[]
        {
            CreateCard(Guid.NewGuid(), "Lightning Bolt", "m19"),
            CreateCard(Guid.NewGuid(), "Lightning Bolt", "m20"), // Same name, different set — counts as 1 in M19
            CreateCard(Guid.NewGuid(), "Mountain", "m19"),
        });
        await _cardStore.AddSetAsync(new CardSet("m19", "Magic 2019", null, "core"));

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        var m19 = progress.First(p => p.SetAbbreviation == "m19");
        Assert.That(m19.TotalCards, Is.EqualTo(2L)); // Lightning Bolt + Mountain (distinct names)
    }

    [Test]
    public async Task CollectorProgress_OwnedCards_CountsOnlyOwned()
    {
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();

        await _cardStore.AddAllAsync(new[]
        {
            CreateCard(cardId1, "Lightning Bolt", "m19"),
            CreateCard(cardId2, "Mountain", "m19"),
        });
        await _cardStore.AddSetAsync(new CardSet("m19", "Magic 2019", null, "core"));

        // Create deck with 1 owned + 1 borrowed card
        var deck = await _collectionStore.CreateDeckAsync("Test Deck");

        // Add owned card
        await _collectionStore.AddAsync(
            new MagicCardPhysical(
                new MagicCard { Id = cardId1, Name = "Lightning Bolt" },
                Location.FromId(deck.Id, deck.Name, false), true) { Count = 1 });

        // Add borrowed card
        await _collectionStore.AddAsync(
            new MagicCardPhysical(
                new MagicCard { Id = cardId2, Name = "Mountain" },
                Location.FromId(deck.Id, deck.Name, false), false) { Count = 1 });

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        var m19 = progress.First(p => p.SetAbbreviation == "m19");
        Assert.That(m19.TotalCards, Is.EqualTo(2L)); // Both in KB
        Assert.That(m19.OwnedCards, Is.EqualTo(1L));  // Only Lightning Bolt is owned
    }

    [Test]
    public async Task CollectorProgress_MultipleSets_TrackedIndependently()
    {
        await _cardStore.AddAllAsync(new[]
        {
            CreateCard(Guid.NewGuid(), "Lightning Bolt", "m19"),
            CreateCard(Guid.NewGuid(), "Mountain", "m19"),
            CreateCard(Guid.NewGuid(), "Forest", "m20"),
        });
        await _cardStore.AddSetAsync(new CardSet("m19", "Magic 2019", null, "core"));
        await _cardStore.AddSetAsync(new CardSet("m20", "Magic 2020", null, "core"));

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        var m19 = progress.First(p => p.SetAbbreviation == "m19");
        var m20 = progress.First(p => p.SetAbbreviation == "m20");

        Assert.That(m19.TotalCards, Is.EqualTo(2L));
        Assert.That(m20.TotalCards, Is.EqualTo(1L));
    }

    [Test]
    public async Task CollectorProgress_SameCardInMultipleDecks_CountedOnce()
    {
        var cardId = Guid.NewGuid();

        await _cardStore.AddAllAsync(new[]
        {
            CreateCard(cardId, "Lightning Bolt", "m19"),
        });
        await _cardStore.AddSetAsync(new CardSet("m19", "Magic 2019", null, "core"));

        // Create two decks with the same card
        var deck1 = await _collectionStore.CreateDeckAsync("Deck 1");
        var deck2 = await _collectionStore.CreateDeckAsync("Deck 2");

        await _collectionStore.AddAsync(
            new MagicCardPhysical(
                new MagicCard { Id = cardId, Name = "Lightning Bolt" },
                Location.FromId(deck1.Id), true) { Count = 1 });

        await _collectionStore.AddAsync(
            new MagicCardPhysical(
                new MagicCard { Id = cardId, Name = "Lightning Bolt" },
                Location.FromId(deck2.Id), true) { Count = 1 });

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        var m19 = progress.First(p => p.SetAbbreviation == "m19");
        Assert.That(m19.OwnedCards, Is.EqualTo(1L)); // Same card name counted once
    }

    [Test]
    public async Task CollectorProgress_DifferentPrintingsSameName_CountedOnce()
    {
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();

        await _cardStore.AddAllAsync(new[]
        {
            CreateCard(cardId1, "Lightning Bolt", "m19"),
            CreateCard(cardId2, "Lightning Bolt", "m19"), // Different printing, same name
        });
        await _cardStore.AddSetAsync(new CardSet("m19", "Magic 2019", null, "core"));

        var deck = await _collectionStore.CreateDeckAsync("Test Deck");

        // Own both printings
        await _collectionStore.AddAsync(
            new MagicCardPhysical(
                new MagicCard { Id = cardId1, Name = "Lightning Bolt" },
                Location.FromId(deck.Id, deck.Name, false), true) { Count = 1 });

        await _collectionStore.AddAsync(
            new MagicCardPhysical(
                new MagicCard { Id = cardId2, Name = "Lightning Bolt" },
                Location.FromId(deck.Id, deck.Name, false), true) { Count = 1 });

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        var m19 = progress.First(p => p.SetAbbreviation == "m19");
        Assert.That(m19.TotalCards, Is.EqualTo(1L));   // Distinct name = 1
        Assert.That(m19.OwnedCards, Is.EqualTo(1L));    // Distinct owned name = 1
    }

    [Test]
    public async Task CollectorProgress_SideboardOwned_CountedAsOwned()
    {
        var cardId = Guid.NewGuid();
        await _cardStore.AddAllAsync(new[]
        {
            CreateCard(cardId, "Lightning Bolt", "m19"),
        });
        await _cardStore.AddSetAsync(new CardSet("m19", "Magic 2019", null, "core"));

        var deck = await _collectionStore.CreateDeckAsync("Test Deck");

        // Add card to sideboard (owned) — query sideboard directly since GetChildrenAsync filters them out
        var sbId = await GetSideboardIdAsync(deck.Id);
        await _collectionStore.AddAsync(
            new MagicCardPhysical(
                new MagicCard { Id = cardId, Name = "Lightning Bolt" },
                Location.FromId(sbId, "Sideboard", true), true) { Count = 1 });

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        var m19 = progress.First(p => p.SetAbbreviation == "m19");
        Assert.That(m19.OwnedCards, Is.EqualTo(1L)); // Sideboard owned cards count
    }

    [Test]
    public async Task CollectorProgress_NoEditions_ReturnsEmpty()
    {
        var progress = await _cardStore.GetCollectorProgressRawAsync();

        Assert.That(progress, Is.Empty);
    }

    [Test]
    public async Task CollectorProgress_CardsInKBButNoEdition_IncludedViaLeftJoin()
    {
        // Add cards but NO edition — they won't appear in progress because
        // the query starts FROM Editions (LEFT JOIN)
        await _cardStore.AddAllAsync(new[]
        {
            CreateCard(Guid.NewGuid(), "Lightning Bolt", "M99"),
        });

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        // M99 has no Edition entry, so it won't appear
        Assert.That(progress.Where(p => p.SetAbbreviation == "M99"), Is.Empty);
    }

    [Test]
    public async Task CollectorProgress_OwnedCountIgnoresBorrowedInSideboard()
    {
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();

        await _cardStore.AddAllAsync(new[]
        {
            CreateCard(cardId1, "Lightning Bolt", "m19"),
            CreateCard(cardId2, "Mountain", "m19"),
        });
        await _cardStore.AddSetAsync(new CardSet("m19", "Magic 2019", null, "core"));

        var deck = await _collectionStore.CreateDeckAsync("Test Deck");

        // Owned in main deck
        await _collectionStore.AddAsync(
            new MagicCardPhysical(
                new MagicCard { Id = cardId1, Name = "Lightning Bolt" },
                Location.FromId(deck.Id, deck.Name, false), true) { Count = 1 });

        // Borrowed in sideboard — query sideboard directly since GetChildrenAsync filters them out
        var sbId = await GetSideboardIdAsync(deck.Id);
        await _collectionStore.AddAsync(
            new MagicCardPhysical(
                new MagicCard { Id = cardId2, Name = "Mountain" },
                Location.FromId(sbId, "Sideboard", true), false) { Count = 1 });

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        var m19 = progress.First(p => p.SetAbbreviation == "m19");
        Assert.That(m19.TotalCards, Is.EqualTo(2L));
        Assert.That(m19.OwnedCards, Is.EqualTo(1L)); // Borrowed not counted
    }

    private async Task<int> GetSideboardIdAsync(int deckId)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        var sbRows = (await conn.QueryAsync<int>(
            "SELECT Id FROM Collection WHERE ParentId = @DeckId AND NodeType = 3",
            new { DeckId = deckId })).ToList();
        return sbRows[0];
    }

    private static MagicCard CreateCard(Guid cardId, string name, string setCode)
    {
        return new MagicCard
        {
            Id = cardId,
            Name = name,
            SetCode = setCode,
            Rarity = "Common",
        };
    }
}
