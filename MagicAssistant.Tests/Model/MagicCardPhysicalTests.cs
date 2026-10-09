using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Tests.Model;

[TestFixture]
public class MagicCardPhysicalTests
{
    private static MagicCard Card(Guid id = default) {
        if (id == Guid.Empty) id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        return new() { Id = id, Name = "Test Card" };
    }

    private static MagicCard CardWithId(string hex) => new() { Id = Guid.Parse(hex), Name = "Test Card" };

    /* ─── Constructor & defaults ──────────────────────────── */

    [Test]
    public void Constructor_SetsDefaults()
    {
        var physical = new MagicCardPhysical(Card());
        Assert.That(physical.Count, Is.EqualTo(1));
        Assert.That(physical.IsOwn, Is.False, "IsOwn defaults to false (bool default); only the 3-param constructor sets it explicitly");
        Assert.That(physical.Collection, Is.Null);
        Assert.That(physical.DateAdded, Is.Not.EqualTo(default(DateTimeOffset)));
    }

    [Test]
    public void Constructor_ThrowsForNullCard()
    {
        Assert.Throws<ArgumentNullException>(() => new MagicCardPhysical(null!));
    }

    [Test]
    public void Constructor_WithCollection_SetsValues()
    {
        var physical = new MagicCardPhysical(Card(), new Collection(123, "deck-123", null, NodeType.Deck, 0), false);
        Assert.That(physical.Collection?.Id, Is.EqualTo(123));
        Assert.That(physical.IsOwn, Is.False);
    }

    /* ─── Ownership & counting ────────────────────────────── */

    [Test]
    public void OwnCount_ReturnsCount_WhenOwned()
    {
        var physical = new MagicCardPhysical(Card(), new Collection(1, string.Empty, null, NodeType.Deck, 0), true) { Count = 3 };
        Assert.That(physical.OwnCount, Is.EqualTo(3));
    }

    [Test]
    public void OwnCount_ReturnsZero_WhenBorrowed()
    {
        var physical = new MagicCardPhysical(Card(), new Collection(1, string.Empty, null, NodeType.Deck, 0), false) { Count = 3 };
        Assert.That(physical.OwnCount, Is.EqualTo(0));
    }


    /* ─── Sideboard flag ──────────────────────────────────── */

    [Test]
    public void IsSideboard_ReflectsCollectionNodeType()
    {
        Assert.That(new MagicCardPhysical(Card(), new Collection(1, "sideboard", null, NodeType.Sideboard, 0), true).IsSideboard, Is.True);
        Assert.That(new MagicCardPhysical(Card(), new Collection(1, "deck-1", null, NodeType.Deck, 0), true).IsSideboard, Is.False);
    }

    /* ─── ForTrade logic ──────────────────────────────────── */

    [Test]
    public void ForTradeCount_DefaultIsZero()
    {
        Assert.That(new MagicCardPhysical(Card()).GetForTradeCount(), Is.EqualTo(0));
    }

    [Test]
    public void ForTradeCount_StoresExplicitValue()
    {
        var physical = new MagicCardPhysical(Card());
        physical.SetForTradeCount(2);
        Assert.That(physical.GetForTradeCount(), Is.EqualTo(2));

        physical.SetForTradeCount(0);
        Assert.That(physical.GetForTradeCount(), Is.EqualTo(0));
    }




    /* ─── Base card delegation ────────────────────────────── */

    [Test]
    public void SetBaseCard_UpdatesReference()
    {
        var physical = new MagicCardPhysical(CardWithId("00000000-0000-0000-0000-000000000001"));
        physical.SetBaseCard(CardWithId("00000000-0000-0000-0000-000000000002"));
        Assert.That(physical.Id, Is.EqualTo(Guid.Parse("00000000-0000-0000-0000-000000000002")));
    }

    [Test]
    public void GetBase_ReturnsBaseCard()
    {
        var card = Card();
        var physical = new MagicCardPhysical(card);
        Assert.That(physical.GetBase(), Is.SameAs(card));
    }


    /* ─── ToString ────────────────────────────────────────── */

    [Test]
    public void ToString_ContainsCardNameAndCount()
    {
        var physical = new MagicCardPhysical(Card()) { Count = 3 };
        var str = physical.ToString();
        Assert.That(str, Does.Contain("Test Card"));
        Assert.That(str, Does.Contain("3"));
    }
}
