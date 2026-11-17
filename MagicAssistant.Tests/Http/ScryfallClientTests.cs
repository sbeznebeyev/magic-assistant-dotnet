using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MagicAssistant.Http.Dtos;
using MagicAssistant.Http.Scryfall;

namespace MagicAssistant.Tests.Http;

[TestFixture]
public class ScryfallClientTests
{
    private MockHttpMessageHandler _handler = null!;
    private HttpClient _httpClient = null!;
    private ScryfallClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://api.scryfall.com") };
        var logger = new Mock<ILogger<ScryfallClient>>().Object;
        _client = new ScryfallClient(_httpClient, logger);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
        _handler?.Dispose();
    }

    /* ═══════════════════════════════════════════════════════
     * GetSetsAsync — пагинация сетов
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task GetSetsAsync_SinglePage_ReturnsAllSets()
    {
        var sets = new[] { CreateSetDto("m19", "Magic 2019"), CreateSetDto("m20", "Magic 2020") };
        _handler.Responses.Enqueue(CreateSetsResponse(sets, hasMore: false, nextPage: null));

        var result = await _client.GetSetsAsync();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetSetsAsync_MultiplePages_FollowsPagination()
    {
        _handler.Responses.Enqueue(CreateSetsResponse(new[] { CreateSetDto("m19", "Magic 2019") }, hasMore: true, nextPage: "/sets?page=2"));
        _handler.Responses.Enqueue(CreateSetsResponse(new[] { CreateSetDto("m20", "Magic 2020") }, hasMore: false, nextPage: null));

        var result = await _client.GetSetsAsync();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(_handler.Requests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetSetsAsync_EmptyResponse_ReturnsEmptyCollection()
    {
        _handler.Responses.Enqueue(CreateSetsResponse(Array.Empty<ScryfallSetDto>(), hasMore: false, nextPage: null));

        var result = await _client.GetSetsAsync();

        Assert.That(result, Is.Empty);
    }

    /* ═══════════════════════════════════════════════════════
     * GetCardsAsync — пагинация карт сета
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task GetSetCardsAsync_SinglePage_ReturnsAllCards()
    {
        var cards = new[] { CreateCardDto(Guid.NewGuid(), "Lightning Bolt"), CreateCardDto(Guid.NewGuid(), "Mountain") };
        _handler.Responses.Enqueue(CreateSetCardsResponse(cards, hasMore: false, nextPage: null));

        var result = await _client.GetCardsAsync("m19", englishOnly: true);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetSetCardsAsync_EmptySet_ReturnsEmptyCollection()
    {
        _handler.Responses.Enqueue(CreateSetCardsResponse(Array.Empty<ScryfallCardDto>(), hasMore: false, nextPage: null));

        var result = await _client.GetCardsAsync("EMPTY", englishOnly: true);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetSetCardsAsync_EscapesSpecialCharactersInSetAbbreviation()
    {
        // The original test checked for raw "e:M19" which would pass even if
        // escaping was completely broken. Using "+" (space equivalent in query strings)
        // to verify that Uri.EscapeDataString actually encodes dangerous characters.
        _handler.Responses.Enqueue(CreateSetCardsResponse(Array.Empty<ScryfallCardDto>(), hasMore: false, nextPage: null));

        await _client.GetCardsAsync("M+19", englishOnly: true);

        var request = _handler.Requests.First();
        var query = request.RequestUri!.Query;

        // "+" must be encoded as "%2B" (not left as "+" which would be interpreted as space)
        // : must be encoded as "%3A"
        Assert.That(query, Does.Contain("set%3AM%2B19"),
            $"BUG in ScryfallClient.GetCardsAsync: Set abbreviation 'M+19' was not properly URL-encoded. " +
            $"Expected 'set%3AM%2B19' in query but got: '{query}'. " +
            $"If '+' appears unencoded, it will be interpreted as a space by the server, " +
            $"causing incorrect search results or 404. Uri.EscapeDataString may not be applied correctly.");
    }

    /* ═══════════════════════════════════════════════════════
     * HTTP ошибки
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task GetSetsAsync_HttpError_ThrowsHttpRequestException()
    {
        _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.BadGateway));

        await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetSetsAsync());
    }

    // ═══════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════

    private static HttpResponseMessage CreateSetsResponse(ScryfallSetDto[] sets, bool hasMore, string? nextPage)
    {
        var response = new ScryfallSearchResponse<ScryfallSetDto>(
            "search", hasMore, nextPage, sets.Length, sets, null);
        return CreateJsonResponse(response);
    }

    private static HttpResponseMessage CreateSetCardsResponse(ScryfallCardDto[] cards, bool hasMore, string? nextPage)
    {
        var response = new ScryfallSearchResponse<ScryfallCardDto>(
            "search", hasMore, nextPage, cards.Length, cards, null);
        return CreateJsonResponse(response);
    }

    private static HttpResponseMessage CreateJsonResponse(object data)
    {
        var json = JsonSerializer.Serialize(data);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private static ScryfallSetDto CreateSetDto(string code, string name) =>
        new(
            Id: Guid.NewGuid(),
            Code: code,
            Name: name,
            Uri: $"https://api.scryfall.com/sets/{code}",
            ScryfallUri: $"https://scryfall.com/sets/{code}",
            SearchUri: $"https://api.scryfall.com/cards/search?q=e%3A{code}",
            ReleasedAt: "2020-01-01",
            SetType: "core",
            CardCount: 269,
            ParentSetCode: null,
            Digital: false,
            NonfoilOnly: false,
            FoilOnly: false,
            IconSvgUri: $"https://svgs.scryfall.io/sets/{code}.svg");

    private static ScryfallCardDto CreateCardDto(Guid cardId, string name) =>
        new() { Id = cardId, Name = name };
}

// ═══════════════════════════════════════════════════════
//  Mock HttpMessageHandler для тестирования пагинации
// ═══════════════════════════════════════════════════════

public class MockHttpMessageHandler : HttpMessageHandler
{
    public Queue<HttpResponseMessage> Responses { get; } = new();
    public List<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        var response = Responses.Dequeue();
        return Task.FromResult(response);
    }
}
