using MagicAssistant.Data.Model;

namespace MagicAssistant.Data.Contracts;

public interface ICardStore<T> where T : IMagicCard
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<bool> AddAsync(T card, CancellationToken ct = default);
    Task<bool> AddAllAsync(IEnumerable<T> cards, CancellationToken ct = default);
    Task<bool> RemoveAsync(T card, CancellationToken ct = default);
    Task<bool> RemoveAllAsync(IEnumerable<T> cards, CancellationToken ct = default);
    Task<bool> UpdateAsync(T card, ICollection<MagicCardField> fields, CancellationToken ct = default);
    Task<T?> GetCardAsync(Guid cardId, CancellationToken ct = default);
    Task<ICollection<T>> GetCardsAsync(Guid cardId, CancellationToken ct = default);
    Task<int> TotalUniqueCardsAsync(CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllDeckCardsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets up to a limited number of items.
    /// Default implementation loads all then takes limit — override in concrete stores for SQL-level LIMIT.
    /// </summary>
    async Task<ICollection<T>> GetAllAsync(int limit, CancellationToken ct = default)
    {
        var all = await GetAllDeckCardsAsync(ct);
        return all.Take(limit).ToList();
    }

    /// <summary>
    /// Bulk update OwnCount/OwnTotal properties on base cards in KB.
    /// Default: no-op (override in concrete stores that need it).
    /// </summary>
    Task BulkUpdateOwnCountAsync(ICollection<OwnCountStats> stats, CancellationToken ct = default) => Task.CompletedTask;
}