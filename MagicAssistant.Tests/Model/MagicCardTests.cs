namespace MagicAssistant.Tests.Model;

[TestFixture]
public class MagicCardTests
{
    /* ─── IsBasicLand — определение базовых земель по Type ──── */

    [Test]
    public void IsBasicLand_ReturnsTrue_ForBasicLands()
    {
        Assert.That(new MagicCard { Type = "Basic Land — Plains" }.IsBasicLand(), Is.True);
        Assert.That(new MagicCard { Type = "basic land" }.IsBasicLand(), Is.True);
    }

    [Test]
    public void IsBasicLand_ReturnsFalse_ForNonBasic()
    {
        Assert.That(new MagicCard { Type = "Creature — Human" }.IsBasicLand(), Is.False);
        Assert.That(new MagicCard { Type = "Land — Desert" }.IsBasicLand(), Is.False);
        Assert.That(new MagicCard { Type = null }.IsBasicLand(), Is.False);
    }

    /* ─── Clone — независимая копия ───────────────────────── */

    [Test]
    public void Clone_CreatesIndependentDeepCopy()
    {
        var original = new MagicCard { Id = Guid.NewGuid(), Name = "Test" };

        var clone = (MagicCard)original.Clone();
        clone.Name = "Modified";

        Assert.That(original.Name, Is.EqualTo("Test"));
    }


    /* ─── Equals — идентичность эталонных карт ─────────────── */

    [Test]
    public void Equals_PrioritizesCardId_ThenName_ThenSet()
    {
        var cardId = Guid.NewGuid();
        Assert.That(new MagicCard { Id = cardId }.Equals(new MagicCard { Id = cardId }), Is.True);
        Assert.That(new MagicCard { Id = Guid.NewGuid() }.Equals(new MagicCard { Id = Guid.NewGuid() }), Is.False);

        // Empty CardId, same Name = equal
        Assert.That(new MagicCard { Name = "Test" }.Equals(new MagicCard { Name = "Test" }), Is.True);

        // Empty CardId, same Name, different Set = NOT equal
        Assert.That(new MagicCard { Name = "Test", SetCode = "A" }.Equals(new MagicCard { Name = "Test", SetCode = "B" }),
            Is.False, "Same Name but different Set should NOT be equal");

        // Both null names and sets = equal
        Assert.That(new MagicCard().Equals(new MagicCard()), Is.True);
    }

    [Test]
    public void Equals_HandlesNullAndDifferentType()
    {
        var card = new MagicCard();
        Assert.That(card.Equals(null), Is.False);
        Assert.That(card!.Equals("string"), Is.False);
    }

    /* ─── Reference card defaults (MagicCard = эталонная карта) ── */

    [Test]
    public void ReferenceCard_HasCorrectPhysicalDefaults()
    {
        var card = new MagicCard();
        Assert.That(card.Count, Is.EqualTo(1), "Reference card always has Count=1 (single instance)");
        Assert.That(card.IsPhysical, Is.False, "Reference card is NOT physical");
    }
        
}
