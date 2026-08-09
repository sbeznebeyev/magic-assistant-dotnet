using MagicAssistant.Data.Caches;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Retrieves the list of all available MTG sets from Scryfall.
/// Results are cached for 30 minutes to avoid repeated API calls.
/// </summary>
public record GetKBAvailableSetsQuery;

public class GetKBAvailableSetsQueryHandler
{
    private readonly ILogger _logger;
    private readonly IDbCardStore _dbCardStore;
    private AllCardSetsCache _allCardSetsCache;

    public GetKBAvailableSetsQueryHandler(ILogger<GetKBAvailableSetsQueryHandler> logger, IDbCardStore dbCardStore, AllCardSetsCache allCardSetsCache)
    {
        _logger = logger;
        _dbCardStore = dbCardStore;
        _allCardSetsCache = allCardSetsCache;
    }

    public async Task<CardSet[]> Handle(
        GetKBAvailableSetsQuery query,
        CancellationToken ct = default)
    {
        // Check cache first
        if (_allCardSetsCache.Count > 0) 
            return _allCardSetsCache!.TryGetValue(1, out var cachedSets) ? (CardSet[])cachedSets! : [];

        // Fetch from KB
        var setsResult = await _dbCardStore.GetCardSetsAsync();
        CardSet[] sets = [];

        // Cache the result
        if (setsResult != null && setsResult.Any())
        {
            sets = setsResult.ToArray();
            _allCardSetsCache.Set(1, sets);
        }

        return sets;
    }
}
