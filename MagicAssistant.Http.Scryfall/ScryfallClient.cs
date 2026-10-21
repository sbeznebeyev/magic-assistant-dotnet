using System.Net.Http.Headers;
using System.Text.Json;
using MagicAssistant.Http.Dtos;

using MagicAssistant.Http.Contracts;
using Microsoft.Extensions.Logging;

namespace MagicAssistant.Http.Scryfall;

public class ScryfallClient : IScrayfallClient
{
    private readonly ILogger _logger;
    private const string BaseUrl = "https://api.scryfall.com";
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ScryfallClient(HttpClient httpClient, ILogger<ScryfallClient> logger)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BaseUrl);
        // Scryfall API requires a User-Agent header (per their API terms).
        // Using a browser-like UA, same approach as the original Java implementation.
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MagicAssistantNet/1.0");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
    }

    public async Task<ICollection<ScryfallSetDto>> GetSetsAsync(CancellationToken ct = default)
    {
        var allSets = new List<ScryfallSetDto>();
        var url = "/sets";

        while (url != null)
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"Scryfall API returned {response.StatusCode}: {errorBody}",
                    null, response.StatusCode);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<ScryfallSearchResponse<ScryfallSetDto>>(json, _jsonOptions);

            if (data?.Data != null)
                allSets.AddRange(data.Data);
            else
                break;

            // Scryfall /sets uses has_more flag (not Link header pagination)
            url = data.HasMore == true ? data.NextPage : null;
            ct.ThrowIfCancellationRequested();
            // Rate limit: 1 req/sec
            if (url != null)
                await Task.Delay(1000, ct);
        }

        return allSets;
    }

    public async Task<ICollection<ScryfallCardDto>> GetCardsAsync(string setCode, bool englishOnly, Action<int>? onPageDownloaded = null, CancellationToken ct = default)
    {
        var query = $"set:{setCode}";
        var allCards = new List<ScryfallCardDto>();
        var url = englishOnly ?
            $"/cards/search?q={Uri.EscapeDataString(query)}&unique=prints&order=set&include_multilingual=false" :
            $"/cards/search?q={Uri.EscapeDataString(query)}&unique=prints&order=set&include_multilingual=true";

        while (url != null)
        {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<ScryfallSearchResponse<ScryfallCardDto>>(json, _jsonOptions);

            if (!string.IsNullOrWhiteSpace(data?.Warnings))
            {
                _logger.LogWarning(data!.Warnings);
                break;
            }

            allCards.AddRange(data!.Data);
            onPageDownloaded?.Invoke(allCards.Count);

            // Scryfall /cards/search uses has_more + next_page (not Link header)
            url = data.HasMore == true ? data.NextPage : null;
            ct.ThrowIfCancellationRequested();
            // Rate limit: 1 req/sec
            if (url != null)
                await Task.Delay(1000, ct);
        }

        return allCards;
    }

}
