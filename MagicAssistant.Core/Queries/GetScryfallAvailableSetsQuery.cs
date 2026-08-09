using MagicAssistant.Http.Contracts;
using MagicAssistant.Http.Dtos;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Retrieves the list of all available MTG sets from Scryfall.
/// Results are cached for 30 minutes to avoid repeated API calls.
/// </summary>
public record GetScryfallAvailableSetsQuery;

public class GetScryfallAvailableSetsQueryHandler
{
    private readonly IScrayfallClient _scryfallClient;
    private ICollection<ScryfallSetDto>? _cachedSets;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;
    private readonly object _lock = new();
    private const int CacheTtlMinutes = 30;

    public GetScryfallAvailableSetsQueryHandler(IScrayfallClient scryfallClient)
    {
        _scryfallClient = scryfallClient;
    }

    public async Task<ICollection<ScryfallSetDto>> Handle(
        GetScryfallAvailableSetsQuery query,
        CancellationToken ct = default)
    {
        // Check cache first
        lock (_lock)
        {
            if (_cachedSets != null && (DateTimeOffset.UtcNow - _cachedAt) < TimeSpan.FromMinutes(CacheTtlMinutes))
                return _cachedSets;
        }

        // Fetch from API
        var sets = await _scryfallClient.GetSetsAsync(ct);

        // Cache the result
        lock (_lock)
        {
            _cachedSets = sets;
            _cachedAt = DateTimeOffset.UtcNow;
        }

        return sets;
    }
}
