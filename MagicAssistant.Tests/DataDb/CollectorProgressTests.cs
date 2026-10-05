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
        // Add set only, no cards — CardCount 0 → Total 0
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core));

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        Assert.That(progress, Has.Count.EqualTo(1));
        var m19 = progress.First(p => p.SetAbbreviation == "m19");
        Assert.That(m19.TotalCards, Is.EqualTo(0L));
        Assert.That(m19.OwnedCards, Is.EqualTo(0L));
    }

    [Test]
    public async Task CollectorProgress_ReleaseDate_IsParsedFromTextStorage()
    {
        // Regression: ReleaseDate is stored as TEXT in SQLite; the dynamic-row mapping must parse it
        // into DateTime, otherwise every set arrives with null and date sorting degrades to name order.
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "aaa", "Alpha Set", setType: CardSetType.core, releaseDate: new DateTime(2019, 5, 31)));
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "zzz", "Zeta Set", setType: CardSetType.core, releaseDate: new DateTime(2024, 10, 4)));

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        var aaa = progress.First(p => p.SetAbbreviation == "aaa");
        var zzz = progress.First(p => p.SetAbbreviation == "zzz");
        Assert.That(aaa.ReleaseDate, Is.EqualTo(new DateTime(2019, 5, 31)),
            "ReleaseDate must be parsed from TEXT storage, not dropped to null.");
        Assert.That(zzz.ReleaseDate, Is.EqualTo(new DateTime(2024, 10, 4)));
        Assert.That(aaa.ReleaseDate!.Value, Is.LessThan(zzz.ReleaseDate!.Value));
    }

    [Test]
    public async Task SetProgressDetails_RarityAndColorBreakdowns_MatchOwnership()
    {
        // Rarity is stored lowercase (Scryfall wire values); Colors is a comma list of single letters.
        var c1 = CreateCard(Guid.NewGuid(), "Bolt", "m19", "1");
        c1.Rarity = "common"; c1.Colors = new[] { "R" };
        var c2 = CreateCard(Guid.NewGuid(), "Mulligan", "m19", "2");
        c2.Rarity = "uncommon"; c2.Colors = new[] { "U", "W" };
        var c3 = CreateCard(Guid.NewGuid(), "Legend", "m19", "3");
        c3.Rarity = "mythic"; c3.Colors = null;   // colorless

        await _cardStore.AddAllAsync(new[] { c1, c2, c3 });
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core, cardCount: 3));

        var deck = await _collectionStore.CreateDeckAsync("Test Deck");
        // Own c1 (common R) and c3 (mythic colorless); c2 stays unowned
        await _collectionStore.AddAsync(
            new MagicCardPhysical(new MagicCard { Id = c1.Id, Name = "Bolt" }, Location.FromId(deck.Id, deck.Name, false), true) { Count = 1 });
        await _collectionStore.AddAsync(
            new MagicCardPhysical(new MagicCard { Id = c3.Id, Name = "Legend" }, Location.FromId(deck.Id, deck.Name, false), true) { Count = 1 });

        var details = await _cardStore.GetSetProgressDetailsAsync("m19");

        Assert.That(details, Is.Not.Null);
        Assert.That(details!.SetName, Is.EqualTo("Magic 2019"));
        Assert.That(details.SetType, Is.EqualTo(CardSetType.core));

        var common = details.RarityGroups.First(g => g.Key == "common");
        var uncommon = details.RarityGroups.First(g => g.Key == "uncommon");
        var mythic = details.RarityGroups.First(g => g.Key == "mythic");
        Assert.That(common, Is.EqualTo(new SetGroupProgress("common", 1, 1)));
        Assert.That(uncommon, Is.EqualTo(new SetGroupProgress("uncommon", 0, 1)));
        Assert.That(mythic, Is.EqualTo(new SetGroupProgress("mythic", 1, 1)));

        // Multicolor counts once per color; colorless = NULL Colors
        var w = details.ColorGroups.First(g => g.Key == "W");
        var u = details.ColorGroups.First(g => g.Key == "U");
        var colorless = details.ColorGroups.First(g => g.Key == "C");
        Assert.That(w, Is.EqualTo(new SetGroupProgress("W", 0, 1)));
        Assert.That(u, Is.EqualTo(new SetGroupProgress("U", 0, 1)));
        Assert.That(colorless, Is.EqualTo(new SetGroupProgress("C", 1, 1)));

        Assert.That(await _cardStore.GetSetProgressDetailsAsync("nope"), Is.Null);
    }

    [Test]
    public async Task CollectorProgress_Total_ComesFromSetCardCount()
    {
        // Total is the Scryfall checklist size stored on the set, not a KB aggregate
        await _cardStore.AddAllAsync(new[]
        {
            CreateCard(Guid.NewGuid(), "Lightning Bolt", "m19", "1"),
            CreateCard(Guid.NewGuid(), "Mountain", "m19", "2"),
        });
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core, cardCount: 300));

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        var m19 = progress.First(p => p.SetAbbreviation == "m19");
        Assert.That(m19.TotalCards, Is.EqualTo(300L)); // CardSet.CardCount as-is
    }

    [Test]
    public async Task CollectorProgress_OwnedCards_CountsOnlyOwned()
    {
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();

        await _cardStore.AddAllAsync(new[]
        {
            CreateCard(cardId1, "Lightning Bolt", "m19", "1"),
            CreateCard(cardId2, "Mountain", "m19", "2"),
        });
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core, cardCount: 2));

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
        Assert.That(m19.TotalCards, Is.EqualTo(2L)); // Set checklist size
        Assert.That(m19.OwnedCards, Is.EqualTo(1L));  // Only Lightning Bolt is owned
    }

    [Test]
    public async Task CollectorProgress_MultipleSets_TrackedIndependently()
    {
        await _cardStore.AddAllAsync(new[]
        {
            CreateCard(Guid.NewGuid(), "Lightning Bolt", "m19", "1"),
            CreateCard(Guid.NewGuid(), "Mountain", "m19", "2"),
            CreateCard(Guid.NewGuid(), "Forest", "m20", "1"),
        });
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core, cardCount: 2));
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "m20", "Magic 2020", setType: CardSetType.core, cardCount: 1));

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
            CreateCard(cardId, "Lightning Bolt", "m19", "1"),
        });
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core, cardCount: 1));

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
    public async Task CollectorProgress_DifferentPrintingsSameOracleAndLanguage_CountedSeparately()
    {
        // Same OracleId + Language, different Id/CollectorNumber → two separate cards
        var oracleId = Guid.NewGuid();
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();

        var card1 = CreateCard(cardId1, "Lightning Bolt", "m19", "108a");
        card1.OracleId = oracleId;
        var card2 = CreateCard(cardId2, "Lightning Bolt", "m19", "108b"); // alt art, same language
        card2.OracleId = oracleId;

        await _cardStore.AddAllAsync(new[] { card1, card2 });
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core, cardCount: 2));

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
        Assert.That(m19.OwnedCards, Is.EqualTo(2L)); // Different CollectorNumbers = different cards
    }

    [Test]
    public async Task CollectorProgress_SamePrintingOtherLanguage_CountedOnce()
    {
        // Same OracleId + CollectorNumber, different Language → one card (language duplicate)
        var oracleId = Guid.NewGuid();
        var cardIdEn = Guid.NewGuid();
        var cardIdRu = Guid.NewGuid();

        var cardEn = CreateCard(cardIdEn, "Lightning Bolt", "m19", "108");
        cardEn.OracleId = oracleId;
        var cardRu = CreateCard(cardIdRu, "Lightning Bolt", "m19", "108"); // ru duplicate
        cardRu.OracleId = oracleId;
        cardRu.Language = "ru";

        await _cardStore.AddAllAsync(new[] { cardEn, cardRu });
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core, cardCount: 1));

        var deck = await _collectionStore.CreateDeckAsync("Test Deck");

        // Own both language variants of the same printing
        await _collectionStore.AddAsync(
            new MagicCardPhysical(
                new MagicCard { Id = cardIdEn, Name = "Lightning Bolt" },
                Location.FromId(deck.Id, deck.Name, false), true) { Count = 1 });

        await _collectionStore.AddAsync(
            new MagicCardPhysical(
                new MagicCard { Id = cardIdRu, Name = "Lightning Bolt" },
                Location.FromId(deck.Id, deck.Name, false), true) { Count = 1 });

        var progress = await _cardStore.GetCollectorProgressRawAsync();

        var m19 = progress.First(p => p.SetAbbreviation == "m19");
        Assert.That(m19.OwnedCards, Is.EqualTo(1L)); // Shared CollectorNumber = one card
    }

    [Test]
    public async Task CollectorProgress_SideboardOwned_CountedAsOwned()
    {
        var cardId = Guid.NewGuid();
        await _cardStore.AddAllAsync(new[]
        {
            CreateCard(cardId, "Lightning Bolt", "m19", "1"),
        });
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core, cardCount: 1));

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
            CreateCard(cardId1, "Lightning Bolt", "m19", "1"),
            CreateCard(cardId2, "Mountain", "m19", "2"),
        });
        await _cardStore.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core, cardCount: 2));

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

    private static MagicCard CreateCard(Guid cardId, string name, string setCode, string? collectorNumber = null)
    {
        return new MagicCard
        {
            Id = cardId,
            Name = name,
            SetCode = setCode,
            Rarity = "Common",
            CollectorNumber = collectorNumber ?? "1",
        };
    }
}
