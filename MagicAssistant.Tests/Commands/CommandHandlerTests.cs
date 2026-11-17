using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Tests.Commands;

[TestFixture]
public class CommandHandlerTests
{
    /* ─── AddCardsToDeck — добавление карт в колоду ───────── */

    [Test]
    public async Task AddCards_DeckNotFound_Throws()
    {
        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MyCard>>();
        coll.Setup(s => s.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var handler = new AddCardsToDeckCommandHandler(cardStore.Object, coll.Object);

        bool exceptionThrown = false;
        try
        {
            await handler.Handle(new AddCardsToDeckCommand(999, new[] { (ICard)new Card() }));
        }
        catch (InvalidOperationException ex)
        {
            exceptionThrown = true;
            Assert.That(ex.Message, Does.Contain("999"),
                "Сообщение исключения должно содержать ID отсутствующей колоды");
        }

        Assert.That(exceptionThrown, Is.True,
            "Ожидается InvalidOperationException при попытке добавить карты в несуществующую колоду");
    }

    [Test]
    public async Task AddCards_CreatesPhysicalsWithCorrectLocation()
    {
        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MyCard>>();
        coll.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection(1, "Deck", null, NodeType.Deck, 0));
        cardStore.Setup(s => s.AddAllAsync(It.IsAny<IEnumerable<MyCard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new AddCardsToDeckCommandHandler(cardStore.Object, coll.Object);
        await handler.Handle(new AddCardsToDeckCommand(1, new[] { (ICard)new Card { Id = Guid.NewGuid()} }));

        cardStore.Verify(s => s.AddAllAsync(
            It.Is<IEnumerable<MyCard>>(cards =>
                cards.Cast<MyCard>().All(p => p.Collection != null && p.Collection.Id == 1 && p.IsOwn)),
            It.IsAny<CancellationToken>()));
    }

    /* ─── RemoveCardsFromDeck — удаление карт ──────────────── */

    [Test]
    public async Task RemoveCards_DelegatesToStoreWithCardIds()
    {
        // Хендлер — чистый делегат: один вызов ICollectionStore.RemoveCardsAsync, результат — как есть.

        var coll = new Mock<ICollectionStore>();
        var cardId = Guid.NewGuid();
        coll.Setup(s => s.RemoveCardsAsync(1, It.IsAny<ICollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var handler = new RemoveCardsFromDeckCommandHandler(coll.Object);
        var result = await handler.Handle(new RemoveCardsFromDeckCommand(1, new[] { cardId }));

        Assert.That(result, Is.EqualTo(2), "Результат стора (SUM(Count) удалённых копий) возвращается как есть");
        coll.Verify(s => s.RemoveCardsAsync(
            1, It.Is<ICollection<Guid>>(ids => ids.Contains(cardId)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RemoveCards_NullCardIds_DelegatesWithEmptyList()
    {
        var coll = new Mock<ICollectionStore>();
        coll.Setup(s => s.RemoveCardsAsync(1, It.IsAny<ICollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new RemoveCardsFromDeckCommandHandler(coll.Object);
        await handler.Handle(new RemoveCardsFromDeckCommand(1, null!));

        coll.Verify(s => s.RemoveCardsAsync(
            1, It.Is<ICollection<Guid>>(ids => !ids.Any()), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RemoveCards_DeckNotFound_Throws()
    {
        var coll = new Mock<ICollectionStore>();
        coll.Setup(s => s.RemoveCardsAsync(999, It.IsAny<ICollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Deck '999' not found"));

        var handler = new RemoveCardsFromDeckCommandHandler(coll.Object);

        bool exceptionThrown = false;
        try
        {
            await handler.Handle(new RemoveCardsFromDeckCommand(999,
                new[] { Guid.NewGuid() }));
        }
        catch (InvalidOperationException ex)
        {
            exceptionThrown = true;
            Assert.That(ex.Message, Does.Contain("999"),
                "Сообщение исключения должно содержать ID отсутствующей колоды");
        }

        Assert.That(exceptionThrown, Is.True,
            "Ожидается InvalidOperationException при попытке удалить карты из несуществующей колоды");
    }

    [Test]
    public async Task ClearSideboard_DelegatesBySideboardNodeId()
    {
        // Чистка идёт по ID самого SB-узла (7), а не родительской колоды:
        // пустой CardIds = удалить все карты узла, сам узел остаётся.

        var coll = new Mock<ICollectionStore>();
        coll.Setup(s => s.RemoveCardsAsync(7, It.IsAny<ICollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var handler = new RemoveCardsFromDeckCommandHandler(coll.Object);
        var result = await handler.Handle(new RemoveCardsFromDeckCommand(7, Array.Empty<Guid>()));

        Assert.That(result, Is.EqualTo(5), "Все 5 копий из сайдборда (2+3) — результат стора");
        coll.Verify(s => s.RemoveCardsAsync(
            7, It.Is<ICollection<Guid>>(ids => !ids.Any()), It.IsAny<CancellationToken>()), Times.Once);
    }

    /* ─── MoveCards — перемещение между колодами ───────────── */

    [Test]
    public async Task MoveCards_PreservesOwnership()
    {
        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MyCard>>();
        var cardId = Guid.NewGuid();

        var physical = CreatePhysical(cardId, 1, 2, isOwn: false);

        coll.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection(1, "Source", null, NodeType.Deck, 0));
        coll.Setup(s => s.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection(2, "Target", null, NodeType.Deck, 0));
        coll.Setup(s => s.GetDeckCardsForMoveAsync(1, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { physical });
        cardStore.Setup(s => s.RemoveAsync(It.IsAny<MyCard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        cardStore.Setup(s => s.AddAsync(It.IsAny<MyCard>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new MoveCardsCommandHandler(cardStore.Object, coll.Object);
        await handler.Handle(new MoveCardsCommand(
            new[] { (ICard)new Card { Id = cardId } }, 1, 2));

        cardStore.Verify(s => s.AddAsync(
            It.Is<MyCard>(p => !p.IsOwn), It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task MoveCards_SourceNotFound_Throws()
    {
        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MyCard>>();
        coll.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var handler = new MoveCardsCommandHandler(cardStore.Object, coll.Object);

        bool exceptionThrown = false;
        try
        {
            await handler.Handle(new MoveCardsCommand(
                new[] { (ICard)new Card { Id = Guid.NewGuid() } }, 1, 2));
        }
        catch (InvalidOperationException ex)
        {
            exceptionThrown = true;
            Assert.That(ex.Message, Does.Contain("1"),
                "Сообщение исключения должно содержать ID отсутствующей source-колоды");
        }

        Assert.That(exceptionThrown, Is.True,
            "Ожидается InvalidOperationException когда source-колода не найдена");
    }

    [Test]
    public async Task MoveCards_TargetNotFound_Throws()
    {
        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MyCard>>();
        coll.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection(1, "Source", null, NodeType.Deck, 0));
        coll.Setup(s => s.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var handler = new MoveCardsCommandHandler(cardStore.Object, coll.Object);

        bool exceptionThrown = false;
        try
        {
            await handler.Handle(new MoveCardsCommand(
                new[] { (ICard)new Card { Id = Guid.NewGuid() } }, 1, 2));
        }
        catch (InvalidOperationException ex)
        {
            exceptionThrown = true;
            Assert.That(ex.Message, Does.Contain("2"),
                "Сообщение исключения должно содержать ID отсутствующей target-колоды");
        }

        Assert.That(exceptionThrown, Is.True,
            "Ожидается InvalidOperationException когда target-колода не найдена");
    }

    /* ─── UpdateCard — обновление свойств карты ────────────── */

    [Test]
    public async Task UpdateCard_UpdatesMultipleFields()
    {
        var cardStore = new Mock<ICardStore<MyCard>>();
        var cardId = Guid.NewGuid();

        var physical = CreatePhysical(cardId, 1, 1);

        ICollection<MagicCardField>? capturedFields = null;
        MyCard? capturedCard = null;

        cardStore.Setup(s => s.GetCardsAsync(cardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { physical });
        cardStore.Setup(s => s.UpdateAsync(
                It.IsAny<MyCard>(), It.IsAny<ICollection<MagicCardField>>(), It.IsAny<CancellationToken>()))
            .Returns((MyCard card, ICollection<MagicCardField> fields, CancellationToken ct) =>
            {
                capturedCard = card;
                capturedFields = fields;
                return Task.FromResult(true);
            });

        var handler = new UpdateCardCommandHandler(cardStore.Object);
        await handler.Handle(new UpdateCardCommand(1, cardId, new Dictionary<MagicCardField, object?>
        {
            [MagicCardField.Count] = 4,
            [MagicCardField.IsOwn] = false,
            [MagicCardField.MyPrice] = 5.5f,
            [MagicCardField.Comment] = "My comment",
        }));

        // Проверяем, что in-memory состояние обновлено
        Assert.That(physical.Count, Is.EqualTo(4));
        Assert.That(physical.IsOwn, Is.False);
        Assert.That(physical.MyPrice, Is.EqualTo(5.5f));
        Assert.That(physical.Comment, Is.EqualTo("My comment"));

        // КРИТИЧЕСКИ ВАЖНО: проверяем, что UpdateAsync был вызван с ВСЕМИ 4 полями
        // Без этой проверки тест не гарантирует, что все изменения будут персистированы в БД
        Assert.That(capturedFields, Is.Not.Null, "UpdateAsync должен был быть вызван");
        Assert.That(capturedFields!.Contains(MagicCardField.Count), Is.True,
            "BUG: поле Count не передано в UpdateAsync — изменение количества не сохранится в БД");
        Assert.That(capturedFields!.Contains(MagicCardField.IsOwn), Is.True,
            "BUG: поле IsOwn не передано в UpdateAsync — изменение ownership не сохранится в БД");
        Assert.That(capturedFields!.Contains(MagicCardField.MyPrice), Is.True,
            "BUG: поле Price не передано в UpdateAsync — изменение цены не сохранится в БД");
        Assert.That(capturedFields!.Contains(MagicCardField.Comment), Is.True,
            "BUG: поле Comment не передано в UpdateAsync — комментарий не сохранится в БД");
    }

    [Test]
    public async Task UpdateCard_CardNotFound_ReturnsFalse()
    {
        var cardStore = new Mock<ICardStore<MyCard>>();
        var cardId = Guid.NewGuid();

        cardStore.Setup(s => s.GetCardsAsync(cardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MyCard>());

        var handler = new UpdateCardCommandHandler(cardStore.Object);
        var result = await handler.Handle(new UpdateCardCommand(1, cardId, new Dictionary<MagicCardField, object?>()));

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task UpdateCard_WrongDeck_ReturnsFalse()
    {
        var cardStore = new Mock<ICardStore<MyCard>>();
        var cardId = Guid.NewGuid();

        // Карта есть, но в другой колоде (deck-2), а обновление запрашивается для deck-1
        var physical = CreatePhysical(cardId, 2, 1);

        cardStore.Setup(s => s.GetCardsAsync(cardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { physical });

        var handler = new UpdateCardCommandHandler(cardStore.Object);
        var result = await handler.Handle(new UpdateCardCommand(1, cardId, new Dictionary<MagicCardField, object?> { [MagicCardField.Count] = 5 }));

        Assert.That(result, Is.False,
            "Хендлер ищет карту по CardId И Location.Id. Карта в deck-2, запрос для deck-1 → не найдена");
    }

    private static MyCard CreatePhysical(Guid cardId, int deckId, int count = 1, bool isOwn = true)
    {
        var card = new Card { Id = cardId, Name = "Test" };
        return new MyCard(card, new Collection(deckId, string.Empty, null, NodeType.Deck, 0), isOwn) { Count = count };
    }
}
