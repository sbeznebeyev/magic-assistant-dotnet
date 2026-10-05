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
     * GetCardByIdAsync — одиночная карта
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task GetCardByIdAsync_ExistingCard_ReturnsCard()
    {
        var cardId = Guid.NewGuid();
        var card = CreateCardDto(cardId, "Black Lotus");
        _handler.Responses.Enqueue(CreateCardResponse(card));

        var result = await _client.GetCardByIdAsync(cardId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Black Lotus"));
    }

    [Test]
    public async Task GetCardByIdAsync_NotFound_ReturnsNull()
    {
        _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await _client.GetCardByIdAsync(Guid.NewGuid());

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetCardByIdAsync_ServerError_ThrowsException()
    {
        _handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetCardByIdAsync(Guid.NewGuid()));
    }

    /* ═══════════════════════════════════════════════════════
     * SearchAsync — поиск (без пагинации)
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task SearchAsync_ReturnsResults()
    {
        var cards = new[] { CreateCardDto(Guid.NewGuid(), "Lightning Bolt"), CreateCardDto(Guid.NewGuid(), "Lightning Helix") };
        _handler.Responses.Enqueue(CreateSearchResponse(cards, hasMore: false, nextPage: null));

        var result = await _client.SearchAsync("lightning");

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SearchAsync_EmptyResults_ReturnsEmptyArray()
    {
        _handler.Responses.Enqueue(CreateSearchResponse(Array.Empty<ScryfallCardDto>(), hasMore: false, nextPage: null));

        var result = await _client.SearchAsync("nonexistent");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_Limit_CappedAt250()
    {
        _handler.Responses.Enqueue(CreateSearchResponse(Array.Empty<ScryfallCardDto>(), hasMore: false, nextPage: null));

        await _client.SearchAsync("test", limit: 500);

        var request = _handler.Requests.First();
        var query = request.RequestUri!.Query;

        // Verify exact capping: per_page must be exactly 250, not 500
        Assert.That(query, Does.Contain("per_page=250"),
            $"BUG in ScryfallClient.SearchAsync: per_page should be capped at 250 " +
            $"(Scryfall API maximum) when limit exceeds it. Actual query: '{query}'. " +
            $"The capping logic (Math.Min(limit, 250)) may be missing or incorrect.");
        // Also verify the original limit (500) does NOT appear
        Assert.That(query, Does.Not.Contain("per_page=500"),
            $"BUG in ScryfallClient.SearchAsync: Original limit value 500 appears in query " +
            $"instead of capped value 250. Actual query: '{query}'. Math.Min(limit, 250) is not applied correctly.");
    }

    [Test]
    public async Task SearchAsync_DefaultLimit_Uses100()
    {
        _handler.Responses.Enqueue(CreateSearchResponse(Array.Empty<ScryfallCardDto>(), hasMore: false, nextPage: null));

        await _client.SearchAsync("test");

        var request = _handler.Requests.First();
        var query = request.RequestUri!.Query;

        Assert.That(query, Does.Contain("per_page=100"),
            $"BUG in ScryfallClient.SearchAsync: Default per_page should be 100 when no limit is specified. " +
            $"Actual query: '{query}'. The method signature default or the URL construction may be wrong.");
        // Verify it's exactly 100, not a partial match like "per_page=1000"
        Assert.That(query.Split('&').Any(p => p == "per_page=100"),
            $"BUG in ScryfallClient.SearchAsync: per_page parameter value is not exactly 100. " +
            $"The query contains 'per_page=100' as a substring but the actual parameter may differ " +
            $"(e.g., per_page=1000). Actual query: '{query}'. Check the default parameter and URL construction.");
    }

    [Test]
    public async Task SearchAsync_DoesNotPaginate()
    {
        _handler.Responses.Enqueue(CreateSearchResponse(new[] { CreateCardDto(Guid.NewGuid(), "Card 1") }, hasMore: true, nextPage: "/cards/search?page=2"));

        var result = await _client.SearchAsync("test");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(_handler.Requests, Has.Count.EqualTo(1));
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

    private static HttpResponseMessage CreateSearchResponse(ScryfallCardDto[] cards, bool hasMore, string? nextPage)
    {
        var response = new ScryfallSearchResponse<ScryfallCardDto>(
            "search", hasMore, nextPage, cards.Length, cards, null);
        return CreateJsonResponse(response);
    }

    private static HttpResponseMessage CreateCardResponse(ScryfallCardDto card) =>
        CreateJsonResponse(card);

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
