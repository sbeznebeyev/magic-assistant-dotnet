using MagicAssistant.Core.Expressions;

namespace MagicAssistant.Tests.Expressions;

[TestFixture]
public class NotExprTests
{
    [Test]
    public void Constructor_ThrowsForNull()
    {
        Assert.Throws<ArgumentNullException>(() => new NotExpr(null!));
    }

    [Test]
    public void Evaluate_InvertsResult()
    {
        Assert.That(new NotExpr(Expr.True).Evaluate(null), Is.False);
        Assert.That(new NotExpr(Expr.False).Evaluate(null), Is.True);
        // Empty evaluates to true, so NOT(Empty) = false
        Assert.That(new NotExpr(Expr.Empty).Evaluate(null), Is.False);
    }

    [Test]
    public void Nested_Not_DoubleNegation()
    {
        var expr = new NotExpr(new NotExpr(Expr.True));
        Assert.That(expr.Evaluate(null), Is.True); // NOT(NOT(True)) = True
    }

    [Test]
    public void ToString_And_Equals_Work()
    {
        var expr = new NotExpr(Expr.True);
        Assert.That(expr.ToString(), Is.EqualTo("NOT(true)"));
        Assert.That(new NotExpr(Expr.False).Equals(new NotExpr(Expr.False)), Is.True);
        Assert.That(new NotExpr(Expr.True).Equals(new NotExpr(Expr.False)), Is.False);
    }
}
