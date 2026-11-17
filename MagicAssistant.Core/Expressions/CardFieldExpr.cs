using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Expressions;

/// <summary>
/// Wraps a MagicCardField enum value as an ICardField for use in expressions.
/// </summary>
public class CardFieldExpr : Expr, ICardField
{
    private readonly MagicCardField _field;

    public CardFieldExpr(MagicCardField field)
    {
        _field = field;
    }

    /// <summary>
    /// The underlying MagicCardField value.
    /// </summary>
    public MagicCardField Field => _field;

    string ICardField.Name => _field.ToString();
    Type ICardField.ValueType => typeof(object);

    object? ICardField.Get(ICardSimple card)
    {
        if (card is not ICard magicCard) return null;
        return _field.Get(magicCard);
    }

    void ICardField.Set(ICardSimple card, object? value)
    {
        if (card is Card mc)
            _field.SetM(mc, value);
        else if (card is MyCard mcp)
            _field.SetM(mcp, value);
    }

    public override string ToString() => $"f{_field}";

    public override object? GetFieldValue(object? o)
    {
        if (o is ICardSimple card)
            return ((ICardField)this).Get(card);
        return null;
    }

    public override bool Equals(Expr? other)
    {
        if (other is CardFieldExpr otherField)
            return _field == otherField._field;
        return false;
    }

    public override int GetHashCode() => _field.GetHashCode();
}
