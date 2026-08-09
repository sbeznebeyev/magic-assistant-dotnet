using System.Text.Json.Serialization;

namespace MagicAssistant.Http.Dtos;

/// <summary>
/// Represents an individual Magic: The Gathering card from the Scryfall API.
/// Cards are the API's most complex object. See https://scryfall.com/docs/api/cards for full details.
/// </summary>
public record ScryfallCardDto
{
    /// <summary>
    /// A unique ID for this card in Scryfall's database.
    /// </summary>
    [property: JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>
    /// A unique ID for this card’s oracle identity. This value is consistent across reprinted card editions, and unique among different cards with the same name (tokens, Unstable variants, etc). Always present except for the reversible_card layout where it will be absent; oracle_id will be found on each face instead. 
    /// </summary>
    [property: JsonPropertyName("oracle_id")]
    public Guid OracleId { get; init; }

    /// <summary>
    /// The name of this card. If the card has multiple faces, this field contains both names separated by " // ".
    /// </summary>
    [property: JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The mana cost for this card. This value will be an empty string "" if the cost is absent.
    /// Multi-faced cards report this value in card_faces objects instead.
    /// </summary>
    [property: JsonPropertyName("mana_cost")]
    public string? ManaCost { get; init; }

    /// <summary>
    /// The type line of this card (e.g. "Creature — Human Warrior").
    /// </summary>
    [property: JsonPropertyName("type_line")]
    public string? TypeLine { get; init; }

    /// <summary>
    /// The Oracle text for this card, if any.
    /// </summary>
    [property: JsonPropertyName("oracle_text")]
    public string? OracleText { get; init; }

    /// <summary>
    /// This card's power, if any. Note that some cards have powers that are not numeric, such as *.
    /// </summary>
    [property: JsonPropertyName("power")]
    public string? Power { get; init; }

    /// <summary>
    /// This card's toughness, if any. Note that some cards have toughnesses that are not numeric, such as *.
    /// </summary>
    [property: JsonPropertyName("toughness")]
    public string? Toughness { get; init; }

    /// <summary>
    /// This card's set code (e.g. "m19").
    /// </summary>
    [property: JsonPropertyName("set")]
    public string Set { get; init; } = string.Empty;

    /// <summary>
    /// This card's rarity. One of common, uncommon, rare, special, mythic, or bonus.
    /// </summary>
    [property: JsonPropertyName("rarity")]
    public string Rarity { get; init; } = string.Empty;

    /// <summary>
    /// The name of the illustrator of this card. Newly spoiled cards may not have this field yet.
    /// </summary>
    [property: JsonPropertyName("artist")]
    public string? Artist { get; init; }

    /// <summary>
    /// A language code for this printing (e.g. "en", "ru").
    /// </summary>
    [property: JsonPropertyName("lang")]
    public string? Language { get; init; }

    /// <summary>
    /// This card's collector number. Note that collector numbers can contain non-numeric characters, such as letters or ★.
    /// </summary>
    [property: JsonPropertyName("collector_number")]
    public string? CollectorNumber { get; init; }

    /// <summary>
    /// The card's mana value (converted mana cost). Note that some funny cards have fractional mana costs.
    /// </summary>
    [property: JsonPropertyName("cmc")]
    [property: JsonConverter(typeof(StringConverter))]
    public string? Cmc { get; init; }

    /// <summary>
    /// This card's colors, if the overall card has colors defined by the rules.
    /// Otherwise the colors will be on the card_faces objects.
    /// </summary>
    [property: JsonPropertyName("colors")]
    public ICollection<string>? Colors { get; init; }

    /// <summary>
    /// This card's color identity.
    /// </summary>
    [property: JsonPropertyName("color_identity")]
    public ICollection<string>? ColorIdentity { get; init; }

    /// <summary>
    /// The colors in this card's color indicator, if any.
    /// A null value for this field indicates the card does not have one.
    /// </summary>
    [property: JsonPropertyName("color_indicator")]
    public ICollection<string>? ColorIndicator { get; init; }

    /// <summary>
    /// True if this card is on the Commander Game Changer list.
    /// </summary>
    [property: JsonPropertyName("game_changer")]
    public bool? GameChanger { get; init; }

    /// <summary>
    /// An array of keywords that this card uses, such as 'Flying' and 'Cumulative upkeep'.
    /// </summary>
    [property: JsonPropertyName("keywords")]
    public ICollection<string>? Keywords { get; init; }

    /// <summary>
    /// Colors of mana that this card could produce.
    /// </summary>
    [property: JsonPropertyName("produced_mana")]
    public ICollection<string>? ProducedMana { get; init; }

    /// <summary>
    /// An object describing the legality of this card across play formats.
    /// Possible legalities are legal, not_legal, restricted, and banned.
    /// </summary>
    [property: JsonPropertyName("legalities")]
    public Dictionary<string, string>? Legalities { get; init; }

    /// <summary>
    /// An array of names this card is also known by (e.g. for cards with alternate names).
    /// </summary>
    [property: JsonPropertyName("card_names")]
    public ICollection<string>? CardNames { get; init; }

    /// <summary>
    /// The side of a double-faced card this object describes (1 or 2). Only present for DFCs.
    /// </summary>
    [property: JsonPropertyName("side")]
    public int? Side { get; init; }

    /// <summary>
    /// A code for this card's layout (e.g. normal, split, transform, mdfc).
    /// See https://scryfall.com/docs/api/layouts for all layout codes.
    /// </summary>
    [property: JsonPropertyName("layout")]
    public string? Layout { get; init; }

    /// <summary>
    /// The date this card was first released.
    /// </summary>
    [property: JsonPropertyName("released_at")]
    public string? ReleasedAt { get; init; }

    /// <summary>
    /// An object listing available imagery for this card (small, normal, art_crop, png).
    /// See https://scryfall.com/docs/api/images for more information.
    /// </summary>
    [property: JsonPropertyName("image_uris")]
    public ImageUrisDto? ImageUris { get; init; }

    /// <summary>
    /// An array of Card Face objects, if this card is multifaced (transform, mdfc, split, flip, reversible).
    /// Each face contains its own name, mana_cost, type_line, oracle_text, power, toughness, image_uris, etc.
    /// </summary>
    [property: JsonPropertyName("card_faces")]
    public ICollection<CardFaceDto>? CardFaces { get; init; }

    /// <summary>
    /// If this card is closely related to other cards (because they call them by name,
    /// generate a token, or meld, etc), this property will be an array with Related Card Objects.
    /// Each related card has: id, object ("related_card"), component (token/meld_part/meld_result/combo_piece),
    /// name, type_line, and uri.
    /// See https://scryfall.com/docs/api/cards#related-card-objects for full details.
    /// </summary>
    [property: JsonPropertyName("all_parts")]
    public ICollection<RelatedCardDto>? AllParts { get; init; }

    /// <summary>
    /// An object containing daily price information for this card, including usd, usd_foil, usd_etched, eur, eur_foil, eur_etched, and tix prices, as strings. 
    /// </summary>
    [property: JsonPropertyName("prices")]
    public ScryfallPricesDto? Prices { get; init; }

    /// <summary>
    /// The localized name printed on this card, if any. 
    /// </summary>
    [property: JsonPropertyName("printed_name")]
    public string? PrintedName { get; init; }

    /// <summary>
    /// The localized text printed on this card, if any.
    /// </summary>
    [property: JsonPropertyName("printed_text")]
    public string? PrintedText { get; init; }

    /// <summary>
    /// The localized type line printed on this card, if any. 
    /// </summary>
    [property: JsonPropertyName("printed_type_line")]
    public string? PrintedTypeLine { get; init; }
}
