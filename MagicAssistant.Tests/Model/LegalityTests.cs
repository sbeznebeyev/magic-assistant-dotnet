using MagicAssistant.Data.Model.Legality;

namespace MagicAssistant.Tests.Model;

[TestFixture]
public class LegalityTests
{
    [Test]
    public void Empty_MapHasNoLegalFormats()
    {
        var map = LegalityMap.Empty;
        Assert.That(map.IsEmpty, Is.True);
        Assert.That(map.LegalFormats, Is.Empty);
    }

    [Test]
    public void SetLegal_AddsAndQueriesFormats()
    {
        var map = new LegalityMap();
        map.SetLegal(Format.Standard);
        map.SetLegal(Format.Modern);

        Assert.That(map.IsLegal(Format.Standard), Is.True);
        Assert.That(map.IsLegal(Format.Modern), Is.True);
        Assert.That(map.IsLegal(Format.Pioneer), Is.False);
        Assert.That(map.IsEmpty, Is.False);
    }

    [Test]
    public void SetLegal_DoesNotDuplicate()
    {
        var map = new LegalityMap();
        map.SetLegal(Format.Standard);
        map.SetLegal(Format.Standard);
        Assert.That(map.LegalFormats.Count(f => f == Format.Standard), Is.EqualTo(1));
    }

    [Test]
    public void AllStandardFormats_Work()
    {
        var map = new LegalityMap();
        foreach (var format in Enum.GetValues<Format>())
        {
            map.SetLegal(format);
            Assert.That(map.IsLegal(format), Is.True, $"Failed for {format}");
        }
    }
}
