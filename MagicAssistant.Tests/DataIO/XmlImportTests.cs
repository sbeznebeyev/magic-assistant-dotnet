namespace MagicAssistant.Tests.DataIO;

[TestFixture]
public class XmlImportTests
{
    // Magic Assistant XML format (produced by XmlExport):
    // <cards><name>...</name><key>...</key><comment>...</comment><type>...</type>
    //   <list>
    //     <mcp><card><id>..</id><name>..</name><edition>..</edition></card>
    //         <count>4</count><price>5.0</price><comment>..</comment>...</mcp>
    //     <mc><name>..</name><cost>..</cost>...<properties>...</properties></mc>
    //   </list>
    // </cards>

    [Test]
    public void ImportFromXml_ParsesValidDeckXml()
    {
        var cardId = Guid.NewGuid();
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<cards>
    <name>Test Deck</name>
    <list>
        <mcp>
            <card>
                <id>" + cardId.ToString() + @"</id>
                <name>Lightning Bolt</name>
                <edition>M19</edition>
            </card>
            <count>4</count>
            <ownership>true</ownership>
        </mcp>
    </list>
</cards>";

        var result = XmlImport.ImportFromXml(xml);

        Assert.That(result.Name, Is.EqualTo("Test Deck"));
        Assert.That(result.Cards, Has.Count.EqualTo(1));
        Assert.That(result.Cards[0], Is.InstanceOf<MagicCardPhysical>());

        var physical = (MagicCardPhysical)result.Cards[0];
        Assert.That(physical.Card.Name, Is.EqualTo("Lightning Bolt"));
        Assert.That(physical.Card.Id, Is.EqualTo(cardId));
        Assert.That(physical.Card.SetCode, Is.EqualTo("m19"));
        Assert.That(physical.Count, Is.EqualTo(4));
    }

    [Test]
    public void ImportFromXml_ParsesMultipleCards()
    {
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();

        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<cards>
    <name>Test Deck</name>
    <list>
        <mcp>
            <card>
                <id>" + cardId1.ToString() + @"</id>
                <name>Card 1</name>
            </card>
            <count>1</count>
        </mcp>
        <mcp>
            <card>
                <id>" + cardId1.ToString() + @"</id>
                <name>Card 2</name>
            </card>
            <count>2</count>
        </mcp>
    </list>
</cards>";

        var result = XmlImport.ImportFromXml(xml);

        Assert.That(result.Cards, Has.Count.EqualTo(2));
        Assert.That(((MagicCardPhysical)result.Cards[0]).Card.Name, Is.EqualTo("Card 1"));
        Assert.That(((MagicCardPhysical)result.Cards[1]).Card.Name, Is.EqualTo("Card 2"));
        Assert.That(((MagicCardPhysical)result.Cards[0]).Count, Is.EqualTo(1));
        Assert.That(((MagicCardPhysical)result.Cards[1]).Count, Is.EqualTo(2));
    }

    [Test]
    public void ImportFromXml_ReturnsEmpty_WhenNoCards()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<cards>
    <name>Empty Deck</name>
    <list></list>
</cards>";

        var result = XmlImport.ImportFromXml(xml);

        Assert.That(result.Name, Is.EqualTo("Empty Deck"));
        Assert.That(result.Cards, Is.Empty);
    }

    [Test]
    public void ImportFromXml_PhysicalCard_ParsesPriceAndCommentAsDirectFields()
    {
        var cardId = Guid.NewGuid();
        // Physical card fields (price, comment) are direct children of <mcp>, NOT inside <properties>.
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<cards>
    <name>Test Deck</name>
    <list>
        <mcp>
            <card>
                <id>" + cardId.ToString() + @"</id>
                <name>Card</name>
            </card>
            <count>1</count>
            <price>5.00</price>
            <comment>My card comment</comment>
        </mcp>
    </list>
</cards>";

        var result = XmlImport.ImportFromXml(xml);

        Assert.That(result.Cards, Has.Count.EqualTo(1));
        var physical = (MagicCardPhysical)result.Cards[0];

        Assert.That(physical.Price, Is.EqualTo(5.0f).Within(0.001f),
            "Price should be parsed from direct <price> element inside <mcp>");
        Assert.That(physical.Comment, Is.EqualTo("My card comment"),
            "Comment should be parsed from direct <comment> element inside <mcp>");
    }

    [Test]
    public void ImportFromXml_HandlesSpecialCharactersInName()
    {
        var cardId = Guid.NewGuid();
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<cards>
    <name>Deck with &amp; special chars</name>
    <list>
        <mcp>
            <card>
                <id>" + cardId.ToString() + @"</id>
                <name>Card with &quot;quotes&quot;</name>
            </card>
            <count>1</count>
        </mcp>
    </list>
</cards>";

        var result = XmlImport.ImportFromXml(xml);

        Assert.That(result.Name, Is.EqualTo("Deck with & special chars"),
            "XML entity &amp; should be decoded to &");
        Assert.That(((MagicCardPhysical)result.Cards[0]).Card.Name, Is.EqualTo("Card with \"quotes\""),
            "XML entity &quot; should be decoded to \"");
    }

    [Test]
    public void ImportFromXml_MultipleLists_OnlyLastListKept()
    {
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();

        // BUG: ReadCardsHeader overwrites result.Cards for each <list> element.
        // Only the LAST list is kept; all previous lists are silently discarded.
        // Expected behavior: either throw an exception for multiple <list> elements,
        // or parse only the first one and ignore the rest (with a warning).
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<cards>
    <name>Multi List Deck</name>
    <list>
        <mcp>
            <card>
                <id>" + cardId1.ToString() + @"</id>
                <name>Card A</name>
            </card>
            <count>1</count>
        </mcp>
    </list>
    <list>
        <mcp>
            <card>
                <id>" + cardId2.ToString() + @"</id>
                <name>Card B</name>
            </card>
            <count>1</count>
        </mcp>
    </list>
</cards>";

        var result = XmlImport.ImportFromXml(xml);

        // BUG: Code overwrites Cards with each <list>, so only Deck 2's cards survive.
        // The first list (Card A) is silently lost.
        try
        {
            Assert.That(result.Cards[0], Is.InstanceOf<MagicCardPhysical>());
            var firstCardName = ((MagicCardPhysical)result.Cards[0]).Card.Name;

            if (firstCardName == "Card A")
            {
                // Correct behavior: first list kept
                Assert.Pass("First list was kept — this is the expected behavior");
            }
            else if (firstCardName == "Card B")
            {
                throw new Exception(
                    "BUG: Multiple <list> elements cause data loss. " +
                    $"Only the LAST list is kept, the first list ('Card A') is silently discarded. " +
                    $"ReadCardsHeader overwrites result.Cards for each <list>. " +
                    "Expected: either keep only the first <list> or throw for multiple lists.");
            }
            else
            {
                Assert.Fail($"Unexpected card name: {firstCardName}");
            }
        }
        catch (Exception ex) when (ex is not AssertionException)
        {
            // Re-throw bug-report exceptions as test failures
            throw;
        }
    }

    [Test]
    public void ImportFromFileAsync_ThrowsForNonExistentFile()
    {
        // On Linux, non-existent path throws DirectoryNotFoundException.
        // The original test expected FileNotFoundException, but that's incorrect for this platform.
        var ex = Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => XmlImport.ImportFromFileAsync("/nonexistent/path/file.xml"));
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task ImportFromFileAsync_ParsesValidXmlFile()
    {
        var cardId = Guid.NewGuid();
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<cards>
    <name>File Deck</name>
    <list>
        <mcp>
            <card>
                <id>" + cardId.ToString() + @"</id>
                <name>Card from file</name>
            </card>
            <count>1</count>
        </mcp>
    </list>
</cards>";

        var filePath = Path.Combine(Path.GetTempPath(), $"test-import-{Guid.NewGuid()}.xml");
        try
        {
            await File.WriteAllTextAsync(filePath, xml);

            var result = await XmlImport.ImportFromFileAsync(filePath);

            Assert.That(result.Name, Is.EqualTo("File Deck"));
            Assert.That(result.Cards, Has.Count.EqualTo(1));
            Assert.That(((MagicCardPhysical)result.Cards[0]).Card.Name, Is.EqualTo("Card from file"));
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
