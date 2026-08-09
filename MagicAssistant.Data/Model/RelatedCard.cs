namespace MagicAssistant.Data.Model;

/// <summary>
/// Represents a related card from the Scryfall <c>all_parts</c> array.
/// Cards that are closely related to other cards (because they call them by name,
/// generate a token, or meld, etc) have an <c>AllParts</c> collection
/// containing RelatedCard objects.
/// </summary>
public class RelatedCard
{
    /// <summary>
    /// A unique ID for this related card in Scryfall's database.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// A content type for this object, always "related_card".
    /// </summary>
    public string? ObjectType { get; set; }

    /// <summary>
    /// A field explaining what role this card plays in this relationship.
    /// One of: token, meld_part, meld_result, or combo_piece.
    /// </summary>
    public string? Component { get; set; }

    /// <summary>
    /// The name of this particular related card.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The type line of this related card.
    /// </summary>
    public string? TypeLine { get; set; }

    /// <summary>
    /// A URI where you can retrieve a full object describing this card on Scryfall's API.
    /// </summary>
    public string? Uri { get; set; }
}
