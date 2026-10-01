using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Data.IO.json;

/// <summary>
/// Exports card collections to the native Magic Assistant JSON format (v1).
///
/// Produces the same structure that JsonImport can read back:
/// {
///   "format": "magic-assistant/deck",
///   "version": 1,
///   "name": "...",
///   "comment": "",
///   "type": "deck",
///   "exportedAt": "2026-10-01T13:47:00+00:00",
///   "cards": [ { "id", "name", "set", "count", ... } ]
/// }
///
/// Reference core (id / name / set / count) is always written. Optional fields
/// are omitted when at their defaults: own=true, price=0, empty comment/custom/special,
/// forTrade=0. Output is pretty-printed with 2-space indent.
///
/// Only <see cref="MagicCardPhysical"/> cards are supported for export.
/// Base <see cref="MagicCard"/> cards cannot be exported directly.
/// </summary>
public static class JsonExport
{
    /// <summary>Format marker written into the file; JsonImport validates it.</summary>
    public const string FormatId = "magic-assistant/deck";

    /// <summary>Schema version of the native JSON format.</summary>
    public const int Version = 1;

    /// <summary>
    /// Export cards to a Magic Assistant JSON string.
    /// </summary>
    public static string ExportToJson(
        string? name,
        string? comment,
        string? type,
        IEnumerable<IMagicCard> cards)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true, // .NET 10: renamed from Indent
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep Cyrillic card names readable
        }))
        {
            writer.WriteStartObject();

            // Envelope — always written
            writer.WriteString("format", FormatId);
            writer.WriteNumber("version", Version);
            writer.WriteString("name", name ?? string.Empty);
            writer.WriteString("comment", comment ?? string.Empty);
            writer.WriteString("type", type ?? "deck");
            writer.WriteString("exportedAt", DateTimeOffset.Now);

            // Cards
            writer.WritePropertyName("cards");
            writer.WriteStartArray();
            foreach (var card in cards)
            {
                if (card is MagicCardPhysical physical)
                    WriteMagicCardPhysical(writer, physical);
                else
                    throw new Exception("Can't export non-physical cards");
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    static void WriteMagicCardPhysical(Utf8JsonWriter writer, MagicCardPhysical physical)
    {
        writer.WriteStartObject();

        // Reference core — always written
        writer.WriteString("id", physical.Card.Id);
        writer.WriteString("name", physical.Name);
        writer.WriteString("set", physical.SetCode ?? string.Empty);
        writer.WriteNumber("count", physical.Count);

        // Optional fields — only when not at default
        if (!physical.IsOwn)
            writer.WriteBoolean("own", false);

        if (physical.MyPrice != 0f && float.IsFinite(physical.MyPrice))
        {
            // Raw number token: shortest round-trippable representation (e.g. "0.1"),
            // i.e. MyPrice.ToString(InvariantCulture).
            writer.WritePropertyName("price");
            writer.WriteRawValue(physical.MyPrice.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrEmpty(physical.Comment))
            writer.WriteString("comment", physical.Comment);

        if (!string.IsNullOrEmpty(physical.Custom))
            writer.WriteString("custom", physical.Custom);

        if (!string.IsNullOrEmpty(physical.SpecialTags))
            writer.WriteString("special", physical.SpecialTags);

        // ForTradeCount — same rule as the legacy XML format: only when > 0 and the fortrade tag is absent
        var forTrade = physical.GetForTradeCount();
        if (forTrade > 0 && !physical.HasSpecialTag("fortrade"))
            writer.WriteNumber("forTrade", forTrade);

        writer.WriteEndObject();
    }
}
