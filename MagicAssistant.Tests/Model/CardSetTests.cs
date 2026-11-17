using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.SearchableProperties;

namespace MagicAssistant.Tests.Model;

[TestFixture]
public class CardSetTests
{
    #region Construction & Basic Properties

    [Test]
    public void Constructor_SetsBasicProperties()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core);

        Assert.That(edition.SetCode, Is.EqualTo("m19"));
        Assert.That(edition.Name, Is.EqualTo("Magic 2019"));
        Assert.That(edition.SetType, Is.EqualTo(CardSetType.core)); // wire value round-trips as enum
        Assert.That(edition.Id, Is.GreaterThan(Guid.Empty));
    }

    [Test]
    public void Constructor_NullTypeMapsToUnknown()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019");

        Assert.That(edition.SetType, Is.EqualTo(CardSetType.unknown));
    }

    [Test]
    public void Constructor_GeneratesUniqueIds()
    {
        var e1 = new CardSet(Guid.NewGuid(), "A1", "Set A");
        var e2 = new CardSet(Guid.NewGuid(), "A2", "Set B");

        Assert.That(e1.Id, Is.Not.EqualTo(e2.Id));
    }

    [Test]
    public void UnknownEdition_ReturnsSingleton()
    {
        var u1 = CardSet.UnknownEdition;
        var u2 = CardSet.UnknownEdition;

        Assert.That(u1, Is.SameAs(u2));
        Assert.That(u1.SetCode, Is.EqualTo("???"));
        Assert.That(u1.IsUnknown, Is.True);
    }

    #endregion

    #region ReleaseDate — date property

    [Test]
    public void ReleaseDate_CanBeSet()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019");
        edition.ReleaseDate = new DateTime(2018, 9, 28);

        Assert.That(edition.ReleaseDate!.Value.Year, Is.EqualTo(2018));
        Assert.That(edition.ReleaseDate!.Value.Month, Is.EqualTo(9));
        Assert.That(edition.ReleaseDate!.Value.Day, Is.EqualTo(28));
    }

    [Test]
    public void ReleaseDate_DefaultIsNull()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019");

        Assert.That(edition.ReleaseDate, Is.Null);
    }

    #endregion

    #region Equality — based on SetCode (abbreviation)

    [Test]
    public void Equals_BasedOnSetCode()
    {
        var e1 = new CardSet(Guid.NewGuid(), "m19", "Magic 2019");
        var e2 = new CardSet(Guid.NewGuid(), "m19", "Different Name");
        var e3 = new CardSet(Guid.NewGuid(), "m20", "Magic 2020");

        Assert.That(e1.Equals(e2), Is.True);
        Assert.That(e1.Equals(e3), Is.False);
        Assert.That(e1.SetCode, Is.EqualTo(e2.SetCode));
    }

    [Test]
    public void Equals_NullReturnsFalse()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019");

        Assert.That(edition.Equals(null), Is.False);
    }

    [Test]
    public void Equals_SelfReturnsTrue()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019");

        Assert.That(edition.Equals(edition), Is.True);
    }

    [Test]
    public void Equals_DifferentTypeReturnsFalse()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019");

        Assert.That(edition.Equals("m19"), Is.False);
    }

    #endregion

    #region IsUnknown — unknown set detection

    [Test]
    public void IsUnknown_TrueForUnknownEdition()
    {
        Assert.That(CardSet.UnknownEdition.IsUnknown, Is.True);
    }

    [Test]
    public void IsUnknown_FalseForNormalEdition()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019");

        Assert.That(edition.IsUnknown, Is.False);
    }

    #endregion

    #region ToString — string representation

    [Test]
    public void ToString_ReturnsAbbreviationAndName()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019");

        Assert.That(edition.ToString(), Is.EqualTo("m19 - Magic 2019")); // Always lowercase
    }

    #endregion

    #region EditionsInUseRegistry — usage tracking

    [Test]
    public void IsUsedByPrintings_ReturnsTrueWhenRegistered()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019");
        CardSet.EditionsInUseRegistry = new HashSet<string> { "m19" };

        try
        {
            Assert.That(edition.IsUsedByPrintings(), Is.True);
        }
        finally
        {
            CardSet.EditionsInUseRegistry = null;
        }
    }

    [Test]
    public void IsUsedByPrintings_ReturnsFalseWhenNotRegistered()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019");
        CardSet.EditionsInUseRegistry = new HashSet<string> { "m20" };

        try
        {
            Assert.That(edition.IsUsedByPrintings(), Is.False);
        }
        finally
        {
            CardSet.EditionsInUseRegistry = null;
        }
    }

    [Test]
    public void IsUsedByPrintings_FallbackForNullRegistry()
    {
        var edition = new CardSet(Guid.NewGuid(), "m19", "Magic 2019");
        CardSet.EditionsInUseRegistry = null;

        Assert.That(edition.IsUsedByPrintings(), Is.True,
            "Fallback: non-unknown edition should return true when registry is null.");
    }

    [Test]
    public void IsUsedByPrintings_UnknownEditionReturnsFalseWithNullRegistry()
    {
        CardSet.EditionsInUseRegistry = null;

        Assert.That(CardSet.UnknownEdition.IsUsedByPrintings(), Is.False,
            "Fallback: unknown edition should return false when registry is null.");
    }

    #endregion

    #region Editions (SearchableProperty) — filter catalog

    [Test]
    public void Editions_FilterFieldIsSet()
    {
        Assert.That(Editions.Instance.GetFilterField(), Is.EqualTo("Set"));
    }

    [Test]
    public void Editions_ReturnsEmptyAfterFreshReload()
    {
        // Reload with empty to reset singleton state (previous tests may have populated it)
        Editions.Instance.Reload(Array.Empty<CardSet>());

        Assert.That(Editions.Instance.GetIds(), Is.Empty);
    }

    [Test]
    public void Editions_ReloadPopulatesCatalog()
    {
        var editions = new[]
        {
            new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core),
            new CardSet(Guid.NewGuid(), "m20", "Magic 2020", setType: CardSetType.core)
        };

        Editions.Instance.Reload(editions);

        Assert.That(Editions.Instance.GetIds(), Is.EquivalentTo(new[] { "m19", "m20" }));
    }

    [Test]
    public void Editions_GetNameById_ReturnsEditionName()
    {
        var editions = new[] { new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core) };
        Editions.Instance.Reload(editions);

        Assert.That(Editions.Instance.GetNameById("m19"), Is.EqualTo("Magic 2019"));
    }

    [Test]
    public void Editions_GetNameById_ReturnsIdForUnknownAbbreviation()
    {
        var editions = new[] { new CardSet(Guid.NewGuid(), "m19", "Magic 2019", setType: CardSetType.core) };
        Editions.Instance.Reload(editions);

        Assert.That(Editions.Instance.GetNameById("UNKNOWN"), Is.EqualTo("UNKNOWN"));
    }

    #endregion
}
