using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.Model;

[TestFixture]
public class LocationTests
{
    [Test]
    public void NoWhere_HasEmptyIdAndNotSideboard()
    {
        Assert.That(Location.NoWhere.Id, Is.Empty);
        Assert.That(Location.NoWhere.IsSideboard, Is.False);
    }

    [Test]
    public void FromPath_ParsesIdAndSideboardSuffix()
    {
        var loc = Location.FromPath("deck-123");
        Assert.That(loc.Id, Is.EqualTo("deck-123"));
        Assert.That(loc.IsSideboard, Is.False);

        // _sb is the sideboard suffix used throughout the codebase:
        // SqliteCollectionStore creates sideboards as deckId + "_sb",
        // DeckCardService calls Location.FromPath(deckId + "_sb"),
        // RemoveCardsFromDeckCommand uses deck.Id + "_sb", etc.
        var sb = Location.FromPath("deck-123_sb");
        Assert.That(sb.IsSideboard, Is.True,
            "Location.FromPath must recognize \"_sb\" as sideboard suffix (used throughout the codebase).");

        // Id preserves the full path including _sb suffix — it's the unique deck identifier in DB.
        // SqliteCollectionStore stores DeckId = "deck-123_sb" for sideboard rows.
        Assert.That(sb.Id, Is.EqualTo("deck-123_sb"),
            "Id must include the full path; _sb suffix is not stripped from Id.");
    }

    [Test]
    public void FromPath_NullAndEmpty_ReturnNoWhereSingleton()
    {
        // Both null and empty must return the exact NoWhere singleton, not a new Location with empty Id.
        Assert.That(Location.FromPath(""), Is.SameAs(Location.NoWhere),
            "BUG: FromPath(\"\") should return the NoWhere singleton instance, not a separate Location object.");
        Assert.That(Location.FromPath(null!), Is.SameAs(Location.NoWhere),
            "BUG: FromPath(null) should return the NoWhere singleton instance.");
    }

    [Test]
    public void Equals_And_GetHashCode_BasedOnIdAndSideboard()
    {
        var l1 = Location.FromPath("deck-1");
        var l2 = Location.FromPath("deck-1");
        var l3 = Location.FromPath("deck-2");

        Assert.That(l1.Equals(l2), Is.True);
        Assert.That(l1.Equals(l3), Is.False);
        Assert.That(l1.GetHashCode(), Is.EqualTo(l2.GetHashCode()));

        // Sideboard vs non-sideboard with same Id must NOT be equal.
        var mainLocation = Location.FromId(1, "deck-1", false);
        var sideLocation = Location.FromId(1, "deck-1", true);
        Assert.That(mainLocation.Equals(sideLocation), Is.False,
            "BUG: Locations with same Id but different IsSideboard must not be equal. " +
            "IsSideboard is part of equality semantics.");

        // NoWhere equals a freshly-constructed Location with id=0 and IsSideboard=false
        var equivalent = Location.FromId(0, string.Empty, false);
        Assert.That(Location.NoWhere.Equals(equivalent), Is.True);
    }
}
