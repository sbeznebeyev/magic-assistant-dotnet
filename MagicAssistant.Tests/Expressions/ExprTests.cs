using MagicAssistant.Core.Expressions;

namespace MagicAssistant.Tests.Expressions;

[TestFixture]
public class ExprTests
{
    /* ─── Singleton constants ─────────────────────────────── */

    [Test]
    public void True_AlwaysEvaluatesTrue()
    {
        Assert.That(Expr.True.Evaluate(null), Is.True);
        Assert.That(Expr.True.Evaluate("anything"), Is.True);
    }

    [Test]
    public void False_AlwaysEvaluatesFalse()
    {
        Assert.That(Expr.False.Evaluate(null), Is.False);
    }

    [Test]
    public void Empty_EvaluatesTrue()
    {
        Assert.That(Expr.Empty.Evaluate(null), Is.True);
    }

    /* ─── Short-circuit AND/OR ────────────────────────────── */

    [Test]
    public void True_ShortCircuitsCorrectly()
    {
        // True.And(x) = x, True.Or(x) = True, True.Not() = False
        Assert.That(Expr.True.And(Expr.False), Is.SameAs(Expr.False));
        Assert.That(Expr.True.Or(Expr.False), Is.SameAs(Expr.True));
        Assert.That(Expr.True.Not(), Is.SameAs(Expr.False));
    }

    [Test]
    public void False_ShortCircuitsCorrectly()
    {
        // False.And(x) = False, False.Or(x) = x, False.Not() = True
        Assert.That(Expr.False.And(Expr.True), Is.SameAs(Expr.False));
        Assert.That(Expr.False.Or(Expr.True), Is.SameAs(Expr.True));
        Assert.That(Expr.False.Not(), Is.SameAs(Expr.True));
    }

    [Test]
    public void Empty_ShortCircuitsCorrectly()
    {
        // Empty.And(x) = x, Empty.Or(x) = x, Empty.Not() = Empty
        Assert.That(Expr.Empty.And(Expr.False), Is.SameAs(Expr.False));
        Assert.That(Expr.Empty.Or(Expr.True), Is.SameAs(Expr.True));
        Assert.That(Expr.Empty.Not(), Is.SameAs(Expr.Empty));
    }

    /* ─── Static helpers ──────────────────────────────────── */

    [Test]
    public void ValueOf_ReturnsCorrectSingleton()
    {
        Assert.That(Expr.ValueOf(true), Is.SameAs(Expr.True));
        Assert.That(Expr.ValueOf(false), Is.SameAs(Expr.False));
    }

}
