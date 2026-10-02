using MagicAssistant.Data.IO.json;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.DataIO;

[TestFixture]
public class JsonRoundTripTests
{
    /* ═══════════════════════════════════════════════════════
     * Round-trip: Export → Import должен сохранить данные
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public void RoundTrip_PhysicalCard_PreservesAllFields()
    {
        var cardId = Guid.NewGuid();

        var physical = new MagicCardPhysical(
            new MagicCard { Id = cardId, Name = "Lightning Bolt", SetCode = "m19" },
            Location.FromPath("deck-1"), true)
        {
            Count = 4,
            MyPrice = 2.5f,
            Comment = "Test comment",
            SpecialTags = "foil,premium",
            Custom = "custom-data",
        };

        var json = JsonExport.ExportToJson("Test Deck", "A test deck", "deck", new[] { physical });
        var result = JsonImport.ImportFromJson(json);

        Assert.That(result.Name, Is.EqualTo("Test Deck"));
        Assert.That(result.Comment, Is.EqualTo("A test deck"));
        Assert.That(result.Type, Is.EqualTo("deck"));
        Assert.That(result.Key, Is.Null, "the native JSON format has no key field");
        Assert.That(result.Cards, Has.Count.EqualTo(1));

        var imported = (MagicCardPhysical)result.Cards[0];
        Assert.That(imported.Card.Id, Is.EqualTo(cardId));
        Assert.That(imported.Name, Is.EqualTo("Lightning Bolt"));
        Assert.That(imported.SetCode, Is.EqualTo("m19"));
        Assert.That(imported.Count, Is.EqualTo(4));
        Assert.That(imported.MyPrice, Is.EqualTo(2.5f).Within(0.01f));
        Assert.That(imported.Comment, Is.EqualTo("Test comment"));
        Assert.That(imported.SpecialTags, Is.EqualTo("foil,premium"));
        Assert.That(imported.Custom, Is.EqualTo("custom-data"));
        Assert.That(imported.IsOwn, Is.True);
    }

    [Test]
    public void RoundTrip_PhysicalCard_PreservesBorrowedOwnership()
    {
        // Borrowed card (IsOwn = false) — "own": false must survive the round-trip.
        var physical = new MagicCardPhysical(
            new MagicCard { Id = Guid.NewGuid(), Name = "Mountain", SetCode = "m20" },
            Location.FromPath("deck-1"), false)
        {
            Count = 1,
        };

        var json = JsonExport.ExportToJson("Deck", null, "deck", new[] { physical });
        var result = JsonImport.ImportFromJson(json);

        var imported = (MagicCardPhysical)result.Cards[0];
        Assert.That(imported.IsOwn, Is.False,
            "Borrowed card (IsOwn=false) should remain borrowed after round-trip.");
    }

    [Test]
    public void RoundTrip_MultipleCards_PreservesAll()
    {
        var cards = new IMagicCard[]
        {
            new MagicCardPhysical(
                new MagicCard { Id = Guid.NewGuid(), Name = "Lightning Bolt", SetCode = "m19" },
                Location.FromPath("deck-1"), true) { Count = 4, MyPrice = 2.5f },
            new MagicCardPhysical(
                new MagicCard { Id = Guid.NewGuid(), Name = "Mountain", SetCode = "m19" },
                Location.FromPath("deck-1"), true) { Count = 24 },
            new MagicCardPhysical(
                new MagicCard { Id = Guid.NewGuid(), Name = "Counterspell", SetCode = "m20" },
                Location.FromPath("deck-1"), false) { Count = 1, Comment = "Borrowed" },
        };

        var json = JsonExport.ExportToJson("My Deck", "A test deck", "deck", cards);
        var result = JsonImport.ImportFromJson(json);

        Assert.That(result.Name, Is.EqualTo("My Deck"));
        Assert.That(result.Comment, Is.EqualTo("A test deck"));
        Assert.That(result.Cards, Has.Count.EqualTo(3));

        var imported = result.Cards.Cast<MagicCardPhysical>().ToList();
        Assert.That(imported[0].Count, Is.EqualTo(4));
        Assert.That(imported[1].Count, Is.EqualTo(24));
        Assert.That(imported[2].IsOwn, Is.False);
    }

    [Test]
    public void RoundTrip_EmptyDeck_PreservesMetadata()
    {
        var json = JsonExport.ExportToJson("Empty Deck", "No cards", "deck", Array.Empty<IMagicCard>());
        var result = JsonImport.ImportFromJson(json);

        Assert.That(result.Name, Is.EqualTo("Empty Deck"));
        Assert.That(result.Comment, Is.EqualTo("No cards"));
        Assert.That(result.Cards, Is.Empty);
    }

    [Test]
    public void RoundTrip_SpecialCharactersInComment_EscapedCorrectly()
    {
        var physical = new MagicCardPhysical(
            new MagicCard { Id = Guid.NewGuid(), Name = "Test", SetCode = "m19" },
            Location.FromPath("deck-1"), true)
        {
            Comment = "Tags: <foil> & \"signed\" card — кириллица",
        };

        var json = JsonExport.ExportToJson("Deck", null, "deck", new[] { physical });
        var result = JsonImport.ImportFromJson(json);

        var imported = (MagicCardPhysical)result.Cards[0];
        Assert.That(imported.Comment, Is.EqualTo("Tags: <foil> & \"signed\" card — кириллица"));
    }

    [Test]
    public void RoundTrip_CountOne_AlwaysWritten_StillImportsAsOne()
    {
        // Unlike the legacy XML format, count is part of the reference core and always written.
        var physical = new MagicCardPhysical(
            new MagicCard { Id = Guid.NewGuid(), Name = "Mountain", SetCode = "m19" },
            Location.FromPath("deck-1"), true)
        {
            Count = 1,
        };

        var json = JsonExport.ExportToJson("Deck", null, "deck", new[] { physical });

        Assert.That(json, Does.Contain("\"count\": 1"));

        var result = JsonImport.ImportFromJson(json);
        var imported = (MagicCardPhysical)result.Cards[0];

        Assert.That(imported.Count, Is.EqualTo(1));
    }

    [Test]
    public void RoundTrip_PriceZero_NotWritten_StillImportsAsZero()
    {
        var physical = new MagicCardPhysical(
            new MagicCard { Id = Guid.NewGuid(), Name = "Mountain", SetCode = "m19" },
            Location.FromPath("deck-1"), true)
        {
            MyPrice = 0f,
        };

        var json = JsonExport.ExportToJson("Deck", null, "deck", new[] { physical });

        Assert.That(json, Does.Not.Contain("\"price\""));

        var result = JsonImport.ImportFromJson(json);
        var imported = (MagicCardPhysical)result.Cards[0];

        Assert.That(imported.MyPrice, Is.EqualTo(0f));
    }

    [Test]
    public void RoundTrip_ForTradeCount_Preserved()
    {
        var physical = new MagicCardPhysical(
            new MagicCard { Id = Guid.NewGuid(), Name = "Mountain", SetCode = "m19" },
            Location.FromPath("deck-1"), true)
        {
            Count = 3,
        };
        physical.SetForTradeCount(2);

        var json = JsonExport.ExportToJson("Deck", null, "deck", new[] { physical });
        Assert.That(json, Does.Contain("\"forTrade\": 2"));

        var result = JsonImport.ImportFromJson(json);
        var imported = (MagicCardPhysical)result.Cards[0];

        Assert.That(imported.GetForTradeCount(), Is.EqualTo(2));
    }

    [Test]
    public void RoundTrip_Sideboard_PreservesAllFields()
    {
        var sbId = Guid.NewGuid();
        var sideboardCard = new MagicCardPhysical(
            new MagicCard { Id = sbId, Name = "Liliana, the Wicked", SetCode = "mh3" },
            Location.FromPath("deck-1"), false)
        {
            Count = 2,
            MyPrice = 0.5f,
            Comment = "SB comment",
            SpecialTags = "foil",
            Custom = "sb-custom",
        };

        var mainCard = new MagicCardPhysical(
            new MagicCard { Id = Guid.NewGuid(), Name = "Lightning Bolt", SetCode = "m19" },
            Location.FromPath("deck-1"), true)
        {
            Count = 4,
        };

        var json = JsonExport.ExportToJson("Test Deck", null, "deck", new[] { mainCard }, new[] { sideboardCard });
        var result = JsonImport.ImportFromJson(json);

        Assert.That(result.Cards, Has.Count.EqualTo(1));
        Assert.That(result.Sideboard, Has.Count.EqualTo(1));

        var importedSb = (MagicCardPhysical)result.Sideboard[0];
        Assert.That(importedSb.Card.Id, Is.EqualTo(sbId));
        Assert.That(importedSb.Name, Is.EqualTo("Liliana, the Wicked"));
        Assert.That(importedSb.SetCode, Is.EqualTo("mh3"));
        Assert.That(importedSb.Count, Is.EqualTo(2));
        Assert.That(importedSb.IsOwn, Is.False);
        Assert.That(importedSb.MyPrice, Is.EqualTo(0.5f).Within(0.01f));
        Assert.That(importedSb.Comment, Is.EqualTo("SB comment"));
        Assert.That(importedSb.SpecialTags, Is.EqualTo("foil"));
        Assert.That(importedSb.Custom, Is.EqualTo("sb-custom"));

        // The main deck is untouched by the sideboard round-trip.
        var importedMain = (MagicCardPhysical)result.Cards[0];
        Assert.That(importedMain.Name, Is.EqualTo("Lightning Bolt"));
        Assert.That(importedMain.Count, Is.EqualTo(4));
    }

    [Test]
    public void RoundTrip_NoSideboard_NodeAbsent()
    {
        // A deck without a sideboard must round-trip WITHOUT the node — it is optional.
        var physical = new MagicCardPhysical(
            new MagicCard { Id = Guid.NewGuid(), Name = "Mountain", SetCode = "m19" },
            Location.FromPath("deck-1"), true)
        {
            Count = 24,
        };

        var json = JsonExport.ExportToJson("Deck", null, "deck", new[] { physical });

        Assert.That(json, Does.Not.Contain("\"sideboard\""));

        var result = JsonImport.ImportFromJson(json);
        Assert.That(result.Sideboard, Is.Empty);
    }
}
