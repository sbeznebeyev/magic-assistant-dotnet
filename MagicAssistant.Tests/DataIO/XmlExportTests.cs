namespace MagicAssistant.Tests.DataIO;

[TestFixture]
public class XmlExportTests
{
    [Test]
    public void ExportToXml_GeneratesValidXml()
    {
        var card = new MagicCard { Id = Guid.NewGuid(), Name = "Lightning Bolt" };
        var physical = new MagicCardPhysical(card) { Count = 4 };

        var xml = XmlExport.ExportToXml("Test Deck", "key-123", null, "deck", new[] { physical });

        Assert.That(xml, Does.Contain("<?xml"));
        Assert.That(xml, Does.Contain("<cards>"));
        Assert.That(xml, Does.Contain("Test Deck"));
        Assert.That(xml, Does.Contain("<mcp>"));
    }

    [Test]
    public void ExportToXml_ContainsCardCount()
    {
        var card = new MagicCard { Id = Guid.NewGuid(), Name = "Card" };
        var physical = new MagicCardPhysical(card) { Count = 3 };

        var xml = XmlExport.ExportToXml("Deck", null, null, "deck", new[] { physical });

        Assert.That(xml, Does.Contain("<count>3</count>"));
    }

    [Test]
    public void ExportToXml_HandlesEmptyCardList()
    {
        var xml = XmlExport.ExportToXml("Empty Deck", null, null, "deck", []);

        Assert.That(xml, Does.Contain("Empty Deck"));
    }

    [Test]
    public void ExportToXml_HandlesNullName()
    {
        var card = new MagicCard { Id = Guid.NewGuid(), Name = "Card" };
        var physical = new MagicCardPhysical(card);

        var xml = XmlExport.ExportToXml(null, null, null, "deck", new[] { physical });

        Assert.That(xml, Is.Not.Null);
    }

    [Test]
    public async Task ExportToFileAsync_CreatesFile()
    {
        var card = new MagicCard { Id = Guid.NewGuid(), Name = "Card" };
        var physical = new MagicCardPhysical(card);

        var filePath = Path.Combine(Path.GetTempPath(), $"test-export-{Guid.NewGuid()}.xml");
        try
        {
            await XmlExport.ExportToFileAsync(
                filePath, "Test Deck", "key-123", null, "deck", new[] { physical });

            Assert.That(File.Exists(filePath), Is.True);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Test]
    public async Task ExportToFileAsync_ShouldCreateParentDirectory_IfNotExists()
    {
        var card = new MagicCard { Id = Guid.NewGuid(), Name = "Card" };
        var physical = new MagicCardPhysical(card);

        var dir = Path.Combine(Path.GetTempPath(), $"export-test-{Guid.NewGuid()}");
        var filePath = Path.Combine(dir, "deck.xml");
        try
        {
            // BUG: ExportToFileAsync does NOT create parent directory.
            // File.WriteAllTextAsync throws DirectoryNotFoundException when parent dir doesn't exist.
            // The method should call Directory.CreateDirectory before writing.
            try
            {
                await XmlExport.ExportToFileAsync(
                    filePath, "Deck", null, null, "deck", new[] { physical });
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new Exception(
                    $"BUG: ExportToFileAsync failed to create parent directory '{dir}' before writing file. " +
                    "Expected behavior: method should ensure parent directory exists (Directory.CreateDirectory). " +
                    $"Actual: threw {ex.GetType().Name}. Inner: {ex.Message}",
                    ex);
            }

            Assert.That(File.Exists(filePath), Is.True);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Test]
    public void ExportToXml_PhysicalCard_WrapsInMcpWithNestedCardElement()
    {
        var card = new MagicCard { Id = Guid.NewGuid(), Name = "Card" };
        var physical = new MagicCardPhysical(card) { Count = 1, IsOwn = true };

        var xml = XmlExport.ExportToXml("Deck", null, null, "deck", new[] { physical });

        // WriteMagicCardPhysical writes: <mcp><card><id>..</id><name>..</name><edition>..</edition></card>...</mcp>
        // It does NOT write <mc> — that's only for non-physical MagicCard instances.
        Assert.That(xml, Does.Contain("<mcp>"));
        Assert.That(xml, Does.Contain("<card>"));
    }

    [Test]
    public void ExportToXml_PhysicalCard_WritesPriceAndCommentAsDirectElements()
    {
        var card = new MagicCard { Id = Guid.NewGuid(), Name = "Card" };
        var physical = new MagicCardPhysical(card)
        {
            Count = 1,
            MyPrice = 5.0f,
            Comment = "test card comment",
            Custom = "custom data"
        };

        var xml = XmlExport.ExportToXml("Deck", null, null, "deck", new[] { physical });

        // BUG: MagicCardPhysical does NOT write a <properties> block.
        // Properties block is only written by WriteMagicCard() for non-physical MagicCard instances.
        // Physical card fields (Price, Comment, Custom) are written as direct child elements of <mcp>.
        // This test verifies the actual behavior: Price/Comment/Custom appear as direct elements.

        Assert.That(xml, Does.Contain("<price>5</price>"),
            "Price should be written as a direct <price> element inside <mcp>, not inside <properties>");
        Assert.That(xml, Does.Contain("<comment>test card comment</comment>"),
            "Comment should be written as a direct <comment> element inside <mcp>, not inside <properties>");
        Assert.That(xml, Does.Contain("<custom>custom data</custom>"),
            "Custom should be written as a direct <custom> element inside <mcp>, not inside <properties>");

        // Verify that <properties> is NOT present for physical cards
        Assert.That(xml, Does.Not.Contain("<properties>"),
            "<properties> block should only appear in <mc> (non-physical MagicCard), not in <mcp> (MagicCardPhysical)");
    }
}
