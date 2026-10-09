using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model.Enums;
using MagicAssistant.Data.Model.Legality;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Data.Model;

// TODO: rename to MyCard
public class MagicCardPhysical : IMagicCardPhysical
{
    private string? _comment;
    private int? _forTradeCount;
    private object? _error;

    public MagicCardPhysical(MagicCard card)
    {
        Card = card ?? throw new ArgumentNullException(nameof(card));
        Count = 1;
        DateAdded = DateTimeOffset.Now;
    }

    public MagicCardPhysical(MagicCard card, Collection? collection, bool isOwn) : this(card)
    {
        Collection = collection;
        IsOwn = isOwn;
    }

    // === Core reference ===
    public MagicCard Card { get; private set; }

    /// <summary>
    /// Replace the base card reference (used during reconciliation).
    /// Mirrors Java: MagicCardPhysical.setMagicCard()
    /// </summary>
    public void SetBaseCard(MagicCard card)
    {
        Card = card ?? throw new ArgumentNullException(nameof(card));
    }

    // === Physical properties ===
    public int Count { get; set; }
    public Collection? Collection { get; set; }
    public bool IsOwn { get; set; }
    public DateTimeOffset DateAdded { get; set; }

    // === User-defined properties (direct fields — no _properties dict) ===
    public string? Comment
    {
        get => _comment;
        set => _comment = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    // === IMagicCard delegation to base card ===
    public Guid Id => Card.Id;
    public string Name => Card.Name;
    public string? ManaCost => Card.ManaCost;
    public LayoutEnum Layout => Card.Layout;
    public string? Type => Card.Type;
    public string? Power => Card.Power;
    public string? Toughness => Card.Toughness;
    public string? SetCode => Card.SetCode;
    public string? Rarity => Card.Rarity;
    public string? OracleText => Card.OracleText;
    public string? Artist => Card.Artist;
    public string? Language => Card.Language;
    public int? CmcNumber => Card.CmcNumber;
    public string? CmcText => Card.CmcText;
    public string? ColorType => Card.ColorType;
    /// <summary>
    /// Per-instance price, persisted in Deck.MyPrice (one per collection row).
    /// </summary>
    public float MyPrice { get; set; }

    /// <summary>
    /// Condition grade, persisted in Deck.Grade as a nullable integer (NULL = not graded).
    /// </summary>
    public CardGrade? Grade { get; set; }

    // === Deep clone — mirrors Java MagicCardPhysical.clone() ===
    public IMagicCard Clone()
    {
        return (MagicCardPhysical)MemberwiseClone();
    }

    public MagicCard GetBase() => Card;
    public string? Text => Card.Text;
    public int Side => Card.Side;
    public int CollectorNumberId => Card.CollectorNumberId;
    public LegalityMap LegalityMap => Card.LegalityMap;
    public bool IsBasicLand() => Card.IsBasicLand();
    public CardSet? CardSet => Card.CardSet;

    // === ICard implementation ===

    /// <summary>
    /// Owned copies of THIS physical instance.
    /// </summary>
    public int OwnCount => IsOwn ? Count : 0;

    /// <summary>
    /// Total copies of this card across all decks (owned + borrowed).
    /// Delegates to base KB card which reads from OwnCounts properties.
    /// </summary>
    public int OwnTotal => Card.OwnTotal;

    public bool IsSideboard => Collection?.NodeType == NodeType.Sideboard;
    public int UniqueCount => 1;
    public bool IsPhysical => true;

    // === ICardModifiable implementation ===
    void ICardModifiable.SetCount(int count) => Count = count;

    // === IMagicCardPhysical implementation ===
    public void SetCollection(Collection? collection) => Collection = collection;

    public int GetForTradeCount() => _forTradeCount ?? 0;

    public void SetForTradeCount(int count)
    {
        _forTradeCount = count > 0 ? count : (int?)null;
    }



    // === Matching (mirrors Java MagicCardPhysical.matching()) ===
    /// <summary>
    /// Compares two physical cards ignoring count and location.
    /// Used to determine if two physical instances represent the same card state.
    /// </summary>
    public bool Matching(MagicCardPhysical other)
    {
        if (!Card.Equals(other.Card)) return false;
        if (IsOwn != other.IsOwn) return false;
        if (MyPrice != other.MyPrice) return false;
        if (!EqualsNull(Comment, other.Comment)) return false;
        return true;
    }

    private static bool EqualsNull(string? a, string? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a == b;
    }

    // === get(ICardField) — delegates to MagicCardField ===
    public object? Get(MagicCardField field) => field.Get(this);

    // === Creature count helper ===
    public int GetCreatureCount()
    {
        var type = Card.Type;
        if (type == null || !type.Contains("Creature", StringComparison.OrdinalIgnoreCase)) return 0;
        if (Card.Power == null || string.IsNullOrEmpty(Card.Power)) return 0;
        return Math.Max(1, Count);
    }

    public int Count4 => Math.Min(OwnCount, 4);

    // === Error field (for import errors) ===
    public object? GetError() => _error;
    public void SetError(object? value) => _error = value;

    // === Equality ===
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

    public override bool Equals(object? obj)
    {
        if (obj is MagicCardPhysical other)
            return Card.Equals(other.Card) && Count == other.Count && CollectionId(Collection) == CollectionId(other.Collection);
        if (obj is MagicCard mc)
            return Card.Equals(mc);
        return false;
    }

    private static int CollectionId(Collection? c) => c?.Id ?? 0;

    public override string ToString() => $"{Card} x {Count}";
}
