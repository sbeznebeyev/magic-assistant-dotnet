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
        Assert.That(physical.Location.Id, Is.Empty);
        Assert.That(physical.DateAdded, Is.Not.EqualTo(default(DateTimeOffset)));
    }

    [Test]
    public void Constructor_ThrowsForNullCard()
    {
        Assert.Throws<ArgumentNullException>(() => new MagicCardPhysical(null!));
    }

    [Test]
    public void Constructor_WithLocation_SetsValues()
    {
        var physical = new MagicCardPhysical(Card(), Location.FromPath("deck-123"), false);
        Assert.That(physical.Location.Id, Is.EqualTo("deck-123"));
        Assert.That(physical.IsOwn, Is.False);
    }

    /* ─── Ownership & counting ────────────────────────────── */

    [Test]
    public void OwnCount_ReturnsCount_WhenOwned()
    {
        var physical = new MagicCardPhysical(Card(), Location.FromPath("deck"), true) { Count = 3 };
        Assert.That(physical.OwnCount, Is.EqualTo(3));
    }

    [Test]
    public void OwnCount_ReturnsZero_WhenBorrowed()
    {
        var physical = new MagicCardPhysical(Card(), Location.FromPath("deck"), false) { Count = 3 };
        Assert.That(physical.OwnCount, Is.EqualTo(0));
    }

    [Test]
    public void OwnUnique_ReturnsOneOrZero_BasedOnOwnership()
    {
        Assert.That(new MagicCardPhysical(Card(), Location.FromPath("d"), true).OwnUnique, Is.EqualTo(1));
        Assert.That(new MagicCardPhysical(Card(), Location.FromPath("d"), false).OwnUnique, Is.EqualTo(0));
    }

    [Test]
    public void Count4_CapsAtFour_ForOwnedCards()
    {
        Assert.That(new MagicCardPhysical(Card(), Location.FromPath("d"), true) { Count = 2 }.Count4, Is.EqualTo(2));
        Assert.That(new MagicCardPhysical(Card(), Location.FromPath("d"), true) { Count = 5 }.Count4, Is.EqualTo(4));
        Assert.That(new MagicCardPhysical(Card(), Location.FromPath("d"), false) { Count = 3 }.Count4, Is.EqualTo(0));
    }

    /* ─── Sideboard detection ─────────────────────────────── */

    [Test]
    public void IsSideboard_DetectsSbSuffix()
    {
        Assert.That(new MagicCardPhysical(Card(), Location.FromPath("deck-1_sb"), true).IsSideboard, Is.True);
        Assert.That(new MagicCardPhysical(Card(), Location.FromPath("deck-1"), true).IsSideboard, Is.False);
    }

    /* ─── Special tags ────────────────────────────────────── */

    [Test]
    public void SpecialTags_AddRemoveAndCheck()
    {
        var physical = new MagicCardPhysical(Card());
        physical.SetSpecialTag("foil");
        Assert.That(physical.HasSpecialTag("foil"), Is.True);
        Assert.That(physical.HasSpecialTag("premium"), Is.False);

        physical.SetSpecialTag("foil"); // duplicate — should not add again
        Assert.That(physical.SpecialTags, Is.EqualTo("foil"));

        physical.SetSpecialTag("premium");
        Assert.That(physical.HasSpecialTag("premium"), Is.True);

        physical.RemoveSpecialTag("foil");
        Assert.That(physical.HasSpecialTag("foil"), Is.False);
        Assert.That(physical.HasSpecialTag("premium"), Is.True);
    }

    /* ─── ForTrade logic ──────────────────────────────────── */

    [Test]
    public void ForTradeCount_DefaultIsZero()
    {
        Assert.That(new MagicCardPhysical(Card()).GetForTradeCount(), Is.EqualTo(0));
    }

    [Test]
    public void ForTradeCount_ReturnsTotal_WhenTagSet()
    {
        var physical = new MagicCardPhysical(Card()) { Count = 4 };
        physical.SetSpecialTag("fortrade");
        Assert.That(physical.GetForTradeCount(), Is.EqualTo(4));
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

    /* ─── TradeSplit — разделение стопки для трейда ───────── */

    [Test]
    public void TradeSplit_ReturnsNull_WhenNoSplitNeeded()
    {
        var physical = new MagicCardPhysical(Card()) { Count = 4 };
        Assert.That(physical.TradeSplit(4, 4), Is.Null);   // fcount == total → entire pile for trade
        Assert.That(physical.TradeSplit(4, 0), Is.Null);   // fcount == 0 → nothing to trade
    }

    [Test]
    public void TradeSplit_WhenFcountExceedsTotal_ShouldNotMutateState()
    {
        // BUG: TradeSplit(2, 5) where fcount > total silently mutates state
        // (sets Count=5 and adds "fortrade" tag) while returning null.
        // Expected: when fcount > count, the method should either reject the input
        // or leave the original object unchanged. Returning null with side-effects is incorrect.
        var physical = new MagicCardPhysical(Card()) { Count = 2 };
        var result = physical.TradeSplit(2, 5);

        Assert.That(result, Is.Null,
            "TradeSplit should return null when fcount > total (no split possible)");
        Assert.That(physical.Count, Is.EqualTo(2),
            "BUG: TradeSplit must not mutate Count when fcount exceeds total. " +
            "Expected Count to remain 2, but implementation sets it to fcount (5). " +
            "This silently corrupts the card state on invalid input.");
        Assert.That(physical.HasSpecialTag("fortrade"), Is.False,
            "BUG: TradeSplit must not add 'fortrade' tag when fcount exceeds total. " +
            "The original card should remain untouched on invalid input.");
    }

    [Test]
    public void TradeSplit_SplitsCardsCorrectly()
    {
        var physical = new MagicCardPhysical(Card()) { Count = 4, Price = 5.0f, Comment = "test" };
        // fcount(2) < count(4) → actual split occurs
        var tradeCopy = physical.TradeSplit(4, 2);

        Assert.That(tradeCopy, Is.Not.Null);
        Assert.That(physical.Count, Is.EqualTo(2));
        Assert.That(tradeCopy!.Count, Is.EqualTo(2));
        Assert.That(tradeCopy.HasSpecialTag("fortrade"), Is.True);
        Assert.That(physical.HasSpecialTag("fortrade"), Is.False); // original cleared
        Assert.That(tradeCopy.Price, Is.EqualTo(5.0f));
        Assert.That(tradeCopy.Comment, Is.EqualTo("test"));
    }

    /* ─── Matching — сравнение физических карт ────────────── */

    [Test]
    public void Matching_ReturnsTrue_ForIdenticalCards()
    {
        var p1 = new MagicCardPhysical(Card()) { Count = 4, IsOwn = true };
        var p2 = new MagicCardPhysical(Card()) { Count = 4, IsOwn = true };
        Assert.That(p1.Matching(p2), Is.True);
    }

    [Test]
    public void Matching_ReturnsFalse_ForDifferentProperties()
    {
        var base1 = CardWithId("00000000-0000-0000-0000-000000000001");
        var base2 = CardWithId("00000000-0000-0000-0000-000000000002");

        // Different card IDs
        Assert.That(new MagicCardPhysical(base1) { Count = 4 }.Matching(
            new MagicCardPhysical(base2) { Count = 4 }), Is.False);

        // Different ownership
        Assert.That(new MagicCardPhysical(Card()) { Count = 4, IsOwn = true }.Matching(
            new MagicCardPhysical(Card()) { Count = 4, IsOwn = false }), Is.False);

        // Different price
        Assert.That(new MagicCardPhysical(Card()) { Price = 1f }.Matching(
            new MagicCardPhysical(Card()) { Price = 2f }), Is.False);

        // Different comment
        Assert.That(new MagicCardPhysical(Card()) { Comment = "a" }.Matching(
            new MagicCardPhysical(Card()) { Comment = "b" }), Is.False);
    }

    /* ─── CreatureCount ───────────────────────────────────── */

    [Test]
    public void GetCreatureCount_ReturnsCount_ForCreatures()
    {
        var card = Card();
        card.Type = "Creature — Human";
        card.Power = "2";
        Assert.That(new MagicCardPhysical(card) { Count = 3 }.GetCreatureCount(), Is.EqualTo(3));
    }

    [Test]
    public void GetCreatureCount_ReturnsZero_WhenNoPower()
    {
        // Cards without Power/Toughness are not creatures (lands, instants, sorceries, etc.)
        var card = Card();
        card.Type = "Creature — Human";
        card.Power = null;
        Assert.That(new MagicCardPhysical(card) { Count = 3 }.GetCreatureCount(), Is.EqualTo(0));
    }

    [Test]
    public void GetCreatureCount_ReturnsZero_ForNonCreatureType()
    {
        // Non-creature types should return 0 even if Type string is set
        var card = Card();
        card.Type = "Instant";
        Assert.That(new MagicCardPhysical(card) { Count = 4 }.GetCreatureCount(), Is.EqualTo(0));
    }

    [Test]
    public void GetCreatureCount_ShouldCheckTypeContainsCreature()
    {
        // BUG: GetCreatureCount only checks Power != null, ignoring Type entirely.
        // A Planeswalker/Artifact with Power should NOT be counted as a creature.
        var card = Card();
        card.Type = "Planeswalker";
        card.Power = "5";
        Assert.That(new MagicCardPhysical(card) { Count = 1 }.GetCreatureCount(), Is.EqualTo(0),
            "BUG: GetCreatureCount must check that Type contains 'Creature'. " +
            "A Planeswalker with Power='5' is NOT a creature and should return 0. " +
            "Current implementation only checks Power != null, ignoring Type completely.");
    }

    /* ─── Clone & property delegation ────────────────────── */

    [Test]
    public void Clone_CreatesDeepCopy()
    {
        var physical = new MagicCardPhysical(Card()) { Count = 3, Comment = "test" };
        var clone = (MagicCardPhysical)physical.Clone();
        Assert.That(clone.Count, Is.EqualTo(3));
        Assert.That(clone.Comment, Is.EqualTo("test"));
        Assert.That(clone, Is.Not.SameAs(physical));
    }

    [Test]
    public void Error_Field_StoresValue()
    {
        // Error is a native field, not in _properties anymore.
        var physical = new MagicCardPhysical(Card());
        physical.SetError("test error");
        Assert.That(physical.GetError(), Is.EqualTo("test error"));

        // Non-set error returns null
        var physical2 = new MagicCardPhysical(Card());
        Assert.That(physical2.GetError(), Is.Null);
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

    /* ─── Error handling ──────────────────────────────────── */

    [Test]
    public void SetError_And_GetError_StoreValue()
    {
        var physical = new MagicCardPhysical(Card());
        physical.SetError("import error");
        Assert.That(physical.GetError(), Is.EqualTo("import error"));
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
