using System.Collections.Concurrent;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model.Enums;
using MagicAssistant.Data.Model.Legality;

namespace MagicAssistant.Data.Model;

/// <summary>
/// TODO: rename to ReferenceCard
/// </summary>
public class Card : ICard
{
    public Card() { }

    // === Basic fields ===
    public Guid Id { get; set; }

    /// <summary>
    /// Scryfall oracle identity ID. Consistent across reprinted card editions;
    /// unique among different cards with the same name (tokens, Unstable variants, etc).
    /// </summary>
    public Guid OracleId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? ManaCost { get; set; }
    public LayoutEnum Layout { get; set; }
    public string? Type { get; set; }
    public string? Power { get; set; }
    /// <summary>Parsed integer value of Power. Null if Power is not a valid integer (e.g. "*", "half", null).</summary>
    public int? PowerNumber { get; set; }
    public string? Toughness { get; set; }
    /// <summary>Parsed integer value of Toughness. Null if Toughness is not a valid integer (e.g. "*", "half", null).</summary>
    public int? ToughnessNumber { get; set; }
    public string? SetCode { get; set; }
    public string? Rarity { get; set; }
    public string? OracleText { get; set; }
    public string? Artist { get; set; }
    public string? Language { get; set; }
    public string? CollectorNumber { get; set; }
    public string? Rulings { get; set; }

    /// <summary>
    /// The card's name as printed on the card. May differ from Name for cards with alternate names.
    /// </summary>
    public string? PrintedName { get; set; }

    /// <summary>
    /// The card's text as printed on the card. May differ from OracleText for cards with alternate rules text.
    /// </summary>
    public string? PrintedText { get; set; }

    /// <summary>
    /// The card's type line as printed on the card. May differ from Type for cards with alternate type lines.
    /// </summary>
    public string? PrintedTypeLine { get; set; }

    // === Computed properties ===
    /// <summary>
    /// Converted Mana Cost (CMC) as integer.
    /// Imported from Scryfall with Math.Floor rounding for fractional values.
    /// Null if the value cannot be parsed as a number.
    /// </summary>
    public int? CmcNumber { get; set; }

    /// <summary>
    /// Original CMC string from Scryfall API. Stored as TEXT in SQLite.
    /// Preserves the raw value (e.g. "0.5", "3") for reference.
    /// </summary>
    public string? CmcText { get; set; }

    public string? ColorType { get; set; }

    /// <summary>
    /// Bitmask of colors derived from ManaCost (ColorBitFlags).
    /// Bits: C=1, W=2, U=4, B=8, R=16, G=32, MULTI=64, MONO=128, HYBRID=256.
    /// Null for double-faced cards — bits live on CardFaces instead.
    /// </summary>
    public int? ColorBits { get; set; }

    /// <summary>
    /// Bitmask of color identity derived from ManaCost + OracleText (ColorBitFlags).
    /// Same bit layout as ColorBits. Null for double-faced cards — bits live on CardFaces.
    /// </summary>
    public int? IdentityBits { get; set; }

    // === Additional fields ===
    public LegalityMap LegalityMap { get; set; } = LegalityMap.Empty;

    /// <summary>
    /// Legal formats
    /// </summary>
    public string Legal { get; set; }

    /// <summary>
    /// USD price for this card (from Scryfall prices.usd).
    /// Stored as TEXT in SQLite.
    /// </summary>
    public string? Usd { get; set; }

    /// <summary>
    /// USD Foil price for this card (from Scryfall prices.usd_foil).
    /// Stored as TEXT in SQLite.
    /// </summary>
    public string? UsdFoil { get; set; }

    /// <summary>
    /// Scryfall image URIs for this card (small, normal, large, png).
    /// Persisted to CardsImageUris table.
    /// </summary>
    public ImageUris? ImageUris { get; set; }

    // === Scryfall gameplay fields ===
    /// <summary>
    /// This card's colors (W, U, B, R, G). Null for colorless cards.
    /// Stored as a comma-separated string in the database (e.g. "W,U").
    /// </summary>
    public string[]? Colors { get; set; }

    /// <summary>
    /// This card's color identity: all colors appearing in mana cost, rules text, and loyalty symbols.
    /// Computed from Scryfall's color_identity field.
    /// Stored as a comma-separated string in the database (e.g. "W,U").
    /// </summary>
    public string[]? ColorIdentity { get; set; }

    /// <summary>
    /// True if this card is on the Commander Game Changer list.
    /// </summary>
    public bool? GameChanger { get; set; }

    // === Multifaced card support ===
    /// <summary>
    /// Array of card faces for multifaced cards (transform, mdfc, split, flip, reversible).
    /// Each face contains its own name, mana cost, type line, oracle text, power/toughness, and image URL.
    /// Null for single-faced cards.
    /// </summary>
    public CardFace[]? CardFaces { get; set; }

    // === Related cards (all_parts) support ===
    /// <summary>
    /// Array of related cards from Scryfall's all_parts array.
    /// Cards that are closely related to other cards (because they call them by name,
    /// generate a token, or meld, etc) have an AllParts array containing RelatedCard objects.
    /// Each RelatedCard has: Id, Component (token/meld_part/meld_result/combo_piece), Name, TypeLine, Uri.
    /// Null for cards without related parts.
    /// </summary>
    public RelatedCard[]? AllParts { get; set; }

    // === Edition reference (set after loading) ===
    private CardSet? _cardSet;
    public CardSet? CardSet
    {
        get => _cardSet;
        set => _cardSet = value;
    }

    // === ICard implementation ===
    public int Count => 1; // reference card always 1
    public Collection? Collection => null; // reference card can't be in any location
    public bool IsSideboard => false; // reference card can't be in a sideboard 

    public bool IsPhysical => false;

    // === IMagicCard implementation ===
    public ICard Clone()
    {
        return (Card)MemberwiseClone();
    }

    public Card GetBase() => this;

    public bool Has2Faces => Layout is LayoutEnum.transform or LayoutEnum.modal_dfc or LayoutEnum.meld or LayoutEnum.double_faced_token or LayoutEnum.reversible_card;

    public bool IsBasicLand()
    {
        if (Type == null) return false;
        return Type.Contains("basic", StringComparison.OrdinalIgnoreCase) &&
               Type.Contains("land", StringComparison.OrdinalIgnoreCase);
    }

    // === Helpers ===
    public override string ToString() => $"{Id}: {Name} [{SetCode}]";

    public override bool Equals(object? obj)
    {
        if (obj is not Card other) return false;
        if (ReferenceEquals(this, other)) return true;

        // If both have non-empty CardId, compare by Id
        if (Id != Guid.Empty && other.Id != Guid.Empty)
        {
            return Id == other.Id;
        }

        // Fallback: compare by Name + SetCode (for cards without CardId)
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) && SetCode == other.SetCode;
    }

}
