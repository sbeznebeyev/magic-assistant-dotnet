using MagicAssistant.Data.IO.xml;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.DataIO;

[TestFixture]
public class XmlRoundTripTests
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
            Price = 2.5f,
            Comment = "Test comment",
            SpecialTags = "foil,premium",
            Custom = "custom-data",
        };

        var xml = XmlExport.ExportToXml("Test Deck", "key-123", "A test deck", "deck", new[] { physical });
        var result = XmlImport.ImportFromXml(xml);

        Assert.That(result.Name, Is.EqualTo("Test Deck"));
        Assert.That(result.Key, Is.EqualTo("key-123"));
        Assert.That(result.Comment, Is.EqualTo("A test deck"));
        Assert.That(result.Cards, Has.Count.EqualTo(1));

        var imported = (MagicCardPhysical)result.Cards[0];
        Assert.That(imported.Card.Id, Is.EqualTo(cardId));
        Assert.That(imported.Name, Is.EqualTo("Lightning Bolt"));
        Assert.That(imported.SetCode, Is.EqualTo("m19"));
        Assert.That(imported.Count, Is.EqualTo(4));
        Assert.That(imported.Price, Is.EqualTo(2.5f).Within(0.01f));
        Assert.That(imported.Comment, Is.EqualTo("Test comment"));
        Assert.That(imported.SpecialTags, Is.EqualTo("foil,premium"));
        Assert.That(imported.Custom, Is.EqualTo("custom-data"));
        Assert.That(imported.IsOwn, Is.True);
    }

    [Test]
    public void RoundTrip_PhysicalCard_PreservesBorrowedOwnership()
    {
        // Borrowed card (IsOwn = false)
        var physical = new MagicCardPhysical(
            new MagicCard { Id = Guid.NewGuid(), Name = "Mountain", SetCode = "m20" },
            Location.FromPath("deck-1"), false)
        {
            Count = 1,
        };

        var xml = XmlExport.ExportToXml("Deck", null, null, "deck", new[] { physical });
        var result = XmlImport.ImportFromXml(xml);

        var imported = (MagicCardPhysical)result.Cards[0];
        Assert.That(imported.IsOwn, Is.False,
            "Borrowed card (IsOwn=false) should remain borrowed after round-trip. " +
            "BUG: XmlExport only writes <ownership>false</ownership> when !IsOwn, " +
            "but XmlImport.ReadMagicCardPhysical defaults to IsOwn=true and only overrides if <ownership> element exists.");
    }

    [Test]
    public void RoundTrip_MultipleCards_PreservesAll()
    {
        var cards = new IMagicCard[]
        {
            new MagicCardPhysical(
                new MagicCard { Id = Guid.NewGuid(), Name = "Lightning Bolt", SetCode = "m19" },
                Location.FromPath("deck-1"), true) { Count = 4, Price = 2.5f },
            new MagicCardPhysical(
                new MagicCard { Id = Guid.NewGuid(), Name = "Mountain", SetCode = "m19" },
                Location.FromPath("deck-1"), true) { Count = 24 },
            new MagicCardPhysical(
                new MagicCard { Id = Guid.NewGuid(), Name = "Counterspell", SetCode = "m20" },
                Location.FromPath("deck-1"), false) { Count = 1, Comment = "Borrowed" },
        };

        var xml = XmlExport.ExportToXml("My Deck", "key-123", "A test deck", "deck", cards);
        var result = XmlImport.ImportFromXml(xml);

        Assert.That(result.Name, Is.EqualTo("My Deck"));
        Assert.That(result.Key, Is.EqualTo("key-123"));
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
        var xml = XmlExport.ExportToXml("Empty Deck", "key-456", "No cards", "deck", Array.Empty<IMagicCard>());
        var result = XmlImport.ImportFromXml(xml);

        Assert.That(result.Name, Is.EqualTo("Empty Deck"));
        Assert.That(result.Key, Is.EqualTo("key-456"));
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
            Comment = "Tags: <foil> & \"signed\" card",
        };

        var xml = XmlExport.ExportToXml("Deck", null, null, "deck", new[] { physical });
        var result = XmlImport.ImportFromXml(xml);

        var imported = (MagicCardPhysical)result.Cards[0];
        Assert.That(imported.Comment, Is.EqualTo("Tags: <foil> & \"signed\" card"));
    }

    [Test]
    public void RoundTrip_CountOne_NotExplicitlyWritten_StillImportsAsOne()
    {
        // Count=1 is the default — XmlExport skips writing it
        var physical = new MagicCardPhysical(
            new MagicCard { Id = Guid.NewGuid(), Name = "Mountain", SetCode = "m19" },
            Location.FromPath("deck-1"), true)
        {
            Count = 1,
        };

        var xml = XmlExport.ExportToXml("Deck", null, null, "deck", new[] { physical });

        // Verify count element is NOT in XML (default behavior)
        Assert.That(xml, Does.Not.Contain("<count>1</count>"));

        var result = XmlImport.ImportFromXml(xml);
        var imported = (MagicCardPhysical)result.Cards[0];

        // Should still be 1 after round-trip
        Assert.That(imported.Count, Is.EqualTo(1));
    }

    [Test]
    public void RoundTrip_PriceZero_NotWritten_StillImportsAsZero()
    {
        var physical = new MagicCardPhysical(
            new MagicCard { Id = Guid.NewGuid(), Name = "Mountain", SetCode = "m19" },
            Location.FromPath("deck-1"), true)
        {
            Price = 0f,
        };

        var xml = XmlExport.ExportToXml("Deck", null, null, "deck", new[] { physical });

        // Verify price element is NOT in XML (default behavior)
        Assert.That(xml, Does.Not.Contain("<price>"));

        var result = XmlImport.ImportFromXml(xml);
        var imported = (MagicCardPhysical)result.Cards[0];

        Assert.That(imported.Price, Is.Null.Or.EqualTo(0f));
    }

}
