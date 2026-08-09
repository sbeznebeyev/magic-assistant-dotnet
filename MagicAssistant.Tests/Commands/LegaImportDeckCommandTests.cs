using MagicAssistant.Data.DbModel;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Tests.Commands;

[TestFixture]
public class LegacyImportDeckCommandTests
{
    // ── Helpers ────────────────────────────────────────────

    static string CreateXmlFile(string name, params (Guid cardId, string cardName, string cardSet)[] cards)
    {
        var path = Path.Combine(Path.GetTempPath(), $"old-import-{Guid.NewGuid()}.xml");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<cards>");
        sb.AppendLine($"<name>{name}</name>");
        sb.AppendLine("<list>");
        foreach (var (id, cn, ed) in cards)
        {
            sb.AppendLine("  <mcp>");
            sb.AppendLine("    <card>");
            sb.AppendLine($"      <id>{id}</id>");
            sb.AppendLine($"      <name>{cn}</name>");
            sb.AppendLine($"      <edition>{ed}</edition>");
            sb.AppendLine("    </card>");
            sb.AppendLine("    <count>1</count>");
            sb.AppendLine("  </mcp>");
        }
        sb.AppendLine("</list>");
        sb.AppendLine("</cards>");
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    // ── Tests ──────────────────────────────────────────────

    [Test]
    public async Task Handle_ConvertsFullSetNameToAbbreviation()
    {
        var store = new MockDbCardStore();
        store.SeedSets(
            new CardSet("m19", "Core Set 2019", null, "core"),
            new CardSet("WOT", "War of the Spark", null, "expansion")
        );

        var collectionStore = new MockCollectionStore();
        var cardStore = new MockCardStore<MagicCardPhysical>();
        var handler = new LegacyImportDeckCommandHandler(collectionStore, cardStore, store);

        var filePath = CreateXmlFile(
            "Test Deck",
            (Guid.NewGuid(), "Lightning Bolt", "Core Set 2019"),
            (Guid.NewGuid(), "Angel of Fury", "War of the Spark")
        );

        try
        {
            var result = await handler.Handle(new ImportDeckCommand(filePath), default);

            Assert.That(result.Success, Is.True);
            Assert.That(result.CardsImported, Is.EqualTo(2));

            // Verify set names were converted to abbreviations
            var importedCards = cardStore.ImportedCards;
            Assert.That(importedCards[0].Card.SetCode, Is.EqualTo("m19"),
                "Full set name 'Core Set 2019' should be converted to abbreviation 'm19'");
            Assert.That(importedCards[1].Card.SetCode, Is.EqualTo("wot"),
                "Full set name 'War of the Spark' should be converted to abbreviation 'wot'");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task Handle_ReplacesGathererIdWithScryfallUuid()
    {
        var store = new MockDbCardStore();
        store.SeedSets(
            new CardSet("m19", "Core Set 2019", null, "core")
        );
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();

        store.SeedCards(
            new MagicCard { Id = cardId1, Name = "Lightning Bolt", SetCode = "m19" },
            new MagicCard { Id = cardId2, Name = "Island", SetCode = "m19" }
        );

        var collectionStore = new MockCollectionStore();
        var cardStore = new MockCardStore<MagicCardPhysical>();
        var handler = new LegacyImportDeckCommandHandler(collectionStore, cardStore, store);

        var filePath = CreateXmlFile(
            "Test Deck",
            (Guid.NewGuid(), "Lightning Bolt", "Core Set 2019"),
            (Guid.NewGuid(), "Island", "Core Set 2019")
        );

        try
        {
            var result = await handler.Handle(new ImportDeckCommand(filePath), default);

            Assert.That(result.Success, Is.True);
            Assert.That(result.CardsImported, Is.EqualTo(2));

            var importedCards = cardStore.ImportedCards;
            Assert.That(importedCards[0].Card.Id, Is.EqualTo(cardId1),
                "Gatherer ID '449420' should be replaced with Scryfall UUID");
            Assert.That(importedCards[1].Card.Id, Is.EqualTo(cardId2),
                "Gatherer ID '449653' should be replaced with Scryfall UUID");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task Handle_KeepsOriginalId_WhenCardNotFoundInKB()
    {
        var store = new MockDbCardStore();
        store.SeedSets(
            new CardSet("m19", "Core Set 2019", null, "core")
        );
        // No cards seeded — KB is empty

        var collectionStore = new MockCollectionStore();
        var cardStore = new MockCardStore<MagicCardPhysical>();
        var handler = new LegacyImportDeckCommandHandler(collectionStore, cardStore, store);

        var filePath = CreateXmlFile(
            "Test Deck",
            (Guid.Empty, "Unknown Card", "Core Set 2019")
        );

        try
        {
            var result = await handler.Handle(new ImportDeckCommand(filePath), default);

            Assert.That(result.Success, Is.True);
            Assert.That(result.CardsImported, Is.EqualTo(1));

            // When card is not found in KB, original Gatherer ID should be preserved
            var importedCards = cardStore.ImportedCards;
            Assert.That(importedCards[0].Card.Id, Is.EqualTo(Guid.Empty),
                "When card is not found in KB, original Gatherer ID should be kept");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task Handle_KeepsOriginalEdition_WhenSetNotFoundInKB()
    {
        var store = new MockDbCardStore();
        var setName = "Unknown Set Name";
        // No editions seeded — KB is empty

        var collectionStore = new MockCollectionStore();
        var cardStore = new MockCardStore<MagicCardPhysical>();
        var handler = new LegacyImportDeckCommandHandler(collectionStore, cardStore, store);

        var filePath = CreateXmlFile(
            "Test Deck",
            (Guid.NewGuid(), "Lightning Bolt", setName)
        );

        try
        {
            var result = await handler.Handle(new ImportDeckCommand(filePath), default);

            Assert.That(result.Success, Is.True);
            Assert.That(result.CardsImported, Is.EqualTo(1));

            // When set is not found in KB, original full name should be preserved
            var importedCards = cardStore.ImportedCards;
            Assert.That(importedCards[0].Card.SetCode, Is.EqualTo(setName.ToLower()),
                "When set is not found in KB, original full name should be kept");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task Handle_ReturnsError_WhenNoCardsInFile()
    {
        var store = new MockDbCardStore();
        var collectionStore = new MockCollectionStore();
        var cardStore = new MockCardStore<MagicCardPhysical>();
        var handler = new LegacyImportDeckCommandHandler(collectionStore, cardStore, store);

        var path = Path.Combine(Path.GetTempPath(), $"old-import-empty-{Guid.NewGuid()}.xml");
        File.WriteAllText(path, @"<?xml version=""1.0"" encoding=""UTF-8""?>
<cards><name>Empty</name><list></list></cards>");

        try
        {
            var result = await handler.Handle(new ImportDeckCommand(path), default);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("No cards"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Handle_MatchesCardByNameAndSet()
    {
        // Same card name in different sets — should match by set abbreviation too
        var store = new MockDbCardStore();
        store.SeedSets(
            new CardSet("m19", "Core Set 2019", null, "core"),
            new CardSet("m20", "Core Set 2020", null, "core")
        );

        var cardId1 = Guid.NewGuid();

        store.SeedCards(
            new MagicCard { Id = cardId1, Name = "Island", SetCode = "m19" },
            new MagicCard { Id = Guid.NewGuid(), Name = "Island", SetCode = "m20" }
        );

        var collectionStore = new MockCollectionStore();
        var cardStore = new MockCardStore<MagicCardPhysical>();
        var handler = new LegacyImportDeckCommandHandler(collectionStore, cardStore, store);

        var filePath = CreateXmlFile(
            "Test Deck",
            (Guid.NewGuid(), "Island", "Core Set 2019")
        );

        try
        {
            var result = await handler.Handle(new ImportDeckCommand(filePath), default);

            Assert.That(result.Success, Is.True);
            var importedCards = cardStore.ImportedCards;
            Assert.That(importedCards[0].Card.Id, Is.EqualTo(cardId1),
                "Should match 'Island' from 'Core Set 2019' → M19, not M20");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task Handle_CreatesDeckWithImportedName()
    {
        var store = new MockDbCardStore();
        var collectionStore = new MockCollectionStore();
        var cardStore = new MockCardStore<MagicCardPhysical>();
        var handler = new LegacyImportDeckCommandHandler(collectionStore, cardStore, store);

        var filePath = CreateXmlFile(
            "My Imported Deck",
            (Guid.NewGuid(), "Some Card", "m19")
        );

        try
        {
            var result = await handler.Handle(new ImportDeckCommand(filePath), default);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Deck, Is.Not.Null);
            Assert.That(result.Deck!.Name, Is.EqualTo("My Imported Deck"));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task Handle_ImportsMultipleCards()
    {
        var store = new MockDbCardStore();
        store.SeedSets(
            new CardSet("m19", "Core Set 2019", null, "core")
        );

        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();
        var cardId3 = Guid.NewGuid();

        store.SeedCards(
            new MagicCard { Id = cardId1, Name = "Card A", SetCode = "m19" },
            new MagicCard { Id = cardId2, Name = "Card B", SetCode = "m19" },
            new MagicCard { Id = cardId3, Name = "Card C", SetCode = "m19" }
        );

        var collectionStore = new MockCollectionStore();
        var cardStore = new MockCardStore<MagicCardPhysical>();
        var handler = new LegacyImportDeckCommandHandler(collectionStore, cardStore, store);

        var filePath = CreateXmlFile(
            "Multi Card Deck",
            (Guid.NewGuid(), "Card A", "Core Set 2019"),
            (Guid.NewGuid(), "Card B", "Core Set 2019"),
            (Guid.NewGuid(), "Card C", "Core Set 2019")
        );

        try
        {
            var result = await handler.Handle(new ImportDeckCommand(filePath), default);

            Assert.That(result.Success, Is.True);
            Assert.That(result.CardsImported, Is.EqualTo(3));

            var importedCards = cardStore.ImportedCards;
            Assert.That(importedCards[0].Card.Id, Is.EqualTo(cardId1));
            Assert.That(importedCards[1].Card.Id, Is.EqualTo(cardId2));
            Assert.That(importedCards[2].Card.Id, Is.EqualTo(cardId3));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    // ── Mocks ──────────────────────────────────────────────

    class MockDbCardStore : IDbCardStore
    {
        readonly List<CardSet> _editions = new();
        readonly List<MagicCard> _cards = new();

        public void SeedSets(params CardSet[] cardSets) => _editions.AddRange(cardSets);
        public void SeedCards(params MagicCard[] cards) => _cards.AddRange(cards);

        public async Task<ICollection<CardSet>> GetCardSetsAsync(CancellationToken ct = default)
            => await Task.FromResult(_editions as ICollection<CardSet>);

        public async Task<ICollection<MagicCard>> SearchAsync(string query, int limit = 100, CancellationToken ct = default)
        {
            // FTS5-like search: case-insensitive substring match on Name
            var q = query?.ToLowerInvariant() ?? string.Empty;
            var results = _cards.Where(c => c.Name != null && c.Name.ToLowerInvariant().Contains(q)).ToList();
            return await Task.FromResult(results.Take(limit).ToList() as ICollection<MagicCard>);
        }

        public async Task<ICollection<MagicCard>> GetBySetAsync(string setAbbreviation, CancellationToken ct = default)
        {
            var results = _cards.Where(c => c.SetCode == setAbbreviation).ToList();
            return await Task.FromResult(results as ICollection<MagicCard>);
        }

        public async Task<ICollection<MagicCard>> GetPrintingsAsync(string name, CancellationToken ct = default)
        {
            var results = _cards.Where(c => c.Name == name).ToList();
            return await Task.FromResult(results as ICollection<MagicCard>);
        }

        // Stub implementations — not used in these tests
        public Task<bool> AddAsync(MagicCard card, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> AddAllAsync(IEnumerable<MagicCard> cards, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RemoveAsync(MagicCard card, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> RemoveAllAsync(IEnumerable<MagicCard> cards, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> UpdateAsync(MagicCard card, ICollection<MagicCardField> fields, CancellationToken ct = default) => Task.FromResult(true);
        public Task<MagicCard?> GetCardAsync(Guid cardId, CancellationToken ct = default) => Task.FromResult<MagicCard?>(null);
        public Task<ICollection<MagicCard>> GetCardsAsync(Guid cardId, CancellationToken ct = default) => Task.FromResult(Array.Empty<MagicCard>() as ICollection<MagicCard>);
        public Task<int> TotalUniqueCardsAsync(CancellationToken ct = default) => Task.FromResult(_cards.Count);
        public Task<IEnumerable<MagicCard>> GetAllDeckCardsAsync(CancellationToken ct = default) => Task.FromResult(_cards.AsEnumerable());
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task AddSetAsync(CardSet edition, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAllFieldsAsync(ICollection<MagicCardField> fields, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateCardPropertyAsync(Guid cardId, string columnName, string? value, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Dictionary<Guid, MagicCard>> GetCardsByIdsAsync(ICollection<Guid> ids, CancellationToken ct = default) => Task.FromResult(new Dictionary<Guid, MagicCard>());
        public Task<ICollection<CollectorProgressRaw>> GetCollectorProgressRawAsync(CancellationToken ct = default) => Task.FromResult(Array.Empty<CollectorProgressRaw>() as ICollection<CollectorProgressRaw>);

        public Task<Dictionary<string, string>> ResolveSetNamesAsync(ICollection<string> fullNames, CancellationToken ct = default)
            => Task.FromResult(_editions
                .Where(e => fullNames.Contains(e.Name, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(e => e.Name, e => e.SetCode, StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, MagicCard>> ResolveCardsByNameAndSetAsync(
            ICollection<(string Name, string? Set)> pairs, CancellationToken ct = default)
        {
            var result = new Dictionary<string, MagicCard>();
            foreach (var (name, set) in pairs)
            {
                var key = $"{name}|{(set ?? "")}";
                var match = _cards.FirstOrDefault(c => c.Name == name && (set == null || c.SetCode == set));
                if (match != null && !result.ContainsKey(key))
                    result[key] = match;
            }
            return Task.FromResult(result);
        }

        // Missing IDbCardStore methods
        public Task<CardSet?> GetCardSetAsync(string setCode, CancellationToken ct = default)
            => Task.FromResult(_editions.FirstOrDefault(e => e.SetCode == setCode));
        public Task<int> BulkUpsertSetsAsync(CardSet[] sets, CancellationToken ct = default)
        {
            _editions.Clear();
            _editions.AddRange(sets);
            return Task.FromResult(sets.Length);
        }
        public Task<int> BulkUpdateSetsAsync(CardSet[] sets, CancellationToken ct = default)
        {
            // CardSet is immutable; replace existing entries with updated copies
            foreach (var set in sets)
            {
                var idx = _editions.FindIndex(e => e.SetCode == set.SetCode);
                if (idx >= 0)
                {
                    _editions[idx] = set;
                }
            }
            return Task.FromResult(sets.Length);
        }
        public Task<int> BulkInsertNewSetsAsync(CardSet[] sets, CancellationToken ct = default)
        {
            var count = 0;
            foreach (var set in sets)
            {
                if (!_editions.Any(e => e.SetCode == set.SetCode))
                {
                    _editions.Add(set);
                    count++;
                }
            }
            return Task.FromResult(count);
        }
        public Task<(string Language, Guid Id)[]> GetAvailableLanguagesAsync(Guid oracleId, CancellationToken ct = default)
            => Task.FromResult(Array.Empty<(string Language, Guid Id)>());
        public Task<CardListResult> GetPagedAsync(CardListFilter filter, CancellationToken ct = default)
            => Task.FromResult(new CardListResult(_cards.Take(filter.PageSize).Skip(filter.Offset).ToList(), _cards.Count));
    }

    class MockCollectionStore : ICollectionStore
    {
        readonly List<Collection> _nodes = new();
        int _nextId = 1;

        public async Task<Collection> CreateDeckAsync(string name, int? parentId = null, CancellationToken ct = default)
        {
            var id = _nextId++;
            var node = new Collection(id, name, parentId, NodeType.Deck, 0);
            _nodes.Add(node);
            return await Task.FromResult(node);
        }

        public Task<ICollection<Collection>> GetBranchAsync(int collectionId, CancellationToken ct = default)
            => Task.FromResult((ICollection<Collection>)_nodes);
        public Task<Collection?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(_nodes.FirstOrDefault(n => n.Id == id));
        public Task<bool> DeleteAsync(int id, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RenameAsync(int id, string newName, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IEnumerable<Collection>> GetChildrenAsync(int? parentId, CancellationToken ct = default) => Task.FromResult(_nodes.AsEnumerable());
        public Task<ICollection<MagicCardPhysical>> GetDeckCardsAsync(int deckId, CancellationToken ct = default) => Task.FromResult(Array.Empty<MagicCardPhysical>() as ICollection<MagicCardPhysical>);
        public Task<ICollection<MagicCardPhysical>> GetDeckWithSideboardAsync(int deckId, CancellationToken ct = default) => Task.FromResult(Array.Empty<MagicCardPhysical>() as ICollection<MagicCardPhysical>);
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<Dictionary<Guid, OwnCountStats>> GetOwnCountStatsAsync(CancellationToken ct = default) => Task.FromResult(new Dictionary<Guid, OwnCountStats>());
        public Task<Collection> CreateCollectionAsync(string name, int? parentId, CancellationToken ct = default)
        {
            var id = _nextId++;
            return Task.FromResult(new Collection(id, name, parentId, NodeType.Folder, 0));
        }
        public Task<ICollection<MagicCardPhysical>> GetAllPhysicalCardsAsync(CancellationToken ct = default)
            => Task.FromResult(Array.Empty<MagicCardPhysical>() as ICollection<MagicCardPhysical>);
        public Task<bool> AddAsync(MagicCardPhysical card, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> AddAllAsync(IEnumerable<MagicCardPhysical> cards, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RemoveAsync(MagicCardPhysical card, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> UpdateAsync(MagicCardPhysical card, ICollection<MagicCardField> fields, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ICollection<MagicCardPhysical>> GetCardsAsync(Guid cardId, CancellationToken ct = default)
            => Task.FromResult(Array.Empty<MagicCardPhysical>() as ICollection<MagicCardPhysical>);
        public Task<IEnumerable<MagicCardPhysical>> GetAllDeckCardsAsync(CancellationToken ct = default)
            => Task.FromResult(Array.Empty<MagicCardPhysical>().AsEnumerable());
        public Task<ICollection<Collection>> GetAllCollectionsAsync(CancellationToken ct = default)
            => Task.FromResult((ICollection<Collection>)_nodes);
        public Task<bool> LockAsync(int id, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> UnlockAsync(int id, CancellationToken ct = default) => Task.FromResult(true);
        public Task<Collection> CreateFolderAsync(string name, int? parentId, CancellationToken ct = default)
        {
            var id = _nextId++;
            return Task.FromResult(new Collection(id, name, parentId, NodeType.Folder, 0));
        }
    }

    class MockCardStore<T> : ICardStore<T> where T : IMagicCard
    {
        public List<T> ImportedCards { get; } = new();

        public Task<bool> AddAsync(T card, CancellationToken ct = default)
        {
            ImportedCards.Add(card);
            return Task.FromResult(true);
        }

        public Task<bool> AddAllAsync(IEnumerable<T> cards, CancellationToken ct = default)
        {
            ImportedCards.AddRange(cards);
            return Task.FromResult(true);
        }

        public Task<bool> RemoveAsync(T card, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> RemoveAllAsync(IEnumerable<T> cards, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> UpdateAsync(T card, ICollection<MagicCardField> fields, CancellationToken ct = default) => Task.FromResult(true);
        public Task<T?> GetCardAsync(Guid cardId, CancellationToken ct = default) => Task.FromResult<T?>(default);
        public Task<ICollection<T>> GetCardsAsync(Guid cardId, CancellationToken ct = default) => Task.FromResult(Array.Empty<T>() as ICollection<T>);
        public Task<int> TotalUniqueCardsAsync(CancellationToken ct = default) => Task.FromResult(ImportedCards.Count);
        public Task<IEnumerable<T>> GetAllDeckCardsAsync(CancellationToken ct = default) => Task.FromResult(ImportedCards.AsEnumerable());
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
