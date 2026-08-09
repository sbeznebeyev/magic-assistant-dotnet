using System.Text;
using System.Xml;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.Legality;

namespace MagicAssistant.Data.IO.xml;

/// <summary>
/// Exports card collections to Magic Assistant XML format.
/// 
/// Produces the same XML structure that XmlImport can read back:
/// &lt;cards&gt;
///   &lt;name&gt;...&lt;/name&gt;
///   &lt;key&gt;...&lt;/key&gt;
///   &lt;comment&gt;...&lt;/comment&gt;
///   &lt;type&gt;deck&lt;/type&gt;
///   &lt;list&gt;
///     &lt;mcp&gt;...&lt;/mcp&gt;  <!-- MagicCardPhysical -->
///   &lt;/list&gt;
/// &lt;/cards&gt;
/// </summary>
/// 
/// Only <see cref="MagicCardPhysical"/> cards are supported for export.
/// Base <see cref="MagicCard"/> cards cannot be exported directly.
public static class XmlExport
{
    /// <summary>
    /// Export cards to a Magic Assistant XML file.
    /// </summary>
    public static async Task ExportToFileAsync(
        string filePath,
        string? name,
        string? key,
        string? comment,
        string? type,
        IEnumerable<IMagicCard> cards,
        CancellationToken ct = default)
    {
        var xml = ExportToXml(name, key, comment, type, cards);
        var parentDir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(parentDir))
            Directory.CreateDirectory(parentDir);
        await File.WriteAllTextAsync(filePath, xml, ct);
    }

    /// <summary>
    /// Export cards to a Magic Assistant XML string.
    /// </summary>
    public static string ExportToXml(
        string? name,
        string? key,
        string? comment,
        string? type,
        IEnumerable<IMagicCard> cards)
    {
        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false,
        });

        writer.WriteStartDocument(true); // standalone="yes"
        writer.WriteStartElement("cards");

        WriteElement(writer, "name", name);
        WriteElement(writer, "key", key);
        WriteElement(writer, "comment", comment);
        WriteElement(writer, "type", type);

        writer.WriteStartElement("list");

        foreach (var card in cards)
        {
            if (card is MagicCardPhysical physical)
                WriteMagicCardPhysical(writer, physical);
            else
                throw new Exception("Can't export non-physical cards");
        }

        writer.WriteEndElement(); // list
        writer.WriteEndElement(); // cards
        writer.Flush();

        return sb.ToString();
    }

    static void WriteMagicCardPhysical(XmlWriter writer, MagicCardPhysical physical)
    {
        writer.WriteStartElement("mcp");

        // Card reference
        writer.WriteStartElement("card");
        WriteElement(writer, "id", physical.Card.Id.ToString());
        WriteElement(writer, "name", physical.Name);
        WriteElement(writer, "edition", physical.SetCode);
        writer.WriteEndElement(); // card

        // Physical fields — only write non-default values (mirrors Java behavior)
        WriteElementIf(writer, "count", physical.Count.ToString(), physical.Count != 1);
        WriteElementIf(writer, "price", physical.Price?.ToString(System.Globalization.CultureInfo.InvariantCulture), physical.Price.HasValue && physical.Price.Value != 0f);
        WriteElementIf(writer, "comment", physical.Comment, !string.IsNullOrEmpty(physical.Comment));
        WriteElementIf(writer, "custom", physical.Custom, !string.IsNullOrEmpty(physical.Custom));
        WriteElementIf(writer, "ownership", physical.IsOwn.ToString().ToLowerInvariant(), !physical.IsOwn);
        WriteElementIf(writer, "special", physical.SpecialTags, !string.IsNullOrEmpty(physical.SpecialTags));

        // Date — always write if set
        if (physical.DateAdded != default)
            WriteElement(writer, "date", physical.DateAdded.ToString("yyyy-MM-dd"));

        // ForTradeCount
        var forTrade = physical.GetForTradeCount();
        if (forTrade > 0 && !physical.HasSpecialTag("fortrade"))
            WriteElement(writer, "forTrade", forTrade.ToString());

        writer.WriteEndElement(); // mcp
    }

    static void WriteElement(XmlWriter writer, string localName, string? value)
    {
        if (value == null)
        {
            writer.WriteElementString(localName, string.Empty);
        }
        else
        {
            writer.WriteElementString(localName, value);
        }
    }

    static void WriteElementIf(XmlWriter writer, string localName, string? value, bool condition)
    {
        if (condition && !string.IsNullOrEmpty(value))
            writer.WriteElementString(localName, value);
    }
}
