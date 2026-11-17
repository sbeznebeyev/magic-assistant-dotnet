using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.Model;

[TestFixture]
public class CardSetTypeTests
{
    [Test]
    public void ToString_ReturnsWireString()
    {
        Assert.That(CardSetType.from_the_vault.ToString(), Is.EqualTo("from_the_vault"));
        Assert.That(CardSetType.core.ToString(), Is.EqualTo("core"));
    }

    [Test]
    public void FromDb_ParsesKnownValues()
    {
        Assert.That(CardSetTypes.FromDb("core"), Is.EqualTo(CardSetType.core));
        Assert.That(CardSetTypes.FromDb("duel_deck"), Is.EqualTo(CardSetType.duel_deck));
    }

    [Test]
    public void FromDb_IsCaseInsensitive()
    {
        Assert.That(CardSetTypes.FromDb("CORE"), Is.EqualTo(CardSetType.core));
    }

    [Test]
    public void FromDb_UnknownOrNullMapsToSentinel()
    {
        Assert.That(CardSetTypes.FromDb(null), Is.EqualTo(CardSetType.unknown));
        Assert.That(CardSetTypes.FromDb(""), Is.EqualTo(CardSetType.unknown));
        Assert.That(CardSetTypes.FromDb("Core Set"), Is.EqualTo(CardSetType.unknown));
        Assert.That(CardSetTypes.FromDb("Weird Type"), Is.EqualTo(CardSetType.unknown));
        Assert.That(CardSetTypes.FromDb("weird_new_type"), Is.EqualTo(CardSetType.unknown));
    }

    [Test]
    public void Parse_KnownValue()
    {
        Assert.That(CardSetTypes.Parse("expansion"), Is.EqualTo(CardSetType.expansion));
    }

    [Test]
    public void Parse_UnknownValueThrows()
    {
        Assert.Throws<ArgumentException>(() => CardSetTypes.Parse("not_a_real_type"));
    }

    [Test]
    public void Parse_NullThrows()
    {
        Assert.Throws<ArgumentException>(() => CardSetTypes.Parse(null));
    }
}
