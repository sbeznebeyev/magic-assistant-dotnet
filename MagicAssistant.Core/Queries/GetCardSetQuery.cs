using MagicAssistant.Data.Caches;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using Microsoft.Extensions.Caching.Memory;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Get a CardSet (edition) by its set code abbreviation (e.g. "M19", "mh2").
/// Used by CardDescView to display full set name instead of abbreviation.
/// </summary>
public record GetCardSetQuery(string SetCode);

public class GetCardSetQueryHandler
{
    private readonly IDbCardStore _dbStore;
    private readonly CardSetsCache _cardSetsCache;

    public GetCardSetQueryHandler(IDbCardStore dbStore, CardSetsCache cardSetsCache)
    {
        _dbStore = dbStore;
        _cardSetsCache = cardSetsCache;
    }

    public async Task<CardSet?> Handle(GetCardSetQuery query, CancellationToken ct = default)
    {
        if(_cardSetsCache.TryGetValue<CardSet>(query.SetCode, out var cardSet))
            return cardSet;

        // Fetch from KB
        cardSet = await _dbStore.GetCardSetAsync(query.SetCode, ct);

        // Cache the result
        if (cardSet != null)
        {
            _cardSetsCache.Set(query.SetCode, cardSet);
        }

        return cardSet;
        
    }
}
