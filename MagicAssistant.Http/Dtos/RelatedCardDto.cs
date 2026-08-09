using System.Text.Json.Serialization;

namespace MagicAssistant.Http.Dtos;

/// <summary>
/// Represents a related card object inside the <c>all_parts</c> array of a Scryfall card.
/// Cards that are closely related to other cards (because they call them by name,
/// generate a token, or meld, etc) have an <c>all_parts</c> property
/// that contains Related Card objects.
/// See https://scryfall.com/docs/api/cards#related-card-objects for full details.
/// </summary>
public record RelatedCardDto
{
    /// <summary>
    /// A unique ID for this card in Scryfall's database.
    /// </summary>
    [property: JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>
    /// A content type for this object, always "related_card".
    /// </summary>
    [property: JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>
    /// A field explaining what role this card plays in this relationship.
    /// One of: token, meld_part, meld_result, or combo_piece.
    /// </summary>
    [property: JsonPropertyName("component")]
    public string? Component { get; init; }

    /// <summary>
    /// The name of this particular related card.
    /// </summary>
    [property: JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The type line of this related card.
    /// </summary>
    [property: JsonPropertyName("type_line")]
    public string? TypeLine { get; init; }

    /// <summary>
    /// A URI where you can retrieve a full object describing this card on Scryfall's API.
    /// </summary>
    [property: JsonPropertyName("uri")]
    public string? Uri { get; init; }
}
