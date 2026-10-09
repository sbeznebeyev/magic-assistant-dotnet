using MagicAssistant.Core.Expressions;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.Expressions;

[TestFixture]
public class BinaryExprTests
{
    /* ─── Constructor & null checks ───────────────────────── */

    [Test]
    public void Constructor_ThrowsForNullOperands()
    {
        Assert.Throws<ArgumentNullException>(() => new BinaryExpr(null!, Operation.AND, Expr.True));
        Assert.Throws<ArgumentNullException>(() => new BinaryExpr(Expr.True, Operation.AND, null!));
    }

    /* ─── Evaluate — logical operations ───────────────────── */

    [Test]
    public void Evaluate_And_Or_WorkCorrectly()
    {
        Assert.That(new BinaryExpr(Expr.True, Operation.AND, Expr.True).Evaluate(null), Is.True);
        Assert.That(new BinaryExpr(Expr.False, Operation.AND, Expr.True).Evaluate(null), Is.False);
        Assert.That(new BinaryExpr(Expr.False, Operation.OR, Expr.True).Evaluate(null), Is.True);
        Assert.That(new BinaryExpr(Expr.False, Operation.OR, Expr.False).Evaluate(null), Is.False);
    }

    /* ─── Evaluate — comparison operations ────────────────── */

    [Test]
    public void Evaluate_Equals_SameValues_ReturnsTrue()
    {
        var expr = new BinaryExpr(new CardFieldExpr(MagicCardField.Name), Operation.EQUALS, new Value("test"));
        Assert.That(expr.Evaluate(new Card { Name = "test" }), Is.True);
        Assert.That(expr.Evaluate(new Card { Name = "other" }), Is.False);
    }

    [Test]
    public void Evaluate_Matches_CaseInsensitiveContains()
    {
        var expr = new BinaryExpr(new CardFieldExpr(MagicCardField.Name), Operation.MATCHES,
            new TextValue("lightning", false, false, false));
        Assert.That(expr.Evaluate(new Card { Name = "Lightning Bolt" }), Is.True);
        Assert.That(expr.Evaluate(new Card { Name = "Fireball" }), Is.False);
    }

    [Test]
    public void Evaluate_GE_LE_EQ_CompareNumerically()
    {
        var ge = new BinaryExpr(new CardFieldExpr(MagicCardField.Cmc), Operation.GE, new Value("3"));
        Assert.That(ge.Evaluate(new Card { CmcNumber = 3 }), Is.True);
        Assert.That(ge.Evaluate(new Card { CmcNumber = 4 }), Is.True);
        Assert.That(ge.Evaluate(new Card { CmcNumber = 2 }), Is.False);

        var le = new BinaryExpr(new CardFieldExpr(MagicCardField.Cmc), Operation.LE, new Value("3"));
        Assert.That(le.Evaluate(new Card { CmcNumber = 2 }), Is.True);
        Assert.That(le.Evaluate(new Card { CmcNumber = 4 }), Is.False);

        var eq = new BinaryExpr(new CardFieldExpr(MagicCardField.Cmc), Operation.EQ, new Value("3"));
        Assert.That(eq.Evaluate(new Card { CmcNumber = 3 }), Is.True);
        Assert.That(eq.Evaluate(new Card { CmcNumber = 4 }), Is.False);
    }

    /* ─── Static factories ────────────────────────────────── */

    [Test]
    public void FieldFactories_CreateCorrectOperations()
    {
        // FieldEquals → EQUALS
        var equalsExpr = (BinaryExpr)BinaryExpr.FieldEquals(MagicCardField.Name, "test");
        Assert.That(equalsExpr.Op, Is.EqualTo(Operation.EQUALS));

        // FieldMatches → MATCHES
        var matchesExpr = (BinaryExpr)BinaryExpr.FieldMatches(MagicCardField.Name, "test");
        Assert.That(matchesExpr.Op, Is.EqualTo(Operation.MATCHES));
    }

    [Test]
    public void FieldInt_ParsesOperators()
    {
        Assert.That(((BinaryExpr)BinaryExpr.FieldInt(MagicCardField.Cmc, "==3")).Op, Is.EqualTo(Operation.EQ));
        Assert.That(((BinaryExpr)BinaryExpr.FieldInt(MagicCardField.Cmc, ">=3")).Op, Is.EqualTo(Operation.GE));
        Assert.That(((BinaryExpr)BinaryExpr.FieldInt(MagicCardField.Cmc, "<=3")).Op, Is.EqualTo(Operation.LE));
    }

    [Test]
    public void FieldInt_EdgeCases()
    {
        // Zero or >= 0 returns True (always satisfied)
        Assert.That(BinaryExpr.FieldInt(MagicCardField.Cmc, "0"), Is.SameAs(Expr.True));
        Assert.That(BinaryExpr.FieldInt(MagicCardField.Cmc, ">= 0"), Is.SameAs(Expr.True));

        // Null field returns Empty (via nullable overload)
        Assert.That(BinaryExpr.FieldMatches((MagicCardField?)null, "test"), Is.SameAs(Expr.Empty));
    }

    [Test]
    public void FieldMatches_NullValue_ShouldReturnEmpty()
    {
        // BUG: FieldMatches(MagicCardField field, string value) — non-nullable overload —
        // does NOT check for null value. It creates TextValue(null, ...) which converts
        // null to empty string "", producing an always-false MATCHES expression instead of Expr.Empty.
        // The nullable overload FieldMatches(MagicCardField?, string?) DOES check for null,
        // but C# overload resolution picks the non-nullable version when field is non-null.
        try
        {
            var result = BinaryExpr.FieldMatches(MagicCardField.Name, null);
            Assert.That(result, Is.SameAs(Expr.Empty),
                "FieldMatches with null value should return Expr.Empty, not a MATCHES expression");
        }
        catch (AssertionException ex)
        {
            throw new AssertionException(
                $"BUG: BinaryExpr.FieldMatches(MagicCardField field, string value) does not guard against null value. " +
                $"It creates TextValue(null, ...) which converts null to empty string \"\", " +
                $"producing a MATCHES expression that will never match real data. " +
                $"The nullable overload FieldMatches(MagicCardField?, string?) DOES check for null and returns Expr.Empty, " +
                $"but C# overload resolution prefers the non-nullable overload when field is non-null. " +
                $"Fix: Add null guard to the non-nullable overload, or remove it entirely and keep only the nullable version.",
                ex);
        }
    }

    /* ─── TextSearch ──────────────────────────────────────── */

    [Test]
    public void TextSearch_CreatesMatchesExpr_ForSingleWord()
    {
        var expr = BinaryExpr.TextSearch(MagicCardField.Name, "lightning");
        Assert.That(expr, Is.InstanceOf<BinaryExpr>());
        Assert.That(((BinaryExpr)expr).Op, Is.EqualTo(Operation.MATCHES));
    }

    [Test]
    public void TextSearch_ReturnsEmpty_ForNullOrWhitespace()
    {
        Assert.That(BinaryExpr.TextSearch(null!, "lightning"), Is.SameAs(Expr.Empty));
        Assert.That(BinaryExpr.TextSearch(MagicCardField.Name, ""), Is.SameAs(Expr.Empty));
        Assert.That(BinaryExpr.TextSearch(MagicCardField.Name, "  "), Is.SameAs(Expr.Empty));
    }

    /* ─── Equality & helpers ──────────────────────────────── */

    [Test]
    public void Equals_CompareLeftOpRight_Structurally()
    {
        // Structural equality — different object instances, same content
        var expr1 = new BinaryExpr(
            new CardFieldExpr(MagicCardField.Name),
            Operation.EQUALS,
            new Value("test"));

        var expr2 = new BinaryExpr(
            new CardFieldExpr(MagicCardField.Name),
            Operation.EQUALS,
            new Value("test"));

        Assert.That(expr1.Equals(expr2), Is.True,
            "BinaryExpr should support structural equality (different instances, same content)");

        // Different operation → not equal
        var expr3 = new BinaryExpr(
            new CardFieldExpr(MagicCardField.Name),
            Operation.MATCHES,
            new Value("test"));

        Assert.That(expr1.Equals(expr3), Is.False,
            "BinaryExpr with different operations should not be equal");

        // Different field → not equal
        var expr4 = new BinaryExpr(
            new CardFieldExpr(MagicCardField.Cmc),
            Operation.EQUALS,
            new Value("test"));

        Assert.That(expr1.Equals(expr4), Is.False,
            "BinaryExpr with different fields should not be equal");
    }

    [Test]
    public void GetFieldFromExpr_ExtractsField()
    {
        Assert.That(BinaryExpr.GetFieldFromExpr(new CardFieldExpr(MagicCardField.Name)),
            Is.EqualTo(MagicCardField.Name));
        Assert.That(BinaryExpr.GetFieldFromExpr(Expr.True), Is.Null);
    }
}
