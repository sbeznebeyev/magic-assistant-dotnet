using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Services.CardSearch;

/// <summary>
/// Vertical slice: card search and filtering operations.
/// Wraps IDbCardStore and ICollectionStore queries into a unified service.
/// </summary>
public class CardSearchService : ICardSearchService
{
    private readonly IDbCardStore _dbStore;
    private readonly ICollectionStore _collectionStore;

    public CardSearchService(
        IDbCardStore dbStore,
        ICollectionStore collectionStore)
    {
        _dbStore = dbStore;
        _collectionStore = collectionStore;
    }

    /// <inheritdoc />
    public async Task<ICollection<IMagicCard>> SearchAsync(
        string query, int limit = 100, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<IMagicCard>();

        var cards = await _dbStore.SearchAsync(query.Trim(), limit, ct);
        return cards.Cast<IMagicCard>().ToList();
    }

    /// <inheritdoc />
    public Task<MagicCard?> GetCardByIdAsync(Guid cardId, CancellationToken ct = default)
    {
        return _dbStore.GetCardAsync(cardId, ct);
    }

    /// <inheritdoc />
    public Task<ICollection<MagicCard>> GetPrintingsAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(name))
            return Task.FromResult<ICollection<MagicCard>>(Array.Empty<MagicCard>());

        return _dbStore.GetPrintingsAsync(name, ct);
    }

    /// <inheritdoc />
    public Task<ICollection<MagicCardPhysical>> GetInstancesAsync(Guid cardId, CancellationToken ct = default)
    {
        return _collectionStore.GetCardsAsync(cardId, ct);
    }

    /// <inheritdoc />
    public Task<ICollection<MagicCard>> GetBySetAsync(string setAbbreviation, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(setAbbreviation))
            return Task.FromResult<ICollection<MagicCard>>(Array.Empty<MagicCard>());

        return _dbStore.GetBySetAsync(setAbbreviation, ct);
    }

    /// <inheritdoc />
    public Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        return _dbStore.TotalUniqueCardsAsync(ct);
    }

    /// <inheritdoc />
    public Task<ICollection<MagicCard>> GetAllAsync(int limit, CancellationToken ct = default)
    {
        if (limit <= 0)
            return Task.FromResult<ICollection<MagicCard>>(Array.Empty<MagicCard>());

        return _dbStore.GetAllAsync(limit, ct);
    }

    /// <inheritdoc />
    public ICollection<IMagicCard> ApplyFilter(ICollection<IMagicCard> cards, MagicCardFilter filter)
    {
        if (cards == null || !cards.Any())
            return Array.Empty<IMagicCard>();
        if (filter == null)
            return cards;

        var result = filter.FilterCards(cards);
        return result.Cast<IMagicCard>().ToList();
    }

    /// <inheritdoc />
    public Task<CardListResult> GetPagedAsync(CardListFilter filter, CancellationToken ct = default)
    {
        return _dbStore.GetPagedAsync(filter, ct);
    }
}
