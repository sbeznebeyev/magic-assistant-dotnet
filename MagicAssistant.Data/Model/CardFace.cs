namespace MagicAssistant.Data.Model;

/// <summary>
/// Represents a single face of a multifaced card (transform, mdfc, split, flip, reversible).
/// Each face has its own name, mana cost, type line, oracle text, power/toughness, and image.
/// </summary>
public class CardFace
{
    /// <summary>
    /// The name of this particular face.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The mana cost for this face. Empty string "" if absent.
    /// </summary>
    public string? ManaCost { get; set; }

    /// <summary>
    /// The type line of this particular face.
    /// </summary>
    public string? TypeLine { get; set; }

    /// <summary>
    /// The Oracle text for this face, if any.
    /// </summary>
    public string? OracleText { get; set; }

    /// <summary>
    /// This face's power, if any. May be non-numeric (e.g. "*").
    /// </summary>
    public string? Power { get; set; }

    /// <summary>
    /// Parsed integer value of this face's power. Null if not a valid integer.
    /// </summary>
    public int? PowerNumber { get; set; }

    /// <summary>
    /// This face's toughness, if any. May be non-numeric (e.g. "*").
    /// </summary>
    public string? Toughness { get; set; }

    /// <summary>
    /// Parsed integer value of this face's toughness. Null if not a valid integer.
    /// </summary>
    public int? ToughnessNumber { get; set; }

    /// <summary>
    /// The mana value of this particular face (for reversible cards).
    /// Stored as INTEGER in SQLite. Imported with Math.Floor rounding.
    /// Null if the value cannot be parsed as a number.
    /// </summary>
    public int? CmcNumber { get; set; }

    /// <summary>
    /// Original CMC string from Scryfall API for this face.
    /// Preserves the raw value (e.g. "0.5", "3") for reference.
    /// </summary>
    public string? CmcText { get; set; }

    /// <summary>
    /// This face's colors, if the game defines colors for the individual face.
    /// </summary>
    public ICollection<string>? Colors { get; set; }

    /// <summary>
    /// The colors in this face's color indicator, if any.
    /// </summary>
    public ICollection<string>? ColorIndicator { get; set; }

    /// <summary>
    /// Bitmask of colors for this face (ColorBitFlags). Computed from ManaCost + Colors.
    /// </summary>
    public int ColorBits { get; set; }

    /// <summary>
    /// Bitmask of color identity for this face (ColorBitFlags).
    /// For faces, identity = colors (no oracle text on individual faces).
    /// </summary>
    public int IdentityBits { get; set; }

    /// <summary>
    /// This face's loyalty, if any. May be non-numeric (e.g. "X").
    /// </summary>
    public string? Loyalty { get; set; }

    /// <summary>
    /// The name of the illustrator of this card face.
    /// </summary>
    public string? Artist { get; set; }

    /// <summary>
    /// A unique identifier for the card face artwork that remains consistent across reprints.
    /// </summary>
    public Guid? IllustrationId { get; set; }

    /// <summary>
    /// Scryfall image URIs for this face (small, normal, large, png).
    /// Persisted to CardFacesImageUris table.
    /// For double-sided cards each face has its own image_uris.
    /// For non-double-sided cards, image_uris lives on the parent card object instead.
    /// </summary>
    public ImageUris? ImageUris { get; set; }

    /// <summary>
    /// The flavor text printed on this face, if any.
    /// </summary>
    public string? FlavorText { get; set; }

    /// <summary>
    /// The localized name printed on this face, if any.
    /// </summary>
    public string? PrintedName { get; set; }

    /// <summary>
    /// The localized text printed on this face, if any.
    /// </summary>
    public string? PrintedText { get; set; }

    /// <summary>
    /// The localized type line printed on this face, if any.
    /// </summary>
    public string? PrintedTypeLine { get; set; }
}
