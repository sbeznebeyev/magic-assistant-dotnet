using System.Text.Json.Serialization;

namespace MagicAssistant.Http.Dtos;

/// <summary>
/// Represents a single face of a multifaced card (transform, mdfc, split, flip, reversible).
/// Each face has its own name, mana cost, type line, oracle text, power/toughness, and image.
/// See https://scryfall.com/docs/api/cards#card-face-objects for full details.
/// </summary>
public record CardFaceDto
{
    /// <summary>
    /// The name of this particular face.
    /// </summary>
    [property: JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The mana cost for this face. This value will be an empty string "" if the cost is absent.
    /// </summary>
    [property: JsonPropertyName("mana_cost")]
    public string? ManaCost { get; init; }

    /// <summary>
    /// The type line of this particular face, if the card is reversible.
    /// </summary>
    [property: JsonPropertyName("type_line")]
    public string? TypeLine { get; init; }

    /// <summary>
    /// The Oracle text for this face, if any.
    /// </summary>
    [property: JsonPropertyName("oracle_text")]
    public string? OracleText { get; init; }

    /// <summary>
    /// This face's power, if any. Note that some cards have powers that are not numeric, such as *.
    /// </summary>
    [property: JsonPropertyName("power")]
    public string? Power { get; init; }

    /// <summary>
    /// This face's toughness, if any. Note that some cards have toughnesses that are not numeric, such as *.
    /// </summary>
    [property: JsonPropertyName("toughness")]
    public string? Toughness { get; init; }

    /// <summary>
    /// The mana value of this particular face, if the card is reversible.
    /// </summary>
    [property: JsonPropertyName("cmc")]
    [property: JsonConverter(typeof(StringConverter))]
    public string? Cmc { get; init; }

    /// <summary>
    /// This face's colors, if the game defines colors for the individual face of this card.
    /// </summary>
    [property: JsonPropertyName("colors")]
    public ICollection<string>? Colors { get; init; }

    /// <summary>
    /// The colors in this face's color indicator, if any.
    /// </summary>
    [property: JsonPropertyName("color_indicator")]
    public ICollection<string>? ColorIndicator { get; init; }

    /// <summary>
    /// This face's loyalty, if any. Note that some cards have loyalties that are not numeric, such as X.
    /// </summary>
    [property: JsonPropertyName("loyalty")]
    public string? Loyalty { get; init; }

    /// <summary>
    /// The name of the illustrator of this card face. Newly spoiled cards may not have this field yet.
    /// </summary>
    [property: JsonPropertyName("artist")]
    public string? Artist { get; init; }

    /// <summary>
    /// A unique identifier for the card face artwork that remains consistent across reprints.
    /// Newly spoiled cards may not have this field yet.
    /// </summary>
    [property: JsonPropertyName("illustration_id")]
    public Guid? IllustrationId { get; init; }

    /// <summary>
    /// An object providing URIs to imagery for this face, if this is a double-sided card.
    /// If this card is not double-sided, then the image_uris property will be part of the parent object instead.
    /// </summary>
    [property: JsonPropertyName("image_uris")]
    public ImageUrisDto? ImageUris { get; init; }

    /// <summary>
    /// The flavor text printed on this face, if any.
    /// </summary>
    [property: JsonPropertyName("flavor_text")]
    public string? FlavorText { get; init; }

    /// <summary>
    /// The localized name printed on this face, if any.
    /// </summary>
    [property: JsonPropertyName("printed_name")]
    public string? PrintedName { get; init; }

    /// <summary>
    /// The localized text printed on this face, if any.
    /// </summary>
    [property: JsonPropertyName("printed_text")]
    public string? PrintedText { get; init; }

    /// <summary>
    /// The localized type line printed on this face, if any.
    /// </summary>
    [property: JsonPropertyName("printed_type_line")]
    public string? PrintedTypeLine { get; init; }

    /// <summary>
    /// This face's defense, if any. Only present for Vanguard cards.
    /// </summary>
    [property: JsonPropertyName("defense")]
    public string? Defense { get; init; }

    /// <summary>
    /// The watermark on this particular card face, if any.
    /// </summary>
    [property: JsonPropertyName("watermark")]
    public string? Watermark { get; init; }

    /// <summary>
    /// The Oracle ID of this particular face, if the card is reversible.
    /// </summary>
    [property: JsonPropertyName("oracle_id")]
    public Guid? OracleId { get; init; }

    /// <summary>
    /// The layout of this card face, if the card is reversible.
    /// </summary>
    [property: JsonPropertyName("layout")]
    public string? Layout { get; init; }
}
