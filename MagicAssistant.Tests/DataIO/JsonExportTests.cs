using MagicAssistant.Data.Model.NavigatorNodes;
using MagicAssistant.Data.IO.json;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.DataIO;

[TestFixture]
public class JsonExportTests
{
    [Test]
    public void ExportToJson_ContainsReferenceCore()
    {
        var cardId = Guid.NewGuid();
        var card = new Card { Id = cardId, Name = "Card", SetCode = "m19" };
        var physical = new MyCard(card) { Count = 3 };

        var json = JsonExport.ExportToJson("Deck", null, "deck", new[] { physical });

        Assert.That(json, Does.Contain($"\"id\": \"{cardId}\""));
        Assert.That(json, Does.Contain("\"name\": \"Card\""));
        Assert.That(json, Does.Contain("\"set\": \"m19\""));
        Assert.That(json, Does.Contain("\"count\": 3"));
    }

    [Test]
    public void ExportToJson_CountOne_IsAlwaysWritten()
    {
        // Unlike the legacy XML format, count is part of the reference core and never omitted.
        var card = new Card { Id = Guid.NewGuid(), Name = "Card" };
        var physical = new MyCard(card) { Count = 1 };

        var json = JsonExport.ExportToJson("Deck", null, "deck", new[] { physical });

        Assert.That(json, Does.Contain("\"count\": 1"));
    }

    [Test]
    public void ExportToJson_OmitsOptionalFields_AtDefaults()
    {
        var card = new Card { Id = Guid.NewGuid(), Name = "Card" };
        var physical = new MyCard(card)
        {
            Count = 1,
            IsOwn = true,
            MyPrice = 0f,
        };

        var json = JsonExport.ExportToJson("Deck", null, "deck", new[] { physical });

        // The envelope always writes "comment"; scope the assertions to the cards section.
        var cardsPart = json[json.IndexOf("\"cards\"")..];
        Assert.That(cardsPart, Does.Not.Contain("\"own\""));
        Assert.That(cardsPart, Does.Not.Contain("\"price\""));
        Assert.That(cardsPart, Does.Not.Contain("\"comment\""));
        Assert.That(cardsPart, Does.Not.Contain("\"custom\""));
        Assert.That(cardsPart, Does.Not.Contain("\"special\""));
        Assert.That(cardsPart, Does.Not.Contain("\"forTrade\""));
    }

    [Test]
    public void ExportToJson_WritesOwnFalse_WhenBorrowed()
    {
        var card = new Card { Id = Guid.NewGuid(), Name = "Card" };
        var physical = new MyCard(card) { Count = 1, IsOwn = false };

        var json = JsonExport.ExportToJson("Deck", null, "deck", new[] { physical });

        Assert.That(json, Does.Contain("\"own\": false"));
    }

    [Test]
    public void ExportToJson_WritesPrice_WhenNonZero()
    {
        var card = new Card { Id = Guid.NewGuid(), Name = "Card" };
        var physical = new MyCard(card) { Count = 1, MyPrice = 5.0f };

        var json = JsonExport.ExportToJson("Deck", null, "deck", new[] { physical });

        Assert.That(json, Does.Contain("\"price\": 5"));
    }

    [Test]
    public void ExportToJson_Price_UsesShortestRoundTrippableRepresentation()
    {
        var card = new Card { Id = Guid.NewGuid(), Name = "Card" };
        var physical = new MyCard(card) { Count = 1, MyPrice = 0.1f };

        var json = JsonExport.ExportToJson("Deck", null, "deck", new[] { physical });

        // float 0.1f must serialize as "0.1", not the double-expanded "0.10000000149011612"
        Assert.That(json, Does.Contain("\"price\": 0.1"));
        Assert.That(json, Does.Not.Contain("0.100000001"));
    }

    [Test]
    public void ExportToJson_ThrowsForNonPhysicalCard()
    {
        var card = new Card { Id = Guid.NewGuid(), Name = "Base Card" };

        Assert.Throws<Exception>(() => JsonExport.ExportToJson("Deck", null, "deck", new ICard[] { card }));
    }

    [Test]
    public void ExportToJson_WritesSideboard_WhenProvided()
    {
        var sbId = Guid.NewGuid();
        var sbCard = new MyCard(
            new Card { Id = sbId, Name = "Sideboard Card", SetCode = "m19" },
            new Collection(1, string.Empty, null, NodeType.Deck, 0), true)
        {
            Count = 2,
            IsOwn = false,
        };

        var json = JsonExport.ExportToJson(
            "Deck", null, "deck",
            new[] { new MyCard(new Card { Id = Guid.NewGuid(), Name = "Main" }, new Collection(1, string.Empty, null, NodeType.Deck, 0), true) { Count = 1 } },
            new[] { sbCard });

        Assert.That(json, Does.Contain("\"sideboard\""));
        Assert.That(json, Does.Contain($"\"id\": \"{sbId}\""));
        Assert.That(json, Does.Contain("\"own\": false"), "sideboard cards use the same optional-field rules");

        // The sideboard array must come after the main cards array.
        var sbIndex = json.IndexOf("\"sideboard\"");
        var cardsIndex = json.IndexOf("\"cards\"");
        Assert.That(sbIndex, Is.GreaterThan(cardsIndex));
    }

    [Test]
    public void ExportToJson_OmitsSideboard_WhenNull()
    {
        var json = JsonExport.ExportToJson(
            "Deck", null, "deck",
            new[] { new MyCard(new Card { Id = Guid.NewGuid(), Name = "Main" }, new Collection(1, string.Empty, null, NodeType.Deck, 0), true) { Count = 1 } },
            sideboardCards: null);

        Assert.That(json, Does.Not.Contain("\"sideboard\""));
    }

    [Test]
    public void ExportToJson_OmitsSideboard_WhenEmpty()
    {
        // An empty sideboard list must not produce an empty "sideboard": [] node — the field is optional.
        var json = JsonExport.ExportToJson(
            "Deck", null, "deck",
            new[] { new MyCard(new Card { Id = Guid.NewGuid(), Name = "Main" }, new Collection(1, string.Empty, null, NodeType.Deck, 0), true) { Count = 1 } },
            Array.Empty<ICard>());

        Assert.That(json, Does.Not.Contain("\"sideboard\""));
    }

    [Test]
    public void ExportToJson_ThrowsForNonPhysicalSideboardCard()
    {
        var baseCard = new Card { Id = Guid.NewGuid(), Name = "Base SB Card" };

        Assert.Throws<Exception>(() => JsonExport.ExportToJson(
            "Deck", null, "deck",
            new[] { new MyCard(new Card { Id = Guid.NewGuid(), Name = "Main" }, new Collection(1, string.Empty, null, NodeType.Deck, 0), true) { Count = 1 } },
            new ICard[] { baseCard }));
    }
}
