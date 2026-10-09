using System.Text.Json;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Data.IO.json;

/// <summary>
/// Imports card collections from the native Magic Assistant JSON format (v1).
///
/// The reference core (id / name / set / count) is required on every card —
/// a missing or invalid core field is a format error. Optional fields fall back
/// to defaults when absent (own=true, price=0, empty comment/custom, forTrade=0;
/// a legacy "fortrade" tag in "special" maps to forTrade).
/// Unknown properties are ignored (forward compatibility with future versions).
///
/// The top-level "sideboard" array is optional and uses the exact same card schema
/// as "cards"; it lands in <see cref="CardCollectionImportResult.Sideboard"/>.
/// Absent or empty → an empty Sideboard list.
/// </summary>
public static class JsonImport
{
    public static CardCollectionImportResult ImportFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON content is required.", nameof(json));

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Invalid Magic Assistant JSON: {ex.Message}", ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new FormatException("Invalid Magic Assistant JSON: root must be an object.");

            var root = document.RootElement;

            // Format marker + version gate. Unknown *fields* are ignored, but a
            // mismatched format or an unsupported version cannot be parsed safely.
            if (root.TryGetProperty("format", out var format)
                && format.ValueKind == JsonValueKind.String
                && !string.Equals(format.GetString(), JsonExport.FormatId, StringComparison.Ordinal))
                throw new FormatException($"Not a Magic Assistant deck file (format '{format.GetString()}').");

            if (root.TryGetProperty("version", out var version)
                && version.ValueKind == JsonValueKind.Number
                && version.GetInt32() != JsonExport.Version)
                throw new FormatException(
                    $"Unsupported Magic Assistant JSON version {version.GetInt32()} (expected {JsonExport.Version}).");

            var result = new CardCollectionImportResult
            {
                Name = GetString(root, "name") ?? string.Empty,
                Comment = GetString(root, "comment"),
                Type = GetString(root, "type"),
            };

            if (!root.TryGetProperty("cards", out var cards) || cards.ValueKind != JsonValueKind.Array)
                throw new FormatException("Invalid Magic Assistant JSON: 'cards' array is required.");

            foreach (var cardElement in cards.EnumerateArray())
                result.Cards.Add(ReadMagicCardPhysical(cardElement));

            // Optional sideboard — same per-card rules as the main list.
            if (root.TryGetProperty("sideboard", out var sideboard) &&
                sideboard.ValueKind != JsonValueKind.Null)
            {
                if (sideboard.ValueKind != JsonValueKind.Array)
                    throw new FormatException("Invalid Magic Assistant JSON: 'sideboard' must be an array.");

                foreach (var cardElement in sideboard.EnumerateArray())
                    result.Sideboard.Add(ReadMagicCardPhysical(cardElement));
            }

            return result;
        }
    }

    static MagicCardPhysical ReadMagicCardPhysical(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new FormatException("Invalid card entry: expected an object.");

        // Reference core — all four are required
        var id = RequireString(element, "id");
        if (!Guid.TryParse(id, out var cardId))
            throw new FormatException($"Card '{id}': id must be a Scryfall UUID (GUID).");

        var name = RequireString(element, "name");
        var setCode = RequireString(element, "set").ToLowerInvariant();

        if (!element.TryGetProperty("count", out var countElement) || countElement.ValueKind != JsonValueKind.Number)
            throw new FormatException($"Card '{name}': 'count' is required.");
        var count = countElement.GetInt32();
        if (count < 1)
            throw new FormatException($"Card '{name}': count must be >= 1.");

        var physical = new MagicCardPhysical(
            new MagicCard { Id = cardId, Name = name, SetCode = setCode })
        {
            Count = count,
            // Optional fields — defaults when absent
            IsOwn = element.TryGetProperty("own", out var own) && own.ValueKind == JsonValueKind.True
                ? true
                : element.TryGetProperty("own", out var own2) && own2.ValueKind == JsonValueKind.False
                    ? false
                    : true,
        };

        if (element.TryGetProperty("price", out var price) && price.ValueKind == JsonValueKind.Number)
            physical.MyPrice = (float)price.GetDouble();

        physical.Comment = GetString(element, "comment");
        physical.Custom = GetString(element, "custom");
        // Legacy bridge: a "fortrade" tag in "special" marks the whole pile as for trade
        var specialTags = GetString(element, "special");
        if (!string.IsNullOrEmpty(specialTags) &&
            specialTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains("fortrade"))
            physical.SetForTradeCount(physical.Count);

        if (element.TryGetProperty("forTrade", out var forTrade) && forTrade.ValueKind == JsonValueKind.Number)
        {
            var ft = forTrade.GetInt32();
            if (ft > 0)
                physical.SetForTradeCount(ft);
        }

        return physical;
    }

    /// <summary>Reads a string property; null/absent/whitespace → null.</summary>
    static string? GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Null) return null;
        var s = value.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    /// <summary>Reads a required non-empty string property; throws FormatException when absent.</summary>
    static string RequireString(JsonElement element, string name)
    {
        var value = GetString(element, name);
        if (value is null)
            throw new FormatException($"Missing required field '{name}'.");
        return value;
    }
}
