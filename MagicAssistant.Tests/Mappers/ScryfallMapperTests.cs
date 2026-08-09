using MagicAssistant.Core;
using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.Legality;
using MagicAssistant.Http.Dtos;

namespace MagicAssistant.Tests.Mappers;

[TestFixture]
public class ScryfallMapperTests
{
    // ─── ToMagicCard (ScryfallCardDto) ─────────────────────

    [Test]
    public void ToMagicCard_SetsAllFields()
    {
        var cardId = Guid.NewGuid();
        var dto = new ScryfallCardDto
        {
            Id = cardId,
            Name = "Lightning Bolt",
            ManaCost = "{R}",
            TypeLine = "Instant",
            OracleText = "Lightning Bolt deals 3 damage to any target.",
            Power = null,
            Toughness = null,
            Set = "m19",
            Rarity = "Common",
            Artist = "Dan Brereton",
            Language = "en",
            CollectorNumber = "100",
            Cmc = "1",
            Legalities = new Dictionary<string, string> { ["standard"] = "legal" },
        };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.Id, Is.EqualTo(cardId));
        Assert.That(card.Name, Is.EqualTo("Lightning Bolt"));
        Assert.That(card.ManaCost, Is.EqualTo("{R}"));
        Assert.That(card.Type, Is.EqualTo("Instant"));
    }

    [Test]
    public void ToMagicCard_SetsLegality()
    {
        var dto = new ScryfallCardDto
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Legalities = new Dictionary<string, string> { ["standard"] = "legal", ["modern"] = "legal" },
        };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.LegalityMap.IsLegal(Format.Standard), Is.True);
        Assert.That(card.LegalityMap.IsLegal(Format.Modern), Is.True);
    }

    [Test]
    public void ToMagicCard_IlegalFormat_Excluded()
    {
        var dto = new ScryfallCardDto
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Legalities = new Dictionary<string, string> { ["standard"] = "illegal" },
        };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.LegalityMap.IsLegal(Format.Standard), Is.False);
    }

    [Test]
    public void ToMagicCard_NullLegalities_EmptyMap()
    {
        var dto = new ScryfallCardDto { Id = Guid.NewGuid(), Name = "Test" };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.LegalityMap.IsEmpty, Is.True);
    }

    [Test]
    public void ToMagicCard_SetsImageUrl()
    {
        var dto = new ScryfallCardDto
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            ImageUris = new ImageUrisDto("small", "normal", "art-corp", "png") { Normal = "https://example.com/card.png" },
        };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.ImageUris?.Normal, Is.EqualTo("https://example.com/card.png"));
    }

    [Test]
    public void ToMagicCard_SetsSide()
    {
        var dto = new ScryfallCardDto
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Side = 1,
        };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.Side, Is.EqualTo(1));
    }

    [Test]
    public void ToMagicCard_NullSide_DefaultsToZero()
    {
        var dto = new ScryfallCardDto { Id = Guid.NewGuid(), Name = "Test" };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.Side, Is.EqualTo(0));
    }

    // ─── ToEdition ─────────────────────────────────────────

    [Test]
    public void ToEdition_SetsAllFields()
    {
        var cardId = Guid.NewGuid();
        var dto = new ScryfallSetDto(
            Id: cardId,
            Code: "m19",
            Name: "Magic 2019",
            Uri: "https://api.scryfall.com/sets/m19",
            ScryfallUri: "https://scryfall.com/sets/m19",
            SearchUri: "https://api.scryfall.com/cards/search?q=e%3Am19",
            ReleasedAt: "2018-07-13",
            SetType: "core",
            CardCount: 269,
            ParentSetCode: null,
            Digital: false,
            NonfoilOnly: false,
            FoilOnly: false,
            IconSvgUri: "https://svgs.scryfall.io/sets/m19.svg");

        var edition = ScryfallMapper.ToEdition(dto);

        Assert.That(edition.SetCode, Is.EqualTo("m19"));
        Assert.That(edition.Name, Is.EqualTo("Magic 2019"));
        Assert.That(edition.SetType, Is.EqualTo("core"));
        Assert.That(edition.CardCount, Is.EqualTo(269));
        Assert.That(edition.Id, Is.EqualTo(cardId));
    }

    [Test]
    public void ToEdition_SetsReleaseDate()
    {
        var cardId = Guid.NewGuid();
        var dto = new ScryfallSetDto(
            Id: cardId,
            Code: "m19",
            Name: "Magic 2019",
            Uri: "https://api.scryfall.com/sets/m19",
            ScryfallUri: "https://scryfall.com/sets/m19",
            SearchUri: "https://api.scryfall.com/cards/search?q=e%3Am19",
            ReleasedAt: "2018-07-13",
            SetType: "core",
            CardCount: 269,
            ParentSetCode: null,
            Digital: false,
            NonfoilOnly: false,
            FoilOnly: false,
            IconSvgUri: "https://svgs.scryfall.io/sets/m19.svg");

        var edition = ScryfallMapper.ToEdition(dto);

        // BUG in ScryfallMapper.ToEdition:
        // dto.ReleasedAt is parsed via DateTime.Parse into a local variable 'date',
        // but the parsed value is never assigned to edition.ReleaseDate.
        // The Edition is returned with ReleaseDate = null despite valid input.
        // Fix: add 'edition.ReleaseDate = date;' before 'return new Edition(...)'.
        Assert.That(edition.ReleaseDate, Is.EqualTo(new DateTime(2018, 7, 13)),
            "BUG in ScryfallMapper.ToEdition: ReleasedAt parsed but not assigned to Edition.ReleaseDate. " +
            "The local variable 'date' is discarded after parsing.");
    }

    // ─── ParseCollectorNumberId ────────────────────────────

    [Test]
    public void ToMagicCard_ParsesCollectorNumberNumeric()
    {
        var dto = new ScryfallCardDto { Id = Guid.NewGuid(), Name = "Test", CollectorNumber = "42" };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.CollectorNumberId, Is.EqualTo(42));
    }

    [Test]
    public void ToMagicCard_ParsesCollectorNumberWithSide()
    {
        var dto = new ScryfallCardDto { Id = Guid.NewGuid(), Name = "Test", CollectorNumber = "42a" };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.CollectorNumberId, Is.EqualTo(42));
    }

    [Test]
    public void ToMagicCard_ParsesCollectorNumberSlash()
    {
        var dto = new ScryfallCardDto { Id = Guid.NewGuid(), Name = "Test", CollectorNumber = "1/234" };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.CollectorNumberId, Is.EqualTo(1));
    }

    [Test]
    public void ToMagicCard_ParsesCollectorNumberLetterPrefix()
    {
        var dto = new ScryfallCardDto { Id = Guid.NewGuid(), Name = "Test", CollectorNumber = "A5" };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.CollectorNumberId, Is.EqualTo(5));
    }

    [Test]
    public void ToMagicCard_NullCollectorNumber_ReturnsZero()
    {
        var dto = new ScryfallCardDto { Id = Guid.NewGuid(), Name = "Test" };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.CollectorNumberId, Is.EqualTo(0));
    }

    // ─── All Formats ──────────────────────────────────────

    [Test]
    public void ToMagicCard_AllLegalFormats_Parsed()
    {
        var legalities = new Dictionary<string, string>
        {
            ["standard"] = "legal",
            ["pioneer"] = "legal",
            ["modern"] = "legal",
            ["legacy"] = "legal",
            ["vintage"] = "legal",
            ["commander"] = "legal",
            ["brawl"] = "legal",
            ["historic"] = "legal",
            ["timeless"] = "legal",
            ["duelist"] = "legal",
            ["pauper"] = "legal",
        };

        var dto = new ScryfallCardDto { Id = Guid.NewGuid(), Name = "Test", Legalities = legalities };

        var card = ScryfallMapper.ToMagicCard(dto);

        Assert.That(card.LegalityMap.IsLegal(Format.Standard), Is.True);
        Assert.That(card.LegalityMap.IsLegal(Format.Pioneer), Is.True);
        Assert.That(card.LegalityMap.IsLegal(Format.Modern), Is.True);
        Assert.That(card.LegalityMap.IsLegal(Format.Legacy), Is.True);
        Assert.That(card.LegalityMap.IsLegal(Format.Vintage), Is.True);
        Assert.That(card.LegalityMap.IsLegal(Format.Commander), Is.True);
    }
}
