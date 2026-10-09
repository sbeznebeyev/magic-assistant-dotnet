using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.Model;

[TestFixture]
public class LocationTests
{
    [Test]
    public void NoWhere_HasZeroIdAndNotSideboard()
    {
        Assert.That(Location.NoWhere.Id, Is.EqualTo(0));
        Assert.That(Location.NoWhere.IsSideboard, Is.False);
    }

    [Test]
    public void FromId_SetsIdNameAndSideboard()
    {
        var loc = Location.FromId(123, "deck-123");
        Assert.That(loc.Id, Is.EqualTo(123));
        Assert.That(loc.Name, Is.EqualTo("deck-123"));
        Assert.That(loc.IsSideboard, Is.False);

        var sb = Location.FromId(124, "sideboard", true);
        Assert.That(sb.Id, Is.EqualTo(124));
        Assert.That(sb.IsSideboard, Is.True);
    }

    [Test]
    public void Equals_BasedOnId()
    {
        var l1 = Location.FromId(1, "deck-1");
        var l2 = Location.FromId(1, "deck-1");
        var l3 = Location.FromId(2, "deck-2");

        Assert.That(l1.Equals(l2), Is.True);
        Assert.That(l1.Equals(l3), Is.False);

        // Equality is by int Id only. Sideboard status is implied by which node the Id points
        // at (sideboards are separate nodes with their own Ids), so it's not part of equality —
        // same Id means equal regardless of the IsSideboard flag.
        var mainLocation = Location.FromId(1, "deck-1", false);
        var sideLocation = Location.FromId(1, "deck-1", true);
        Assert.That(mainLocation.Equals(sideLocation), Is.True,
            "Locations with the same int Id are equal; IsSideboard is not part of equality.");

        // NoWhere equals a freshly-constructed Location with id=0 and IsSideboard=false
        var equivalent = Location.FromId(0, string.Empty, false);
        Assert.That(Location.NoWhere.Equals(equivalent), Is.True);
    }
}
