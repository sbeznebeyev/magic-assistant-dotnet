using MagicAssistant.Core;
using MagicAssistant.Core.Expressions;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.Expressions;

[TestFixture]
public class MagicCardFilterTests
{
    [Test]
    public void Constructor_DefaultsToTrueRoot()
    {
        var filter = new MagicCardFilter();
        Assert.That(filter.Root, Is.SameAs(Expr.True));
    }

    [Test]
    public void SetFilter_Null_UsesTrue()
    {
        var filter = new MagicCardFilter();
        filter.SetFilter(null!);
        Assert.That(filter.Root, Is.SameAs(Expr.True));
    }

    [Test]
    public void FilterCards_NameMatch_ReturnsMatchingCards()
    {
        var filter = new MagicCardFilter();
        filter.SetFilter(BinaryExpr.FieldMatches(MagicCardField.Name, "lightning"));

        var cards = new ICard[]
        {
            new MagicCard { Name = "Lightning Bolt" },
            new MagicCard { Name = "Fireball" },
        };

        Assert.That(filter.FilterCards(cards), Has.Length.EqualTo(1));
    }

    [Test]
    public void FilterCards_SetMatch_ReturnsMatchingCards()
    {
        var filter = new MagicCardFilter();
        filter.SetFilter(BinaryExpr.FieldEquals(MagicCardField.Set, "m19"));

        var cards = new ICard[]
        {
            new MagicCard { Name = "Card 1", SetCode = "m19" },
            new MagicCard { Name = "Card 2", SetCode = "m20" },
        };

        Assert.That(filter.FilterCards(cards), Has.Length.EqualTo(1));
    }

    [Test]
    public void Update_CreatesFilterFromMap()
    {
        var filter = new MagicCardFilter();
        filter.Update(new Dictionary<string, string> { ["type"] = "Creature" });
        Assert.That(filter.Root, Is.Not.SameAs(Expr.True));
    }

    [Test]
    public void Update_EmptyMap_ResetsFilterToTrue()
    {
        // Update(Map) always rebuilds the filter from scratch (mirrors Java CardFilter.update()).
        // With an empty map: expr=True, textOr=Empty, textNot=True
        //   → True.And(Empty).And(True) = Empty.And(True) = True
        // This is intentional: Update() replaces the filter, it doesn't merge.
        var filter = new MagicCardFilter();
        filter.SetFilter(BinaryExpr.FieldEquals(MagicCardField.Name, "original"));
        Assert.That(filter.Root, Is.Not.SameAs(Expr.True));

        filter.Update(new Dictionary<string, string>());
        Assert.That(filter.Root, Is.SameAs(Expr.True));
    }

    [Test]
    public void GroupOrder_WorksWithFields()
    {
        var order = new GroupOrder();
        Assert.That(order.IsGrouped, Is.False);

        order = new GroupOrder(new CardFieldExpr(MagicCardField.Name), new CardFieldExpr(MagicCardField.Set));
        Assert.That(order.IsGrouped, Is.True);
        Assert.That(order.Top, Is.InstanceOf<CardFieldExpr>());
    }
}
