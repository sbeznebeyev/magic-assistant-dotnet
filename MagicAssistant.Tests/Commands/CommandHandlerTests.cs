using MagicAssistant.Data.DbModel;
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
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        coll.Setup(s => s.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var handler = new AddCardsToDeckCommandHandler(cardStore.Object, coll.Object);

        bool exceptionThrown = false;
        try
        {
            await handler.Handle(new AddCardsToDeckCommand(999, new[] { (IMagicCard)new MagicCard() }));
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
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        coll.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection(1, "Deck", null, NodeType.Deck, 0));
        cardStore.Setup(s => s.AddAllAsync(It.IsAny<IEnumerable<MagicCardPhysical>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new AddCardsToDeckCommandHandler(cardStore.Object, coll.Object);
        await handler.Handle(new AddCardsToDeckCommand(1, new[] { (IMagicCard)new MagicCard { Id = Guid.NewGuid()} }));

        cardStore.Verify(s => s.AddAllAsync(
            It.Is<IEnumerable<MagicCardPhysical>>(cards =>
                cards.Cast<MagicCardPhysical>().All(p => p.Location.Id == "1" && p.IsOwn)),
            It.IsAny<CancellationToken>()));
    }

    /* ─── RemoveCardsFromDeck — удаление карт ──────────────── */

    [Test]
    public async Task RemoveCards_RemovesAvailableCards()
    {
        // BUG в исходном коде: RemoveCardsFromDeckCommand принимает ICollection<IMagicCard>,
        // где IMagicCard.Count для MagicCard всегда возвращает 1 (hardcoded в ICard реализации).
        // Хендлер использует Math.Min(instance.Count, card.Count) для определения количества на удаление.
        // Это означает, что при удалении через MagicCard (не MagicCardPhysical) 
        // ВСЕГДА удаляется максимум 1 копия, независимо от того сколько копий в колоде.
        // Для удаления N копий необходимо передать N отдельных MagicCard объектов или использовать MagicCardPhysical.
        // Тест документирует эту семантику: 1 MagicCard → удаление 1 копии из доступных.

        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        var cardId = Guid.NewGuid();
        var physical = CreatePhysical(cardId, 1, 2);

        coll.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection(1, "Deck", null, NodeType.Deck, 0));
        coll.Setup(s => s.GetDeckCardsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { physical });
        cardStore.Setup(s => s.RemoveAsync(It.IsAny<MagicCardPhysical>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new RemoveCardsFromDeckCommandHandler(cardStore.Object, coll.Object);
        var result = await handler.Handle(new RemoveCardsFromDeckCommand(1,
            new[] { (IMagicCard)new MagicCard { Id = cardId } }));

        // MagicCard.Count всегда = 1 → Math.Min(2, 1) = 1
        Assert.That(result, Is.EqualTo(1),
            "Ожидалось удаление ровно 1 копии: MagicCard.Count всегда возвращает 1, " +
            "поэтому Math.Min(instance.Count=2, card.Count=1) = 1. " +
            "BUG: для удаления всех копий карты нужно передать MagicCardPhysical с нужным Count.");

        // Убедимся, что RemoveAsync был вызван с count=1
        cardStore.Verify(s => s.RemoveAsync(
            It.Is<MagicCardPhysical>(p => p.Count == 1),
            It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task RemoveCards_DeckNotFound_Throws()
    {
        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        coll.Setup(s => s.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var handler = new RemoveCardsFromDeckCommandHandler(cardStore.Object, coll.Object);

        bool exceptionThrown = false;
        try
        {
            await handler.Handle(new RemoveCardsFromDeckCommand(999,
                new[] { (IMagicCard)new MagicCard { Id = Guid.NewGuid() } }));
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
    public async Task RemoveCards_CardNotInDeck_ReturnsZero()
    {
        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        var physical = CreatePhysical(Guid.NewGuid(), 1, 1);

        coll.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection(1, "Deck", null, NodeType.Deck, 0));
        coll.Setup(s => s.GetDeckCardsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { physical });

        var handler = new RemoveCardsFromDeckCommandHandler(cardStore.Object, coll.Object);
        var result = await handler.Handle(new RemoveCardsFromDeckCommand(1,
            new[] { (IMagicCard)new MagicCard { Id = Guid.NewGuid() } }));

        Assert.That(result, Is.EqualTo(0), "Карта не найдена в колоде — ничего не должно быть удалено");

        // RemoveAsync НЕ должен был быть вызван
        cardStore.Verify(s => s.RemoveAsync(It.IsAny<MagicCardPhysical>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /* ─── MoveCards — перемещение между колодами ───────────── */

    [Test]
    public async Task MoveCards_PreservesOwnership()
    {
        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        var cardId = Guid.NewGuid();

        var physical = CreatePhysical(cardId, 1, 2, isOwn: false);

        coll.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection(1, "Source", null, NodeType.Deck, 0));
        coll.Setup(s => s.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection(2, "Target", null, NodeType.Deck, 0));
        coll.Setup(s => s.GetDeckCardsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { physical });
        cardStore.Setup(s => s.RemoveAsync(It.IsAny<MagicCardPhysical>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        cardStore.Setup(s => s.AddAsync(It.IsAny<MagicCardPhysical>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new MoveCardsCommandHandler(cardStore.Object, coll.Object);
        await handler.Handle(new MoveCardsCommand(
            new[] { (IMagicCard)new MagicCard { Id = cardId } }, 1, 2));

        cardStore.Verify(s => s.AddAsync(
            It.Is<MagicCardPhysical>(p => !p.IsOwn), It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task MoveCards_SourceNotFound_Throws()
    {
        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        coll.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var handler = new MoveCardsCommandHandler(cardStore.Object, coll.Object);

        bool exceptionThrown = false;
        try
        {
            await handler.Handle(new MoveCardsCommand(
                new[] { (IMagicCard)new MagicCard { Id = Guid.NewGuid() } }, 1, 2));
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
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        coll.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Collection(1, "Source", null, NodeType.Deck, 0));
        coll.Setup(s => s.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var handler = new MoveCardsCommandHandler(cardStore.Object, coll.Object);

        bool exceptionThrown = false;
        try
        {
            await handler.Handle(new MoveCardsCommand(
                new[] { (IMagicCard)new MagicCard { Id = Guid.NewGuid() } }, 1, 2));
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
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        var cardId = Guid.NewGuid();

        var physical = CreatePhysical(cardId, 1, 1);

        ICollection<MagicCardField>? capturedFields = null;
        MagicCardPhysical? capturedCard = null;

        cardStore.Setup(s => s.GetCardsAsync(cardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { physical });
        cardStore.Setup(s => s.UpdateAsync(
                It.IsAny<MagicCardPhysical>(), It.IsAny<ICollection<MagicCardField>>(), It.IsAny<CancellationToken>()))
            .Returns((MagicCardPhysical card, ICollection<MagicCardField> fields, CancellationToken ct) =>
            {
                capturedCard = card;
                capturedFields = fields;
                return Task.FromResult(true);
            });

        var handler = new UpdateCardCommandHandler(cardStore.Object);
        await handler.Handle(new UpdateCardCommand("1", cardId, new Dictionary<MagicCardField, object?>
        {
            [MagicCardField.Count] = 4,
            [MagicCardField.IsOwn] = false,
            [MagicCardField.Price] = 5.5f,
            [MagicCardField.Comment] = "My comment",
        }));

        // Проверяем, что in-memory состояние обновлено
        Assert.That(physical.Count, Is.EqualTo(4));
        Assert.That(physical.IsOwn, Is.False);
        Assert.That(physical.Price, Is.EqualTo(5.5f));
        Assert.That(physical.Comment, Is.EqualTo("My comment"));

        // КРИТИЧЕСКИ ВАЖНО: проверяем, что UpdateAsync был вызван с ВСЕМИ 4 полями
        // Без этой проверки тест не гарантирует, что все изменения будут персистированы в БД
        Assert.That(capturedFields, Is.Not.Null, "UpdateAsync должен был быть вызван");
        Assert.That(capturedFields!.Contains(MagicCardField.Count), Is.True,
            "BUG: поле Count не передано в UpdateAsync — изменение количества не сохранится в БД");
        Assert.That(capturedFields!.Contains(MagicCardField.IsOwn), Is.True,
            "BUG: поле IsOwn не передано в UpdateAsync — изменение ownership не сохранится в БД");
        Assert.That(capturedFields!.Contains(MagicCardField.Price), Is.True,
            "BUG: поле Price не передано в UpdateAsync — изменение цены не сохранится в БД");
        Assert.That(capturedFields!.Contains(MagicCardField.Comment), Is.True,
            "BUG: поле Comment не передано в UpdateAsync — комментарий не сохранится в БД");
    }

    [Test]
    public async Task UpdateCard_CardNotFound_ReturnsFalse()
    {
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        var cardId = Guid.NewGuid();

        cardStore.Setup(s => s.GetCardsAsync(cardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MagicCardPhysical>());

        var handler = new UpdateCardCommandHandler(cardStore.Object);
        var result = await handler.Handle(new UpdateCardCommand("1", cardId, new Dictionary<MagicCardField, object?>()));

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task UpdateCard_WrongDeck_ReturnsFalse()
    {
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        var cardId = Guid.NewGuid();

        // Карта есть, но в другой колоде (deck-2), а обновление запрашивается для deck-1
        var physical = CreatePhysical(cardId, 2, 1);

        cardStore.Setup(s => s.GetCardsAsync(cardId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { physical });

        var handler = new UpdateCardCommandHandler(cardStore.Object);
        var result = await handler.Handle(new UpdateCardCommand("1", cardId, new Dictionary<MagicCardField, object?> { [MagicCardField.Count] = 5 }));

        Assert.That(result, Is.False,
            "Хендлер ищет карту по CardId И Location.Id. Карта в deck-2, запрос для deck-1 → не найдена");
    }

    /* ─── ImportDeck — импорт из XML ──────────────────────── */

    [Test]
    public async Task ImportDeck_ValidFile_ParsesAndCreatesDeck()
    {
        // Magic Assistant XML: <name> — дочерний элемент <cards>, НЕ атрибут <list>
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<cards>
  <name>Imported</name>
  <key>Decks/MyDecs/Imported</key>
  <comment></comment>
  <type>deck</type>
  <properties>
    <property name=""readonly"" value=""true""/>
    <property name=""virtual"" value=""false""/>
  </properties>
  <list>
    <mcp>
      <card>
        <id>7B584A4E-9F7F-427A-8D7B-A57EE2AFA96C</id>
        <name>Card1</name>
        <edition>m19</edition>
      </card>
      <count>1</count>
      <location>Decks/MyDecs/Imported</location>
      <ownership>true</ownership>
      <date>2018-03-17</date>
    </mcp>
    <mcp>
      <card>
        <id>FB1DEFD3-07B4-4064-8C73-9E9490757612</id>
        <name>Верный Пегас</name>
        <edition>m20</edition>
      </card>
      <count>3</count>
      <location>Decks/MyDecs/Imported</location>
      <ownership>true</ownership>
      <date>2020-07-20</date>
    </mcp>
  </list>
</cards>";
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, xml);

            var coll = new Mock<ICollectionStore>();
            var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
            coll.Setup(s => s.CreateDeckAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Collection(1, "Imported", null, NodeType.Deck, 0));
            cardStore.Setup(s => s.AddAllAsync(It.IsAny<IEnumerable<MagicCardPhysical>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new ImportDeckCommandHandler(coll.Object, cardStore.Object);
            var result = await handler.Handle(new ImportDeckCommand(tempFile));

            Assert.That(result.Success, Is.True, "Импорт должен завершиться успешно");
            Assert.That(result.Deck, Is.Not.Null, "Должна быть создана колода");
            Assert.That(result.CardsImported, Is.GreaterThan(0), "Должна быть импортирована хотя бы 1 карта");
            Assert.That(result.ErrorMessage, Is.Null, "Ошибок не должно быть");

            // Верифицируем, что колода была создана с правильным именем из XML
            coll.Verify(s => s.CreateDeckAsync(
                It.Is<string>(name => name == "Imported"),
                null,
                It.IsAny<CancellationToken>()));

            // Верифицируем, что карты были добавлены в store
            cardStore.Verify(s => s.AddAllAsync(
                It.Is<IEnumerable<MagicCardPhysical>>(cards => cards.Any()),
                It.IsAny<CancellationToken>()));
        }
        finally { File.Delete(tempFile); }
    }

    [Test]
    public async Task ImportDeck_NoCards_ReturnsError()
    {
        var xml = @"<?xml version=""1.0""?><cards><list name=""Empty""></list></cards>";
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, xml);
            var handler = new ImportDeckCommandHandler(new Mock<ICollectionStore>().Object,
                new Mock<ICardStore<MagicCardPhysical>>().Object);
            var result = await handler.Handle(new ImportDeckCommand(tempFile));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("No cards"));
        }
        finally { File.Delete(tempFile); }
    }

    /* ─── ExportDeck — экспорт в XML ──────────────────────── */

    [Test]
    public async Task ExportDeck_ValidDeck_CreatesXmlFile()
    {
        var coll = new Mock<ICollectionStore>();
        var cardStore = new Mock<ICardStore<MagicCardPhysical>>();
        var tempFile = Path.GetTempFileName();
        try
        {
            coll.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Collection(1, "My Deck", null, NodeType.Deck, 1));
            coll.Setup(s => s.GetDeckCardsAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { CreatePhysical(Guid.NewGuid(), 1) });

            var handler = new ExportDeckCommandHandler(coll.Object, cardStore.Object);
            var result = await handler.Handle(new ExportDeckCommand(1, tempFile));

            Assert.That(result.Success, Is.True);
            var xml = await File.ReadAllTextAsync(tempFile);
            Assert.That(xml, Does.Contain("My Deck"));
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Test]
    public async Task ExportDeck_DeckNotFound_ReturnsError()
    {
        var coll = new Mock<ICollectionStore>();
        coll.Setup(s => s.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Collection?)null);

        var handler = new ExportDeckCommandHandler(coll.Object, new Mock<ICardStore<MagicCardPhysical>>().Object);
        var result = await handler.Handle(new ExportDeckCommand(999, Path.GetTempFileName()));

        Assert.That(result.Success, Is.False);
    }

    private static MagicCardPhysical CreatePhysical(Guid cardId, int deckId, int count = 1, bool isOwn = true)
    {
        var card = new MagicCard { Id = cardId, Name = "Test" };
        return new MagicCardPhysical(card, Location.FromId(deckId), isOwn) { Count = count };
    }
}
