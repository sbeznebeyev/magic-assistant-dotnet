using Microsoft.Extensions.Logging;
using MagicAssistant.Core;
using MagicAssistant.Data.DbModel;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Tests.Services;

[TestFixture]
public class ServiceTests
{
    private static MagicCardPhysical CreatePhysical(Guid cardId, int deckId, int count = 1, bool isOwn = true)
    {
        var card = new MagicCard { Id = cardId, Name = "Test" };
        return new MagicCardPhysical(card, Location.FromId(deckId), isOwn) { Count = count };
    }

    private static ScryfallSetDto SetDto(string code, string name, int? cardCount = 269)
        => new(
            Id: Guid.NewGuid(),
            Code: code,
            Name: name,
            Uri: $"https://api.scryfall.com/sets/{code}",
            ScryfallUri: $"https://scryfall.com/sets/{code}",
            SearchUri: $"https://api.scryfall.com/cards/search?q=e%3A{code}",
            ReleasedAt: "2020-01-01",
            SetType: "core",
            CardCount: cardCount ?? 0,
            ParentSetCode: null,
            Digital: false,
            NonfoilOnly: false,
            FoilOnly: false,
            IconSvgUri: $"https://svgs.scryfall.io/sets/{code}.svg");

    /* ═══════════════════════════════════════════════════════
     * DeckManagementService — CRUD колод и папок
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task DeckManagement_CreateNode_ValidatesAndTrimsName()
    {
        var store = new Mock<ICollectionStore>();
        store.Setup(s => s.CreateDeckAsync("Trimmed", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection(1, "Trimmed", null, NodeType.Deck, 0));

        var service = CreateDeckManagementService(store.Object);

        // Async methods require Assert.ThrowsAsync or await exception handling
        Assert.That(() => service.CreateNodeAsync(null, "", NodeType.Deck), Throws.InstanceOf<ArgumentException>());
        Assert.That(() => service.CreateNodeAsync(null, null!, NodeType.Deck), Throws.InstanceOf<ArgumentException>());
        Assert.That(() => service.CreateNodeAsync(null, "   ", NodeType.Deck), Throws.InstanceOf<ArgumentException>());

        await service.CreateNodeAsync(null, "  Trimmed  ", NodeType.Deck);
        store.Verify(s => s.CreateDeckAsync("Trimmed", null, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task DeckManagement_RenameAsync_ValidatesAndTrims()
    {
        var store = new Mock<ICollectionStore>();
        store.Setup(s => s.RenameAsync(1, "New", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateDeckManagementService(store.Object);
        Assert.That(() => service.RenameAsync(0, "Name"), Throws.InstanceOf<ArgumentException>());
        Assert.That(() => service.RenameAsync(1, ""), Throws.InstanceOf<ArgumentException>());

        await service.RenameAsync(1, "  New  ");
        store.Verify(s => s.RenameAsync(1, "New", It.IsAny<CancellationToken>()));
    }

    private static DeckManagementService CreateDeckManagementService(ICollectionStore store)
        => new(
            new CreateNodeCommandHandler(store),
            new DeleteNodeCommandHandler(store),
            new RenameNodeCommandHandler(store),
            new LockNodeCommandHandler(store),
            new UnlockNodeCommandHandler(store),
            new GetNodesTreeQueryHandler(store),
            new GetNodeByIdQueryHandler(store),
            new GetNodesBranchQueryHandler(store));

    /* ═══════════════════════════════════════════════════════
     * DeckCardService — операции с картами в колоде
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task DeckCard_SplitCard_ReturnsNull_WhenNoSplitNeeded()
    {
        var cardId = Guid.NewGuid();
        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        // Multiple cards in same deck — service must match by CardId, not just Location.Id
        var physicalTarget = CreatePhysical(cardId, 1, 4);
        var physicalOther = CreatePhysical(Guid.NewGuid(), 1, 3);

        coll.Setup(s => s.GetCardsAsync(cardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MagicCardPhysical[] { physicalTarget, physicalOther });
        cardStore.Setup(s => s.UpdateAsync(It.IsAny<MagicCardPhysical>(), It.IsAny<ICollection<MagicCardField>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new DeckCardService(coll.Object, cardStore.Object, new Mock<ICardResolveService>().Object,
            new RemoveCardsFromDeckCommandHandler(coll.Object));

        var result0 = await service.SplitCardAsync(1, cardId, 0);
        Assert.That(result0, Is.Null, "rightCount=0 → nothing to split, must return null");

        // physicalTarget.Count is still 4 (first call returned early without modification)
        var result4 = await service.SplitCardAsync(1, cardId, 4);
        Assert.That(result4, Is.Null, "rightCount=4 == total → nothing to split, must return null");

        // Verify the OTHER card was NOT touched (service matched by CardId, not just Location)
        Assert.That(physicalOther.Count, Is.EqualTo(3),
            "Service must filter by CardId; other card in same deck must remain untouched");
    }

    [Test]
    public async Task DeckCard_SplitCard_ReturnsRightPart()
    {
        var cardId = Guid.NewGuid();
        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        var physicalTarget = CreatePhysical(cardId, 1, 4);
        var physicalOther = CreatePhysical(Guid.NewGuid(), 1, 3);

        coll.Setup(s => s.GetCardsAsync(cardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MagicCardPhysical[] { physicalTarget, physicalOther });
        cardStore.Setup(s => s.UpdateAsync(It.IsAny<MagicCardPhysical>(), It.IsAny<ICollection<MagicCardField>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        cardStore.Setup(s => s.AddAsync(It.IsAny<MagicCardPhysical>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new DeckCardService(coll.Object, cardStore.Object, new Mock<ICardResolveService>().Object,
            new RemoveCardsFromDeckCommandHandler(coll.Object));
        var result = await service.SplitCardAsync(1, cardId, 2);
        Assert.That(result, Is.Not.Null, "Splitting 4→(2+2) must return the right part");
        Assert.That(result!.Count, Is.EqualTo(2), "Right part must have count=2");
        Assert.That(result.Card.Id, Is.EqualTo(cardId), "Right part must reference the correct card");
        Assert.That(physicalTarget.Count, Is.EqualTo(2),
            "Original card must be reduced to leftCount (4-2=2)");
        Assert.That(physicalOther.Count, Is.EqualTo(3),
            "Service must filter by CardId; other card in same deck must remain untouched");
    }

    /* ═══════════════════════════════════════════════════════
     * CardResolveService — разрешение карт и групп
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public void CardResolve_ExpandGroups_ReturnsSameList_ForNonGroupCards()
    {
        var service = new CardResolveService(new Mock<IDbCardStore>().Object, new Mock<ICollectionStore>().Object);
        var cards = new List<IMagicCard>
        {
            new MagicCard { Id = Guid.Parse("00000000-0000-0000-0000-000000000001") },
            new MagicCard { Id = Guid.Parse("00000000-0000-0000-0000-000000000002") },
        };

        var result = service.ExpandGroups(cards);
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public void CardResolve_GetLocation_ReturnsCommonOrNull()
    {
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();

        var service = new CardResolveService(new Mock<IDbCardStore>().Object, new Mock<ICollectionStore>().Object);

        var loc1 = Location.FromPath("deck-1");
        var cards = new List<IMagicCard>
        {
            CreatePhysical(cardId1, loc1),
            CreatePhysical(cardId2, loc1),
        };
        Assert.That(service.GetLocation(cards)?.Id, Is.EqualTo("deck-1"));

        // Different locations → null
        var diffCards = new List<IMagicCard>
        {
            CreatePhysical(cardId1, Location.FromId(10)),
            CreatePhysical(cardId2, Location.FromId(20)),
        };
        Assert.That(service.GetLocation(diffCards), Is.Null);
    }

    private static MagicCardPhysical CreatePhysical(Guid cardId, Location location)
    {
        var card = new MagicCard { Id = cardId, Name = "Test" };
        return new MagicCardPhysical(card, location, true) { Count = 1 };
    }

    /* ═══════════════════════════════════════════════════════
     * CardSearchService — поиск и фильтрация
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task CardSearch_SearchAsync_TrimsAndValidatesQuery()
    {
        var db = new Mock<IDbCardStore>();
        db.Setup(s => s.SearchAsync("trimmed", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MagicCard>());

        var service = new CardSearchService(db.Object, new Mock<ICollectionStore>().Object);

        Assert.That(await service.SearchAsync(""), Is.Empty);
        Assert.That(await service.SearchAsync(null!), Is.Empty);
        Assert.That(await service.SearchAsync("  "), Is.Empty);
        await service.SearchAsync("  trimmed  ");
        db.Verify(s => s.SearchAsync("trimmed", 100, It.IsAny<CancellationToken>()));
    }

    [Test]
    public void CardSearch_ApplyFilter_HandlesNulls()
    {
        var service = new CardSearchService(new Mock<IDbCardStore>().Object, new Mock<ICollectionStore>().Object);
        Assert.That(service.ApplyFilter(null!, new MagicCardFilter()), Is.Empty);
        Assert.That(service.ApplyFilter(Array.Empty<IMagicCard>(), new MagicCardFilter()), Is.Empty);

        var cards = new[] { (IMagicCard)new MagicCard() };
        Assert.That(service.ApplyFilter(cards, null!), Has.Count.EqualTo(1));
    }

    /* ═══════════════════════════════════════════════════════
     * DeckAnalysisService — анализ колод и прогресс коллекционера
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task DeckAnalysis_CollectorProgress_CalculatesPercent()
    {
        var db = new Mock<IDbCardStore>();
        var coll = new Mock<ICollectionStore>();
        db.Setup(s => s.GetCollectorProgressRawAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CollectorProgressRaw>
            {
                new("m19", null, 269, 135, 80),
                new("NEW", null, 0, 0, 0),
            });

        var service = new DeckAnalysisService(coll.Object, db.Object, new Mock<IAnalysisService>().Object);
        var result = await service.GetCollectorProgressAsync();

        Assert.That(result.First(p => p.SetAbbreviation == "m19").ProgressPercent, Is.GreaterThan(50));
        Assert.That(result.First(p => p.SetAbbreviation == "NEW").ProgressPercent, Is.EqualTo(0));
    }

    /* ═══════════════════════════════════════════════════════
     * KnowledgeBaseSyncService — синхронизация с Scryfall
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task KBSync_SetNotFound_ReturnsFailure()
    {
        var scryfall = new Mock<IScrayfallClient>();
        scryfall.Setup(s => s.GetSetsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScryfallSetDto>());

        var registry = new SyncSetStateRegistry();
        var logger = new Mock<ILogger<KnowledgeBaseSyncService>>().Object;
        var service = new KnowledgeBaseSyncService(scryfall.Object, new Mock<IDbCardStore>().Object,
            new Mock<IImageCacheService>().Object, registry, logger);

        // Pass at least one language — empty languages array causes early return without error
        var result = await service.SyncSetAsync("NONEXIST", ["en"], downloadImages: false);
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessages, Is.Not.Empty);
    }

    [Test]
    public async Task KBSync_Success_RegistersStateAndPersistsCards()
    {
        var scryfall = new Mock<IScrayfallClient>();
        var kb = new Mock<IDbCardStore>();
        var images = new Mock<IImageCacheService>();
        var registry = new SyncSetStateRegistry();

        var cards = Enumerable.Range(1, 5)
            .Select(i => new ScryfallCardDto { Id = Guid.NewGuid(), Name = $"Card {i}", Language = "en" })
            .ToList();

        scryfall.Setup(s => s.GetSetsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { SetDto("m19", "Magic 2019", 5) });
        scryfall.Setup(s => s.GetCardsAsync("m19", It.IsAny<bool>(), It.IsAny<Action<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cards);
        kb.Setup(s => s.AddAllAsync(It.IsAny<IEnumerable<MagicCard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var logger = new Mock<ILogger<KnowledgeBaseSyncService>>().Object;
        var service = new KnowledgeBaseSyncService(scryfall.Object, kb.Object, images.Object, registry, logger);
        // Pass at least one language — empty languages array causes early return without processing
        var result = await service.SyncSetAsync("m19", ["en"], downloadImages: false);

        Assert.That(result.Success, Is.True);
        Assert.That(result.SetName, Is.EqualTo("Magic 2019"));
        Assert.That(result.CardsDownloaded, Is.EqualTo(5));
        Assert.That(registry.Get("m19")!.Completed, Is.True);
    }

    [Test]
    public async Task KBSync_Cancellation_ReturnsFailure()
    {
        var scryfall = new Mock<IScrayfallClient>();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        scryfall.Setup(s => s.GetSetsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var registry = new SyncSetStateRegistry();
        var logger = new Mock<ILogger<KnowledgeBaseSyncService>>().Object;
        var service = new KnowledgeBaseSyncService(scryfall.Object, new Mock<IDbCardStore>().Object,
            new Mock<IImageCacheService>().Object, registry, logger);

        var result = await service.SyncSetAsync("m19", Array.Empty<string>(), downloadImages: false, cts.Token);
        Assert.That(result.Success, Is.False);
    }

}
