using MagicAssistant.Data.IO.json;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.DataIO;

[TestFixture]
public class JsonImportTests
{
    // Native Magic Assistant JSON format (produced by JsonExport):
    // { "format": "magic-assistant/deck", "version": 1, "name": "...", "type": "deck",
    //   "cards": [ { "id", "name", "set", "count", "own"?, "price"?, "comment"?, "custom"?, "special"?, "forTrade"? } ] }

    static string MakeJson(string name, params (Guid id, string name, string set, int count)[] cards)
    {
        var cardEntries = string.Join(",\n", cards.Select(c =>
            $"{{ \"id\": \"{c.id}\", \"name\": \"{c.name}\", \"set\": \"{c.set}\", \"count\": {c.count} }}"));
        return $@"{{
  ""format"": ""magic-assistant/deck"",
  ""version"": 1,
  ""name"": ""{name}"",
  ""type"": ""deck"",
  ""cards"": [
    {cardEntries}
  ]
}}";
    }

    [Test]
    public void ImportFromJson_ParsesValidDeck()
    {
        var cardId = Guid.NewGuid();
        var json = MakeJson("Test Deck", (cardId, "Lightning Bolt", "M19", 4));

        var result = JsonImport.ImportFromJson(json);

        Assert.That(result.Name, Is.EqualTo("Test Deck"));
        Assert.That(result.Type, Is.EqualTo("deck"));
        Assert.That(result.Cards, Has.Count.EqualTo(1));
        Assert.That(result.Cards[0], Is.InstanceOf<MagicCardPhysical>());

        var physical = (MagicCardPhysical)result.Cards[0];
        Assert.That(physical.Card.Name, Is.EqualTo("Lightning Bolt"));
        Assert.That(physical.Card.Id, Is.EqualTo(cardId));
        Assert.That(physical.Card.SetCode, Is.EqualTo("m19"), "set code should be lowercased");
        Assert.That(physical.Count, Is.EqualTo(4));
    }

    [Test]
    public void ImportFromJson_ParsesOptionalFields()
    {
        var cardId = Guid.NewGuid();
        var json = $@"{{
  ""format"": ""magic-assistant/deck"",
  ""name"": ""Test Deck"",
  ""cards"": [
    {{
      ""id"": ""{cardId}"",
      ""name"": ""Card"",
      ""set"": ""m19"",
      ""count"": 2,
      ""own"": false,
      ""price"": 5.0,
      ""comment"": ""My card comment"",
      ""custom"": ""custom-data"",
      ""special"": ""foil"",
      ""forTrade"": 1
    }}
  ]
}}";

        var result = JsonImport.ImportFromJson(json);
        var physical = (MagicCardPhysical)result.Cards[0];

        Assert.That(physical.IsOwn, Is.False);
        Assert.That(physical.MyPrice, Is.EqualTo(5.0f).Within(0.001f));
        Assert.That(physical.Comment, Is.EqualTo("My card comment"));
        Assert.That(physical.Custom, Is.EqualTo("custom-data"));
        Assert.That(physical.SpecialTags, Is.EqualTo("foil"));
        Assert.That(physical.GetForTradeCount(), Is.EqualTo(1));
    }

    [Test]
    public void ImportFromJson_Defaults_WhenOptionalFieldsAbsent()
    {
        var cardId = Guid.NewGuid();
        var json = MakeJson("Deck", (cardId, "Card", "m19", 3));

        var physical = (MagicCardPhysical)JsonImport.ImportFromJson(json).Cards[0];

        Assert.That(physical.IsOwn, Is.True, "own defaults to true");
        Assert.That(physical.MyPrice, Is.EqualTo(0f), "price defaults to 0");
        Assert.That(physical.Comment, Is.Null);
        Assert.That(physical.Custom, Is.Null);
        Assert.That(physical.SpecialTags, Is.Null);
    }

    [Test]
    public void ImportFromJson_IgnoresUnknownFields()
    {
        var cardId = Guid.NewGuid();
        var json = $@"{{
  ""format"": ""magic-assistant/deck"",
  ""version"": 1,
  ""name"": ""Deck"",
  ""someFutureHeaderField"": true,
  ""cards"": [
    {{ ""id"": ""{cardId}"", ""name"": ""Card"", ""set"": ""m19"", ""count"": 1, ""futureCardField"": 42 }}
  ]
}}";

        var result = JsonImport.ImportFromJson(json);

        Assert.That(result.Cards, Has.Count.EqualTo(1), "unknown fields must be ignored, not rejected");
    }

    [Test]
    public void ImportFromJson_ThrowsForMalformedJson()
    {
        Assert.Throws<FormatException>(() => JsonImport.ImportFromJson("{ not valid json"));
    }

    [Test]
    public void ImportFromJson_ThrowsForWrongFormatMarker()
    {
        var json = @"{ ""format"": ""other/app"", ""name"": ""Deck"", ""cards"": [] }";

        Assert.Throws<FormatException>(() => JsonImport.ImportFromJson(json));
    }

    [Test]
    public void ImportFromJson_ThrowsForUnsupportedVersion()
    {
        var json = @"{ ""format"": ""magic-assistant/deck"", ""version"": 99, ""name"": ""Deck"", ""cards"": [] }";

        Assert.Throws<FormatException>(() => JsonImport.ImportFromJson(json));
    }

    [Test]
    public void ImportFromJson_ThrowsWhenCardsArrayMissing()
    {
        var json = @"{ ""format"": ""magic-assistant/deck"", ""name"": ""Deck"" }";

        Assert.Throws<FormatException>(() => JsonImport.ImportFromJson(json));
    }

    [Test]
    public void ImportFromJson_ThrowsWhenIdMissing()
    {
        var json = @"{ ""format"": ""magic-assistant/deck"", ""cards"": [ { ""name"": ""Card"", ""set"": ""m19"", ""count"": 1 } ] }";

        Assert.Throws<FormatException>(() => JsonImport.ImportFromJson(json));
    }

    [Test]
    public void ImportFromJson_ThrowsWhenIdIsNotGuid()
    {
        var json = @"{ ""format"": ""magic-assistant/deck"", ""cards"": [ { ""id"": ""12345"", ""name"": ""Card"", ""set"": ""m19"", ""count"": 1 } ] }";

        Assert.Throws<FormatException>(() => JsonImport.ImportFromJson(json));
    }

    [Test]
    public void ImportFromJson_ThrowsWhenNameMissing()
    {
        var cardId = Guid.NewGuid();
        var json = $@"{{ ""format"": ""magic-assistant/deck"", ""cards"": [ {{ ""id"": ""{cardId}"", ""set"": ""m19"", ""count"": 1 }} ] }}";

        Assert.Throws<FormatException>(() => JsonImport.ImportFromJson(json));
    }

    [Test]
    public void ImportFromJson_ThrowsWhenCountMissing()
    {
        // count is part of the reference core — no default is applied.
        var cardId = Guid.NewGuid();
        var json = $@"{{ ""format"": ""magic-assistant/deck"", ""cards"": [ {{ ""id"": ""{cardId}"", ""name"": ""Card"", ""set"": ""m19"" }} ] }}";

        Assert.Throws<FormatException>(() => JsonImport.ImportFromJson(json));
    }

    [Test]
    public void ImportFromJson_ThrowsWhenCountIsZero()
    {
        var cardId = Guid.NewGuid();
        var json = $@"{{ ""format"": ""magic-assistant/deck"", ""cards"": [ {{ ""id"": ""{cardId}"", ""name"": ""Card"", ""set"": ""m19"", ""count"": 0 }} ] }}";

        Assert.Throws<FormatException>(() => JsonImport.ImportFromJson(json));
    }
}
