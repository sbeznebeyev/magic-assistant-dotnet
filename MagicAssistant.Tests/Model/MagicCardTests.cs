namespace MagicAssistant.Tests.Model;

[TestFixture]
public class MagicCardTests
{
    /* ─── IsBasicLand — определение базовых земель по Type ──── */

    [Test]
    public void IsBasicLand_ReturnsTrue_ForBasicLands()
    {
        Assert.That(new Card { Type = "Basic Land — Plains" }.IsBasicLand(), Is.True);
        Assert.That(new Card { Type = "basic land" }.IsBasicLand(), Is.True);
    }

    [Test]
    public void IsBasicLand_ReturnsFalse_ForNonBasic()
    {
        Assert.That(new Card { Type = "Creature — Human" }.IsBasicLand(), Is.False);
        Assert.That(new Card { Type = "Land — Desert" }.IsBasicLand(), Is.False);
        Assert.That(new Card { Type = null }.IsBasicLand(), Is.False);
    }

    /* ─── Clone — независимая копия ───────────────────────── */

    [Test]
    public void Clone_CreatesIndependentDeepCopy()
    {
        var original = new Card { Id = Guid.NewGuid(), Name = "Test" };

        var clone = (Card)original.Clone();
        clone.Name = "Modified";

        Assert.That(original.Name, Is.EqualTo("Test"));
    }


    /* ─── Equals — идентичность эталонных карт ─────────────── */

    [Test]
    public void Equals_PrioritizesCardId_ThenName_ThenSet()
    {
        var cardId = Guid.NewGuid();
        Assert.That(new Card { Id = cardId }.Equals(new Card { Id = cardId }), Is.True);
        Assert.That(new Card { Id = Guid.NewGuid() }.Equals(new Card { Id = Guid.NewGuid() }), Is.False);

        // Empty CardId, same Name = equal
        Assert.That(new Card { Name = "Test" }.Equals(new Card { Name = "Test" }), Is.True);

        // Empty CardId, same Name, different Set = NOT equal
        Assert.That(new Card { Name = "Test", SetCode = "A" }.Equals(new Card { Name = "Test", SetCode = "B" }),
            Is.False, "Same Name but different Set should NOT be equal");

        // Both null names and sets = equal
        Assert.That(new Card().Equals(new Card()), Is.True);
    }

    [Test]
    public void Equals_HandlesNullAndDifferentType()
    {
        var card = new Card();
        Assert.That(card.Equals(null), Is.False);
        Assert.That(card!.Equals("string"), Is.False);
    }

    /* ─── Reference card defaults (MagicCard = эталонная карта) ── */

    [Test]
    public void ReferenceCard_HasCorrectPhysicalDefaults()
    {
        var card = new Card();
        Assert.That(card.Count, Is.EqualTo(1), "Reference card always has Count=1 (single instance)");
        Assert.That(card.IsPhysical, Is.False, "Reference card is NOT physical");
    }
        
}
