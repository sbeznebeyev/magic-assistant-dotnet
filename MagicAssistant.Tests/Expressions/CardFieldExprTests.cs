using MagicAssistant.Core.Expressions;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.Expressions;

[TestFixture]
public class CardFieldExprTests
{
    [Test]
    public void ICardField_Get_ReturnsFieldValue()
    {
        var field = (ICardField)new CardFieldExpr(MagicCardField.Name);
        Assert.That(field.Name, Is.EqualTo("Name"));
        Assert.That(field.Get(new Card { Name = "Lightning Bolt" }), Is.EqualTo("Lightning Bolt"));
        Assert.That(field.Get(new Card()), Is.EqualTo(string.Empty)); // default empty name
    }

    [Test]
    public void ICardField_Set_UpdatesCard()
    {
        var field = (ICardField)new CardFieldExpr(MagicCardField.Name);
        var card = new Card();
        field.Set(card, "New Name");
        Assert.That(card.Name, Is.EqualTo("New Name"));

        // Works on physical too
        var phys = new MyCard(new Card());
        ((ICardField)new CardFieldExpr(MagicCardField.Comment)).Set(phys, "comment");
        Assert.That(phys.Comment, Is.EqualTo("comment"));
    }

    [Test]
    public void GetFieldValue_ExtractsFromCard()
    {
        var expr = new CardFieldExpr(MagicCardField.Name);
        Assert.That(expr.GetFieldValue(new Card { Name = "Test" }), Is.EqualTo("Test"));
    }

    [Test]
    public void ToString_Equals_GetHashCode_Work()
    {
        var e1 = new CardFieldExpr(MagicCardField.Name);
        var e2 = new CardFieldExpr(MagicCardField.Name);
        var e3 = new CardFieldExpr(MagicCardField.Set);

        Assert.That(e1.ToString(), Is.EqualTo("fName"));
        Assert.That(e1.Equals(e2), Is.True);
        Assert.That(e1.Equals(e3), Is.False);
        Assert.That(e1.GetHashCode(), Is.EqualTo(e2.GetHashCode()));
    }
}
