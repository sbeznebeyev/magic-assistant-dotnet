using System;
using System.Collections.Generic;
using System.Linq;
using MagicAssistant.Core.Expressions;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.SearchableProperties;

namespace MagicAssistant.Core;

public class MagicCardFilter : ICloneable
{
    private Expr _root = Expr.True;
    private SortOrder _sortOrder = new();
    private GroupOrder _groupOrder = new();
    private bool _onlyLastSet = false;
    private bool _nameGrouping = true;

    public Expr Root => _root;

    public static Expr TokenSearch(MagicCardField field, SearchStringTokenizer.SearchToken token)
    {
        string value = token.Value;
        if (token.Type == SearchStringTokenizer.TokenType.Regex)
        {
            var tValue = new TextValue(value, false, false, true);
            return new BinaryExpr(new CardFieldExpr(field), Operation.MATCHES, tValue);
        }
        else if (token.Type == SearchStringTokenizer.TokenType.Abi)
        {
            var tValue = new TextValue(value, false, false, true);
            return new BinaryExpr(new CardFieldExpr(field), Operation.MATCHES, tValue);
        }
        
        var textValue = new TextValue(value, false, false, false);
        if (char.IsLetter(value[0]) && token.Type != SearchStringTokenizer.TokenType.Quoted)
        {
            textValue.WordBoundary = true;
        }
        return new BinaryExpr(new CardFieldExpr(field), Operation.MATCHES, textValue);
    }

    public void SetFilter(Expr root)
    {
        _root = root ?? Expr.True;
    }

    public override string ToString() => _root.ToString();

    public void Update(IDictionary<string, string> map)
    {
        Expr expr = Expr.True;

        // Colors — complex group
        expr = expr.And(CreateColorGroup(map));

        // Basic fields — mapped to CardField constants
        var fieldFilters = new[]
        {
            "type", "edition", "location", "rarity", "lang",
            "typeLine", "nameLine", "power", "toughness", "cmc",
            "count", "price", "dbPrice", "comment", "ownership",
            "communityRating", "collNum", "artist", "special",
            "forTradeCount", "format"
        };

        foreach (var fieldName in fieldFilters)
        {
            if (map.TryGetValue(fieldName, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                var field = MagicCardFieldExtensions.FieldByName(fieldName);
                if (field != null)
                {
                    expr = expr.And(BinaryExpr.FieldEquals(field.Value, value));
                }
            }
        }

        // Text fields — OR-ed together, then ANDed with the rest
        Expr textOr = Expr.Empty;
        var textFields = new[] { "textLine1", "textLine2", "textLine3" };
        foreach (var f in textFields)
        {
            if (map.TryGetValue(f, out var val))
            {
                var field = MagicCardFieldExtensions.FieldByName(f);
                if (field != null)
                    textOr = textOr.Or(BinaryExpr.FieldMatches(field.Value, val));
            }
        }

        // Text NOT filters
        Expr textNot = Expr.True;
        var notFields = new[] { "textNot1", "textNot2", "textNot3" };
        foreach (var f in notFields)
        {
            if (map.TryGetValue(f, out var val))
            {
                var field = MagicCardFieldExtensions.FieldByName(f);
                if (field != null)
                    textNot = textNot.And(BinaryExpr.FieldMatches(field.Value, val).Not());
            }
        }

        _root = expr.And(textOr).And(textNot);
    }

    private Expr CreateColorGroup(IDictionary<string, string> map)
    {
        var field = MagicCardField.ColorType;
        bool orOp = true;
        bool only = false;

        if (map.TryGetValue("colorIdentity", out _) || map.TryGetValue("color_identity", out _))
        {
            field = MagicCardField.ColorIdentity;
            only = true;
        }

        if (map.TryGetValue("colorAnd", out _) || map.TryGetValue("color_and", out _))
        {
            orOp = false;
        }

        if (map.TryGetValue("colorOnly", out _) || map.TryGetValue("color_only", out _))
        {
            only = true;
        }

        return CreateGroup(map, ColorTypesSearch.Instance, orOp, only, field);
    }

    private Expr CreateGroup(IDictionary<string, string> map, ISearchableProperty sp, bool orOp, bool notOp, MagicCardField ff)
    {
        Expr positive = Expr.Empty;
        Expr negative = Expr.True;

        foreach (var id in sp.GetIds())
        {
            if (map.TryGetValue(id, out var value))
            {
                bool isTrue = !string.IsNullOrEmpty(value) && value != "false";
                var expr = BinaryExpr.FieldEquals(ff, sp.GetNameById(id));

                if (isTrue)
                {
                    positive = orOp ? positive.Or(expr) : positive.And(expr);
                }
                else if (notOp)
                {
                    negative = negative.And(expr.Not());
                }
            }
        }

        return negative.And(positive);
    }

    public bool IsFiltered(object? o) => _root != Expr.True && !_root.Evaluate(o);

    public ICardSimple[] FilterCards(IEnumerable<ICardSimple> cards)
    {
        var filtered = new List<ICardSimple>();
        foreach (var card in cards)
        {
            if (card is CardGroup group && group.Count > 0)
                filtered.Add(group);
            else if (!IsFiltered(card))
                filtered.Add(card);
        }

        if (_onlyLastSet)
            return RemoveSetDuplicates(filtered).ToArray();

        return filtered.ToArray();
    }

    private IEnumerable<IMagicCard> RemoveSetDuplicates(IEnumerable<ICardSimple> cards)
    {
        var unique = new Dictionary<Guid, IMagicCard>();
        foreach (var card in cards.OfType<IMagicCard>())
        {
            if (card.Id != Guid.Empty && !unique.ContainsKey(card.Id))
                unique[card.Id] = card;
        }
        return unique.Values;
    }

    public object Clone() => MemberwiseClone();

    // Sorting/grouping stubs — will implement in detail when needed
    public void SetSortField(ICardField field, bool ascending) => _sortOrder.SetSortField(field, ascending);
    public SortOrder GetSortOrder() => _sortOrder;
    public ICardField? GroupField => _groupOrder.Top;
    public bool IsGrouped => _groupOrder.IsGrouped;
    public void SetNameGrouping(bool nameGrouping) => _nameGrouping = nameGrouping;
    public bool IsNameGrouping => _nameGrouping;
}

public class SortOrder
{
    private ICardField? _field;
    private bool _ascending = true;

    public void SetSortField(ICardField field, bool ascending) { _field = field; _ascending = ascending; }
    public void Clear() => _field = null;
    public override bool Equals(object? obj) => obj is SortOrder o && _field == o._field && _ascending == o._ascending;
    public override int GetHashCode() => HashCode.Combine(_field, _ascending);
}

public class GroupOrder
{
    private readonly ICardField[] _fields;
    public GroupOrder() : this(Array.Empty<ICardField>()) { }
    public GroupOrder(params ICardField[] fields) => _fields = fields;
    public bool IsGrouped => _fields.Length > 0;
    public ICardField? Top => _fields.FirstOrDefault();
    public override bool Equals(object? obj) => obj is GroupOrder o && _fields.SequenceEqual(o._fields);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var f in _fields) hash.Add(f);
        return hash.ToHashCode();
    }
}
