using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagicAssistant.Http;

/// <summary>
/// Converts any JSON value (number, string, null) to a string.
/// Scryfall returns CMC as a number (e.g. 2), but we store it as string.
/// </summary>
public class StringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => null,
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value);
    }
}
