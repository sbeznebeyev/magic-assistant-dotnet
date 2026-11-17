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
    public async Task<ICollection<ICard>> SearchAsync(
        string query, int limit = 100, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<ICard>();

        var cards = await _dbStore.SearchAsync(query.Trim(), limit, ct);
        return cards.Cast<ICard>().ToList();
    }

    /// <inheritdoc />
    public Task<Card?> GetCardByIdAsync(Guid cardId, CancellationToken ct = default)
    {
        return _dbStore.GetCardAsync(cardId, ct);
    }

    /// <inheritdoc />
    public Task<ICollection<Card>> GetPrintingsAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(name))
            return Task.FromResult<ICollection<Card>>(Array.Empty<Card>());

        return _dbStore.GetPrintingsAsync(name, ct);
    }

    /// <inheritdoc />
    public Task<ICollection<MyCard>> GetInstancesAsync(Guid cardId, CancellationToken ct = default)
    {
        return _collectionStore.GetCardsAsync(cardId, ct);
    }

    /// <inheritdoc />
    public Task<ICollection<Card>> GetBySetAsync(string setAbbreviation, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(setAbbreviation))
            return Task.FromResult<ICollection<Card>>(Array.Empty<Card>());

        return _dbStore.GetBySetAsync(setAbbreviation, ct);
    }

    /// <inheritdoc />
    public Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        return _dbStore.TotalUniqueCardsAsync(ct);
    }

    /// <inheritdoc />
    public Task<ICollection<Card>> GetAllAsync(int limit, CancellationToken ct = default)
    {
        if (limit <= 0)
            return Task.FromResult<ICollection<Card>>(Array.Empty<Card>());

        return _dbStore.GetAllAsync(limit, ct);
    }

    /// <inheritdoc />
    public ICollection<ICard> ApplyFilter(ICollection<ICard> cards, MagicCardFilter filter)
    {
        if (cards == null || !cards.Any())
            return Array.Empty<ICard>();
        if (filter == null)
            return cards;

        var result = filter.FilterCards(cards);
        return result.Cast<ICard>().ToList();
    }

    /// <inheritdoc />
    public Task<CardListResult> GetPagedAsync(CardListFilter filter, CancellationToken ct = default)
    {
        return _dbStore.GetPagedAsync(filter, ct);
    }
}
