using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model.Enums;
using MagicAssistant.Data.Model.Legality;

namespace MagicAssistant.Data.Model;

// TODO: rename to MyCard
public class MagicCardPhysical : IMagicCardPhysical
{
    private string? _comment;
    private string? _specialTags;
    private string? _custom;
    private int? _forTradeCount;
    private object? _error;

    public MagicCardPhysical(MagicCard card)
    {
        Card = card ?? throw new ArgumentNullException(nameof(card));
        Count = 1;
        DateAdded = DateTimeOffset.Now;
    }

    public MagicCardPhysical(MagicCard card, Location location, bool isOwn) : this(card)
    {
        Location = location;
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
    public Location Location { get; set; } = Location.NoWhere;
    public bool IsOwn { get; set; }
    public DateTimeOffset DateAdded { get; set; }

    // === User-defined properties (direct fields — no _properties dict) ===
    public string? Comment
    {
        get => _comment;
        set => _comment = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public string? SpecialTags
    {
        get => _specialTags;
        set => _specialTags = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public string? Custom
    {
        get => _custom;
        set => _custom = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
    public float CommunityRating => Card.CommunityRating;

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
    public string? EnglishName => Card.EnglishName;
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

    /// <summary>
    /// Total copies across all locations (decks + library).
    /// Delegates to base KB card which reads from OwnCounts properties.
    /// </summary>
    public int OwnTotalAll => Card.OwnTotalAll;

    /// <summary>
    /// Whether this physical instance represents a unique owned copy.
    /// </summary>
    public int OwnUnique => IsOwn ? 1 : 0;

    public bool IsSideboard => Location.IsSideboard;
    public int UniqueCount => 1;
    public bool IsPhysical => true;

    // === ICardModifiable implementation ===
    void ICardModifiable.SetCount(int count) => Count = count;

    // === IMagicCardPhysical implementation ===
    public void SetLocation(Location location) => Location = location;

    public bool HasSpecialTag(string tag)
    {
        var tags = SpecialTags;
        return !string.IsNullOrEmpty(tags) &&
               tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(tag);
    }

    public int GetForTradeCount()
    {
        if (_forTradeCount.HasValue) return _forTradeCount.Value;
        // Fall back to checking forTrade tag
        if (HasSpecialTag("fortrade")) return Count;
        return 0;
    }

    public void SetForTradeCount(int count)
    {
        _forTradeCount = count > 0 ? count : (int?)null;
    }



    // === Special tag helpers (mirrors Java MagicCardPhysical) ===
    public bool IsSpecialTag(string key)
    {
        var value = GetSpecialTagValue(key);
        return bool.TryParse(value, out var result) && result;
    }

    public string? GetSpecialTagValue(string key)
    {
        var tags = SpecialTags;
        if (string.IsNullOrEmpty(tags)) return null;

        foreach (var pair in tags.Split(','))
        {
            var parts = pair.Trim().Split('=');
            if (parts[0] == key)
                return parts.Length > 1 ? parts[1] : "true";
        }
        return null;
    }

    public void SetSpecialTag(string tag)
    {
        var tags = _specialTags ?? string.Empty;
        if (!HasSpecialTag(tag))
            _specialTags = string.IsNullOrEmpty(tags) ? tag : tags + "," + tag;
    }

    public void RemoveSpecialTag(string tag)
    {
        var tags = _specialTags;
        if (string.IsNullOrEmpty(tags)) return;

        var remaining = tags.Split(',')
            .Select(t => t.Trim())
            .Where(t => !t.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _specialTags = remaining.Any() ? string.Join(",", remaining) : null;
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
        if (!EqualsNull(Custom, other.Custom)) return false;
        if (!EqualsNull(Comment, other.Comment)) return false;
        if (!EqualsNull(SpecialTags, other.SpecialTags)) return false;
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

    // === Trade split (mirrors Java MagicCardPhysical.tradeSplit()) ===
    /// <summary>
    /// Splits this card into a "for trade" and "not for trade" instance.
    /// Returns the trade copy, or null if no split is needed.
    /// </summary>
    public MagicCardPhysical? TradeSplit(int count, int fcount)
    {
        _forTradeCount = null;
        RemoveSpecialTag("fortrade");

        count = Math.Max(0, count);
        fcount = Math.Max(0, fcount);

        // Trade count is 0 or less — card is not for trade
        if (fcount == 0)
        {
            Count = count;
            return null;
        }

        // Trade count > total count — invalid input, don't mutate state
        if (fcount > count)
        {
            return null;
        }

        // Trade count == total count — entire pile is for trade
        if (fcount == count)
        {
            SetSpecialTag("fortrade");
            return null;
        }

        // Split: some cards for trade, some not
        Count = count - fcount;
        var tradeCopy = (MagicCardPhysical)Clone();
        tradeCopy.Count = fcount;
        tradeCopy.SetSpecialTag("fortrade");
        return tradeCopy;
    }

    // === Error field (for import errors) ===
    public object? GetError() => _error;
    public void SetError(object? value) => _error = value;

    // === Equality ===
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

    public override bool Equals(object? obj)
    {
        if (obj is MagicCardPhysical other)
            return Card.Equals(other.Card) && Count == other.Count && Location.Equals(other.Location);
        if (obj is MagicCard mc)
            return Card.Equals(mc);
        return false;
    }

    public override string ToString() => $"{Card} x {Count}";
}
