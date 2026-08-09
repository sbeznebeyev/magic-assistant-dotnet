using MagicAssistant.Core.Expressions;

namespace MagicAssistant.Core.Expressions;

public class NotExpr : Expr
{
    private readonly Expr _left;

    public NotExpr(Expr left)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
    }

    public Expr Left => _left;

    public override bool Evaluate(object? o) => !_left.Evaluate(o);

    public override string ToString() => $"NOT({_left})";

    public override bool Equals(Expr? other)
    {
        return other is NotExpr notOther && _left.Equals(notOther._left);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine("Not", _left);
    }
}
