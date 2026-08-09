using System.Xml;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
namespace MagicAssistant.Data.IO.xml;

/// <summary>
/// Imports card collections from Magic Assistant XML format.
/// </summary>
public static class XmlImport
{
    public static async Task<CardCollectionImportResult> ImportFromFileAsync(
        string filePath, CancellationToken ct = default)
    {
        var xml = await File.ReadAllTextAsync(filePath, ct);
        return ImportFromXml(xml);
    }

    public static CardCollectionImportResult ImportFromXml(string xml)
    {
        var result = new CardCollectionImportResult();

        using var reader = XmlReader.Create(
            new StringReader(xml),
            new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Prohibit,
            });

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "cards")
                break;
        }

        if (reader.NodeType != XmlNodeType.Element)
            throw new FormatException("Invalid Magic Assistant XML: root element must be <cards>");

        ReadCardsHeader(reader, result);
        return result;
    }

    static void ReadCardsHeader(XmlReader reader, CardCollectionImportResult result)
    {
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.EndElement:
                    if (reader.LocalName == "cards") return;
                    continue;

                case XmlNodeType.Element when reader.LocalName == "list":
                    if (result.Cards.Count == 0)
                        result.Cards = ReadCardList(reader);
                    else
                        // Skip additional <list> elements — keep only the first one
                        ReadCardList(reader);
                    continue;

                case XmlNodeType.Element:
                    var value = ReadElementValue(reader);
                    switch (reader.LocalName)
                    {
                        case "name": result.Name = value ?? string.Empty; break;
                        case "key": result.Key = value; break;
                        case "comment": result.Comment = value; break;
                        case "type": result.Type = value; break;
                    }
                    break;
            }
        }
    }

    static List<IMagicCard> ReadCardList(XmlReader reader)
    {
        var cards = new List<IMagicCard>();

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.EndElement:
                    if (reader.LocalName == "list") return cards;
                    continue;

                case XmlNodeType.Element when reader.LocalName == "mcp":
                    cards.Add(ReadMagicCardPhysical(reader));
                    break;

                case XmlNodeType.Element when reader.LocalName == "mc":
                    throw new Exception("Can't import non-physical cards <mc>. Only <mcp> supported.");
            }
        }

        return cards;
    }

    static MagicCardPhysical ReadMagicCardPhysical(XmlReader reader)
    {
        // Collect all field values, then construct the object
        var fields = new Dictionary<string, string?>();

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.EndElement:
                    if (reader.LocalName == "mcp") goto Build;
                    continue;

                case XmlNodeType.Element when reader.LocalName == "card":
                    // Read nested card reference (id, name, edition)
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "card") break;
                        if (reader.NodeType != XmlNodeType.Element) continue;
                        fields[reader.LocalName] = ReadElementValue(reader);
                    }
                    break;

                case XmlNodeType.Element:
                    fields[reader.LocalName] = ReadElementValue(reader);
                    break;
            }
        }

    Build:
        var baseCard = new MagicCard
        {
            Id = Guid.Parse(fields.GetValueOrDefault("id") ?? throw new ArgumentNullException("id")),
            Name = fields.GetValueOrDefault("name") ?? string.Empty,
            SetCode = fields.GetValueOrDefault("edition")?.ToLowerInvariant(),
        };

        var physical = new MagicCardPhysical(baseCard) { IsOwn = true };

        if (int.TryParse(fields.GetValueOrDefault("count"), out var count))
            physical.Count = count;
        if (float.TryParse(fields.GetValueOrDefault("price"), System.Globalization.CultureInfo.InvariantCulture, out var price))
            physical.Price = price;
        physical.Comment = OptStr(fields, "comment");
        physical.Custom = OptStr(fields, "custom");
        if (bool.TryParse(fields.GetValueOrDefault("ownership"), out var isOwn))
            physical.IsOwn = isOwn;
        physical.SpecialTags = OptStr(fields, "special");
        if (DateTimeOffset.TryParse(fields.GetValueOrDefault("date"), System.Globalization.CultureInfo.InvariantCulture, out var date))
            physical.DateAdded = date;
        var ftVal = fields.GetValueOrDefault("forTrade") ?? fields.GetValueOrDefault("fortradecount");
        if (int.TryParse(ftVal, out var ft) && ft > 0)
            physical.SetForTradeCount(ft);
        var loc = fields.GetValueOrDefault("location");
        if (!string.IsNullOrEmpty(loc))
            physical.Location = Location.ValueOf(loc);

        return physical;
    }
        
    /// <summary>
    /// Reads text content of a simple element. Caller positioned on start element.
    /// After call, reader is on the end element.
    /// For elements with children, returns null and leaves reader on first child.
    /// </summary>
    static string? ReadElementValue(XmlReader reader)
    {
        if (reader.IsEmptyElement) return null;
        reader.Read();
        if (reader.NodeType is XmlNodeType.Text or XmlNodeType.SignificantWhitespace)
        {
            var value = reader.Value?.Trim();
            reader.Read(); // move to EndElement
            return value;
        }
        return null; // has child elements
    }

    static string? OptStr(Dictionary<string, string?> fields, string key)
    {
        var val = fields.GetValueOrDefault(key);
        return string.IsNullOrWhiteSpace(val) ? null : val;
    }

    public class CardCollectionImportResult
    {
        public string Name { get; set; } = string.Empty;
        public string? Key { get; set; }
        public string? Comment { get; set; }
        public string? Type { get; set; }
        public List<IMagicCard> Cards { get; set; } = new();
    }
}
