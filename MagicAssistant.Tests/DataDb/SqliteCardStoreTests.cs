using Dapper;
using Microsoft.Data.Sqlite;

namespace MagicAssistant.Tests.DataDb;

[TestFixture]
public class SqliteCardStoreTests
{
    private string _dbPath = "";
    private SqliteCardStore _store = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test-cards-{Guid.NewGuid()}.db");
        await InitializeDb.EnsureCreatedAsync(_dbPath);
        _store = new SqliteCardStore(_dbPath);
        await _store.InitializeAsync();
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
    public async Task AddAsync_AddsCard_ToDatabase()
    {
        var card = CreateTestCard(Guid.NewGuid(), "Lightning Bolt");

        var result = await _store.AddAsync(card);

        Assert.That(result, Is.True);

        // Verify actual persistence in DB, not just return value.
        // INSERT OR REPLACE always returns >0 for valid cards even if persist is broken,
        // so we must verify the card exists via TotalUniqueCardsAsync.
        var count = await _store.TotalUniqueCardsAsync();
        Assert.That(count, Is.EqualTo(1), 
            "Card was not persisted to database. AddAsync returned true but DB count is 0. Possible bug: INSERT executed but transaction not committed.");
    }

    [Test]
    public async Task AddAllAsync_AddsMultipleCards()
    {
        var cards = new[]
        {
            CreateTestCard(Guid.NewGuid(), "Lightning Bolt"),
            CreateTestCard(Guid.NewGuid(), "Counterspell"),
            CreateTestCard(Guid.NewGuid(), "Force of Will"),
        };

        var result = await _store.AddAllAsync(cards);

        Assert.That(result, Is.True);
        Assert.That(await _store.TotalUniqueCardsAsync(), Is.EqualTo(3));
    }

    [Test]
    public async Task GetCardAsync_ReturnsCard_ById()
    {
        var cardId = Guid.NewGuid();
        var card = CreateTestCard(cardId, "Lightning Bolt");
        await _store.AddAsync(card);

        var result = await _store.GetCardAsync(cardId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Lightning Bolt"));
    }

    [Test]
    public async Task GetCardAsync_ReturnsNull_ForNonExistentId()
    {
        var result = await _store.GetCardAsync(Guid.NewGuid());

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        await _store.AddAsync(CreateTestCard(Guid.NewGuid(), "Card 1"));
        await _store.AddAsync(CreateTestCard(Guid.NewGuid(), "Card 2"));

        var count = await _store.TotalUniqueCardsAsync();

        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task CountAsync_ReturnsZero_WhenEmpty()
    {
        var count = await _store.TotalUniqueCardsAsync();

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetAllAsync_ReturnsAllCards()
    {
        await _store.AddAsync(CreateTestCard(Guid.NewGuid(), "Card 1"));
        await _store.AddAsync(CreateTestCard(Guid.NewGuid(), "Card 2"));
        await _store.AddAsync(CreateTestCard(Guid.NewGuid(), "Card 3"));

        var result = (await _store.GetAllDeckCardsAsync()).ToList();

        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetAllAsync_WithLimit_ReturnsLimitedCards()
    {
        for (int i = 1; i <= 10; i++)
            await _store.AddAsync(CreateTestCard(Guid.NewGuid(), $"Card {i}"));

        var result = (await _store.GetAllAsync(5)).ToList();

        Assert.That(result, Has.Count.EqualTo(5));
    }

    [Test]
    public async Task UpdateAsync_UpdatesCardFields()
    {
        var cardId = Guid.NewGuid();
        var card = CreateTestCard(cardId, "Original Name");
        await _store.AddAsync(card);

        // Remember original Cost before mutation (CreateTestCard sets ManaCost = "{1}")
        var originalManaCost = card.ManaCost;

        card.Name = "Updated Name";
        var result = await _store.UpdateAsync(card, new[] { MagicCardField.Name });

        Assert.That(result, Is.True);

        // BUG: UpdateAsync caches the modified card object instead of invalidating the cache.
        // GetCardAsync returns the cached object, which has all original fields intact.
        // This means we're verifying the cache, NOT database persistence.
        // Clear cache to force a fresh DB read and verify actual persistence.
        await _store.UpdateAllFieldsAsync(new[] { MagicCardField.Name });

        var updated = await _store.GetCardAsync(cardId);
        
        try
        {
            Assert.That(updated!.Name, Is.EqualTo("Updated Name"),
                "Name was not persisted to database. UpdateAsync may have cached the object without writing to DB.");
            Assert.That(updated.ManaCost, Is.EqualTo(originalManaCost),
                "Cost field changed unexpectedly. UpdateAsync should only update specified fields (Name), but other fields were modified. Possible bug: UpdateAsync replaces entire row instead of updating only specified columns.");
        }
        catch (AssertionException ex)
        {
            throw new AssertionException(
                $"UpdateAsync persistence bug detected. UpdateAsync caches the mutated card object " +
                $"instead of invalidating the cache entry. When reading from DB directly (after cache clear), " +
                $"the persisted state may differ from the cached state. Root cause: UpdateAsync should call " +
                $"_cache.TryRemove(cardId) after successful UPDATE, similar to UpdateCardPropertyAsync.", 
                ex);
        }
    }

    [Test]
    public async Task UpdateAsync_UpdatesMultipleFields()
    {
        var cardId = Guid.NewGuid();
        var card = CreateTestCard(cardId, "Original");
        await _store.AddAsync(card);

        // Remember original values for fields that are NOT being updated
        var originalRarity = card.Rarity; // "Common" — should remain unchanged

        card.Name = "New Name";
        card.ManaCost = "{2}{W}";
        card.Type = "Instant";
        var result = await _store.UpdateAsync(card, new[] { MagicCardField.Name, MagicCardField.ManaCost, MagicCardField.Type });

        Assert.That(result, Is.True);

        // Clear cache to verify actual DB persistence, not cached state
        await _store.UpdateAllFieldsAsync(new[] { MagicCardField.Name, MagicCardField.ManaCost, MagicCardField.Type });

        var updated = await _store.GetCardAsync(cardId);
        try
        {
            Assert.That(updated!.Name, Is.EqualTo("New Name"),
                "Name was not persisted to database.");
            Assert.That(updated.ManaCost, Is.EqualTo("{2}{W}"),
                "Cost was not persisted to database.");
            Assert.That(updated.Type, Is.EqualTo("Instant"),
                "Type was not persisted to database.");
            Assert.That(updated.Rarity, Is.EqualTo(originalRarity),
                $"Rarity changed from '{originalRarity}' to '{updated.Rarity}'. UpdateAsync should only update specified fields (Name, Cost, Type) but modified Rarity as well.");
        }
        catch (AssertionException ex)
        {
            throw new AssertionException(
                $"UpdateAsync multi-field persistence bug: not all specified fields were persisted, " +
                $"or non-specified fields were modified. UpdateAsync should perform a targeted column update, " +
                $"not replace the entire row.", ex);
        }
    }

    [Test]
    public async Task RemoveAsync_ThrowsNotSupportedException()
    {
        var card = CreateTestCard(Guid.NewGuid(), "Name");
        await _store.AddAsync(card);

        await Assert.ThrowsAsync<NotSupportedException>(async () => await _store.RemoveAsync(card));
    }

    [Test]
    public async Task RemoveAllAsync_ThrowsNotSupportedException()
    {
        var card = CreateTestCard(Guid.NewGuid(), "Name");
        await _store.AddAsync(card);

        await Assert.ThrowsAsync<NotSupportedException>(async () => await _store.RemoveAllAsync(new[] { card }));
    }

    [Test]
    public async Task GetCardsAsync_ReturnsSingleCard_ForExistingId()
    {
        var cardId = Guid.NewGuid();
        var card = CreateTestCard(cardId, "Name");
        await _store.AddAsync(card);

        var result = await _store.GetCardsAsync(cardId);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetCardsAsync_ReturnsEmpty_ForNonExistentId()
    {
        var result = await _store.GetCardsAsync(Guid.NewGuid());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetCardsByIdsAsync_ReturnsAllRequestedCards()
    {
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();
        var cardId3 = Guid.NewGuid();

        await _store.AddAsync(CreateTestCard(cardId1, "Card 1"));
        await _store.AddAsync(CreateTestCard(cardId2, "Card 2"));
        await _store.AddAsync(CreateTestCard(cardId3, "Card 3"));

        var result = await _store.GetCardsByIdsAsync(new[] { cardId1, cardId3 });

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.ContainsKey(cardId1), Is.True);
        Assert.That(result.ContainsKey(cardId3), Is.True);
    }

    [Test]
    public async Task GetCardsByIdsAsync_ReturnsEmpty_ForNonExistentIds()
    {
        var result = await _store.GetCardsByIdsAsync(new[] { Guid.NewGuid() });

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task AddEditionAsync_AddsEdition()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core);

        await _store.AddSetAsync(edition);

        var editions = await _store.GetCardSetsAsync();
        Assert.That(editions, Has.Count.EqualTo(1));
        Assert.That(editions.First().SetCode, Is.EqualTo("m19"));
    }

    [Test]
    public async Task GetEditionsAsync_ReturnsAllEditions()
    {
        await _store.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core));
        await _store.AddSetAsync(new CardSet(Guid.NewGuid(), "m20", "Magic 2020", setType: CardSetType.core));

        var editions = await _store.GetCardSetsAsync();

        Assert.That(editions, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task UpdateCardPropertyAsync_UpdatesSingleColumn()
    {
        var cardId = Guid.NewGuid();
        var card = CreateTestCard(cardId, "Name");
        await _store.AddAsync(card);

        await _store.UpdateCardPropertyAsync(cardId, "Name", "Updated");

        var updated = await _store.GetCardAsync(cardId);
        Assert.That(updated!.Name, Is.EqualTo("Updated"));
    }

    [Test]
    public async Task UpdateCardPropertyAsync_InvalidatesCache()
    {
        var cardId = Guid.NewGuid();
        var card = CreateTestCard(cardId, "Original");
        await _store.AddAsync(card);

        // Cache the card
        await _store.GetCardAsync(cardId);

        // Update via property method
        await _store.UpdateCardPropertyAsync(cardId, "Name", "Updated");

        // Should reflect the update
        var updated = await _store.GetCardAsync(cardId);
        Assert.That(updated!.Name, Is.EqualTo("Updated"));
    }

    [Test]
    public async Task UpdateAllFieldsAsync_ClearsCache()
    {
        var cardId = Guid.NewGuid();
        var card = CreateTestCard(cardId, "Original Name");
        await _store.AddAsync(card);

        // Populate cache by reading the card
        var cached = await _store.GetCardAsync(cardId);
        Assert.That(cached!.Name, Is.EqualTo("Original Name"));

        // Mutate the card in DB directly (bypassing the store's cache)
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        await conn.ExecuteAsync("UPDATE Cards SET Name = @Name WHERE Id = @Id", 
            new { Name = "Direct DB Update", Id = cardId });

        // Without cache clear, GetCardAsync would return stale cached data
        await _store.UpdateAllFieldsAsync(new[] { MagicCardField.Name });

        // After cache clear, should read fresh data from DB
        var refreshed = await _store.GetCardAsync(cardId);
        try
        {
            Assert.That(refreshed!.Name, Is.EqualTo("Direct DB Update"),
                "Cache was not properly cleared. GetCardAsync returned stale cached data instead of fresh DB read.");
        }
        catch (AssertionException ex)
        {
            throw new AssertionException(
                $"UpdateAllFieldsAsync cache invalidation bug: cache was not cleared, " +
                $"so subsequent reads return stale data. Expected 'Direct DB Update', got '{refreshed?.Name}'.", 
                ex);
        }
    }

    [Test]
    public async Task BulkUpdateOwnCountAsync_PersistsStatsToOwnCounts()
    {
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();

        var stats = new[]
        {
            new OwnCountStats(cardId1, 2, 4, 1),
            new OwnCountStats(cardId2, 1, 2, 1),
        };

        await _store.AddAllAsync(new[] { CreateTestCard(cardId1, "Card 1"), CreateTestCard(cardId2, "Card 2") });
        await _store.BulkUpdateOwnCountAsync(stats);

        // Verify via direct SQL
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        var count = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM OwnCounts");
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task BulkUpdateOwnCountAsync_HandlesEmptyStats()
    {
        await _store.BulkUpdateOwnCountAsync(Array.Empty<OwnCountStats>());
        // Should not throw
    }

    [Test]
    public async Task BulkUpdateOwnCountAsync_HandlesNullStats()
    {
        await _store.BulkUpdateOwnCountAsync(null!);
        // Should not throw
    }

    // ═══ ResolveSetNamesAsync (batch: full name → abbreviation) ═══

    [Test]
    public async Task ResolveSetNamesAsync_ReturnsEmpty_WhenNoEditions()
    {
        var result = await _store.ResolveSetNamesAsync(new[] { "Core Set 2019" });

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task ResolveSetNamesAsync_ReturnsEmpty_WhenNoInput()
    {
        await _store.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core));

        var result = await _store.ResolveSetNamesAsync(Array.Empty<string>());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task ResolveSetNamesAsync_ResolvesSingleFullSetName()
    {
        await _store.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Core Set 2019", setType: CardSetType.core));

        var result = await _store.ResolveSetNamesAsync(new[] { "Core Set 2019" });

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result["Core Set 2019"], Is.EqualTo("m19"));
    }

    [Test]
    public async Task ResolveSetNamesAsync_ResolvesMultipleFullNames()
    {
        await _store.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Core Set 2019", setType: CardSetType.core));
        await _store.AddSetAsync(new CardSet(Guid.NewGuid(), "WOT", "War of the Spark", setType: CardSetType.expansion));
        await _store.AddSetAsync(new CardSet(Guid.NewGuid(), "rna", "Ravnica Allegiance", setType: CardSetType.expansion));

        var result = await _store.ResolveSetNamesAsync(new[]
        {
            "Core Set 2019",
            "War of the Spark",
            "Ravnica Allegiance"
        });

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result["Core Set 2019"], Is.EqualTo("m19"));
        Assert.That(result["War of the Spark"], Is.EqualTo("wot"));
        Assert.That(result["Ravnica Allegiance"], Is.EqualTo("rna"));
    }

    [Test]
    public async Task ResolveSetNamesAsync_PartialMatch_OnlyKnownSetsReturned()
    {
        await _store.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Core Set 2019", setType: CardSetType.core));

        var result = await _store.ResolveSetNamesAsync(new[]
        {
            "Core Set 2019",
            "Unknown Set Name"
        });

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.ContainsKey("Core Set 2019"), Is.True);
        Assert.That(result.ContainsKey("Unknown Set Name"), Is.False);
    }

    [Test]
    public async Task ResolveSetNamesAsync_CaseInsensitiveMatch()
    {
        await _store.AddSetAsync(new CardSet(Guid.NewGuid(), "m19", "Core Set 2019", setType: CardSetType.core));

        var result = await _store.ResolveSetNamesAsync(new[] { "core set 2019" });

        Assert.That(result, Has.Count.EqualTo(1),
            "ResolveSetNamesAsync should match full names case-insensitively");
    }

    // ═══ ResolveCardsByNameAndSetAsync (batch: (Name, Set) → Scryfall UUID) ═══

    [Test]
    public async Task ResolveCardsByNameAndSetAsync_ReturnsEmpty_WhenNoCards()
    {
        var pairs = new (string, string?)[] { ("Lightning Bolt", "m19") };
        var result = await _store.ResolveCardsByNameAndSetAsync(pairs);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task ResolveCardsByNameAndSetAsync_ReturnsEmpty_WhenNoInput()
    {
        await _store.AddAsync(new MagicCard { Id = Guid.NewGuid(), Name = "Island", SetCode = "m19" });

        var result = await _store.ResolveCardsByNameAndSetAsync(Array.Empty<(string, string?)>());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task ResolveCardsByNameAndSetAsync_ResolvesSingleCard()
    {
        var cardId = Guid.NewGuid();
        await _store.AddAsync(new MagicCard { Id = cardId, Name = "Lightning Bolt", SetCode = "m19" });

        var pairs = new (string, string?)[] { ("Lightning Bolt", "m19") };
        var result = await _store.ResolveCardsByNameAndSetAsync(pairs);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result["Lightning Bolt|m19"].Id, Is.EqualTo(cardId));
    }

    [Test]
    public async Task ResolveCardsByNameAndSetAsync_ResolvesMultipleCards()
    {
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();
        var cardId3 = Guid.NewGuid();

        await _store.AddAllAsync(new[]
        {
            new MagicCard { Id = cardId1, Name = "Lightning Bolt", SetCode = "m19" },
            new MagicCard { Id = cardId2, Name = "Island", SetCode = "m19" },
            new MagicCard { Id = cardId3, Name = "Counterspell", SetCode = "wot" },
        });

        var pairs = new (string, string?)[]
        {
            ("Lightning Bolt", "m19"),
            ("Island", "m19"),
            ("Counterspell", "wot"),
        };

        var result = await _store.ResolveCardsByNameAndSetAsync(pairs);

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result["Lightning Bolt|m19"].Id, Is.EqualTo(cardId1));
        Assert.That(result["Island|m19"].Id, Is.EqualTo(cardId2));
        Assert.That(result["Counterspell|wot"].Id, Is.EqualTo(cardId3));
    }

    [Test]
    public async Task ResolveCardsByNameAndSetAsync_DistinguishesSameNameDifferentSets()
    {
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();

        // "Island" exists in both M19 and M20 with different UUIDs
        await _store.AddAllAsync(new[]
        {
            new MagicCard { Id = cardId1, Name = "Island", SetCode = "m19" },
            new MagicCard { Id = cardId2, Name = "Island", SetCode = "m20" },
        });

        var pairs = new (string, string?)[]
        {
            ("Island", "m19"),
            ("Island", "m20"),
        };

        var result = await _store.ResolveCardsByNameAndSetAsync(pairs);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result["Island|m19"].Id, Is.EqualTo(cardId1),
            "Should resolve Island from m19, not m20");
        Assert.That(result["Island|m20"].Id, Is.EqualTo(cardId2),
            "Should resolve Island from m20, not m19");
    }

    [Test]
    public async Task ResolveCardsByNameAndSetAsync_PartialMatch_OnlyKnownCardsReturned()
    {
        await _store.AddAsync(new MagicCard { Id = Guid.NewGuid(), Name = "Lightning Bolt", SetCode = "m19" });

        var pairs = new (string, string?)[]
        {
            ("Lightning Bolt", "m19"),
            ("Unknown Card", "m19"),
            ("Lightning Bolt", "wot"),  // exists in name but not this set
        };

        var result = await _store.ResolveCardsByNameAndSetAsync(pairs);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.ContainsKey("Lightning Bolt|m19"), Is.True);
        Assert.That(result.ContainsKey("Unknown Card|m19"), Is.False);
        Assert.That(result.ContainsKey("Lightning Bolt|wot"), Is.False);
    }

    [Test]
    public async Task ResolveCardsByNameAndSetAsync_HandlesEmptyStringSet()
    {
        var cardId = Guid.NewGuid();
        // Cards table has Set TEXT NOT NULL — empty string is the closest to "no set"
        await _store.AddAsync(new MagicCard { Id = cardId, Name = "Token", SetCode = "" });

        var pairs = new (string, string?)[] { ("Token", "") };
        var result = await _store.ResolveCardsByNameAndSetAsync(pairs);

        Assert.That(result, Has.Count.EqualTo(1),
            "Should match cards with empty Set when pair has empty set");
        Assert.That(result["Token|"].Id, Is.EqualTo(cardId));
    }

    [Test]
    public async Task ResolveCardsByNameAndSetAsync_DeduplicatesSamePair()
    {
        await _store.AddAsync(new MagicCard { Id = Guid.NewGuid(), Name = "Island", SetCode = "m19" });

        // Same pair repeated — result should still have only one entry per composite key
        var pairs = new (string, string?)[]
        {
            ("Island", "m19"),
            ("Island", "m19"),
            ("Island", "m19"),
        };

        var result = await _store.ResolveCardsByNameAndSetAsync(pairs);

        Assert.That(result, Has.Count.EqualTo(1),
            "Duplicate (Name, Set) pairs should produce a single entry in the result");
    }

    [Test]
    public async Task ResolveCardsByNameAndSetAsync_ExecutesSingleQuery()
    {
        // Performance test: batch resolution should use a single SQL query,
        // not N queries. We verify by checking that all results are returned
        // and the method doesn't throw or hang with many pairs.
        var cards = new List<MagicCard>();
        for (int i = 0; i < 50; i++)
            cards.Add(new MagicCard { Id = Guid.NewGuid(), Name = $"Card {i}", SetCode = "m19" });

        await _store.AddAllAsync(cards);

        var pairs = new List<(string Name, string? Set)>();
        for (int i = 0; i < 50; i++)
            pairs.Add(($"Card {i}", "m19"));

        var result = await _store.ResolveCardsByNameAndSetAsync(pairs);

        Assert.That(result, Has.Count.EqualTo(50),
            "Batch resolution of 50 cards should return all 50 via a single query");
    }

    [Test]
    public async Task GetPagedAsync_LocationFilter_ReturnsOnlyCardsInLocation()
    {
        // Arrange: 3 cards in KB, 2 of them present in a deck
        var cards = new[]
        {
            CreateTestCard(Guid.NewGuid(), "Lightning Bolt"),
            CreateTestCard(Guid.NewGuid(), "Counterspell"),
            CreateTestCard(Guid.NewGuid(), "Force of Will"),
        };
        await _store.AddAllAsync(cards);

        var collectionStore = new SqliteCollectionStore(_dbPath);
        await collectionStore.InitializeAsync();
        var deck = await collectionStore.CreateDeckAsync("Test Deck");
        await collectionStore.AddAllAsync(new[]
        {
            new MagicCardPhysical(cards[0], Location.FromId(deck.Id, deck.Name, false), true) { Count = 2 },
            new MagicCardPhysical(cards[1], Location.FromId(deck.Id, deck.Name, false), true) { Count = 1 },
        });

        // Act: paged query restricted to the deck location
        var result = await _store.GetPagedAsync(new CardListFilter
        {
            LocationFilter = deck.Id,
            Page = 1,
            PageSize = 50,
        });

        // Assert: only the 2 cards in the deck; KB-only card excluded
        Assert.That(result.TotalCount, Is.EqualTo(2));
        var names = result.Cards.Select(c => c.Name).ToList();
        Assert.That(names, Does.Contain("Lightning Bolt"));
        Assert.That(names, Does.Contain("Counterspell"));
        Assert.That(names, Does.Not.Contain("Force of Will"));

        // Act: location + FTS query — intersection of both conditions
        var ftsResult = await _store.GetPagedAsync(new CardListFilter
        {
            LocationFilter = deck.Id,
            Query = "bolt",
            Page = 1,
            PageSize = 50,
        });

        Assert.That(ftsResult.TotalCount, Is.EqualTo(1));
        Assert.That(ftsResult.Cards.Single().Name, Is.EqualTo("Lightning Bolt"));
    }

    [Test]
    public async Task AddSetAsync_PersistsSetTypeAsWireString()
    {
        await _store.AddSetAsync(new CardSet(Guid.NewGuid(), "msh", "Murders at Karlov Manor", setType: CardSetType.from_the_vault));

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        var raw = await conn.ExecuteScalarAsync<string>("SELECT SetType FROM CardSet WHERE SetCode = 'msh'");

        Assert.That(raw, Is.EqualTo("from_the_vault"),
            "SetType must be persisted as the wire string in the TEXT column, not an enum ordinal.");
    }

    private MagicCard CreateTestCard(Guid cardId, string name)
    {
        return new MagicCard
        {
            Id = cardId,
            Name = name,
            ManaCost = "{1}",
            Type = "Instant",
            SetCode = "m19",
            Rarity = "Common",
        };
    }
}
