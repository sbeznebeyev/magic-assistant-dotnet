using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model.Enums;
using MagicAssistant.Data.Model.Legality;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Data.Model;

// TODO: rename to MyCard
public class MyCard : IMyCard
{
    private string? _comment;
    private int? _forTradeCount;

    public MyCard(MagicCard card)
    {
        Card = card ?? throw new ArgumentNullException(nameof(card));
        Count = 1;
        DateAdded = DateTimeOffset.Now;
    }

    public MyCard(MagicCard card, Collection? collection, bool isOwn) : this(card)
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

    public bool IsSideboard => Collection?.NodeType == NodeType.Sideboard;
    public bool IsPhysical => true;

    // === IMagicCardPhysical implementation ===
    public void SetCollection(Collection? collection) => Collection = collection;

    public int GetForTradeCount() => _forTradeCount ?? 0;

    public void SetForTradeCount(int count)
    {
        _forTradeCount = count > 0 ? count : (int?)null;
    }



    // === get(ICardField) — delegates to MagicCardField ===
    public object? Get(MagicCardField field) => field.Get(this);

    public override string ToString() => $"{Card} x {Count}";
}
