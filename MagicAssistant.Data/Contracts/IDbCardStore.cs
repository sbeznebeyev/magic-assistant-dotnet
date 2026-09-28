using MagicAssistant.Data.Model;

namespace MagicAssistant.Data.Contracts;

public interface IDbCardStore : ICardStore<MagicCard>
{
    Task<ICollection<MagicCard>> SearchAsync(string query, int limit = 100, CancellationToken ct = default);
    Task<ICollection<MagicCard>> GetBySetAsync(string setCode, CancellationToken ct = default);
    Task<ICollection<MagicCard>> GetPrintingsAsync(string name, CancellationToken ct = default);

    // Edition methods
    Task AddSetAsync(CardSet cardSet, CancellationToken ct = default);
    Task<ICollection<CardSet>> GetCardSetsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get a single set by its abbreviation code (e.g. "M19", "mh2").
    /// Returns null if not found.
    /// </summary>
    Task<CardSet?> GetCardSetAsync(string setCode, CancellationToken ct = default);

    /// <summary>
    /// Bulk upsert sets in a single transaction (INSERT OR REPLACE).
    /// Used by SyncCardSetsCommandHandler when fullUpdate is false.
    /// </summary>
    Task<int> BulkUpsertSetsAsync(CardSet[] sets, CancellationToken ct = default);

    /// <summary>
    /// Bulk update existing sets in a single transaction (UPDATE WHERE Id IN ...).
    /// Used by SyncCardSetsCommandHandler when fullUpdate is true.
    /// </summary>
    Task<int> BulkUpdateSetsAsync(CardSet[] sets, CancellationToken ct = default);

    /// <summary>
    /// Bulk insert new sets in a single transaction (INSERT OR IGNORE — skips existing by Id).
    /// Used by SyncCardSetsCommandHandler when fullUpdate is true.
    /// </summary>
    Task<int> BulkInsertNewSetsAsync(CardSet[] sets, CancellationToken ct = default);

    /// <summary>
    /// Bulk update specified fields across all cards in the knowledge base.
    /// Invalidates cache so next reads hit the DB.
    /// </summary>
    Task UpdateAllFieldsAsync(ICollection<MagicCardField> fields, CancellationToken ct = default);

    /// <summary>
    /// Update a single card's property in KB without replacing the whole row.
    /// Used by DataManager to persist non-transient properties (ImageUrl, ScryfallId, etc).
    /// </summary>
    Task UpdateCardPropertyAsync(Guid cardId, string columnName, string? value, CancellationToken ct = default);

    /// <summary>
    /// Batch-fetch multiple cards by their IDs in a single SQL query.
    /// Returns a dictionary keyed by CardId for O(1) lookup.
    /// Used by CardResolveService for efficient batch resolution.
    /// </summary>
    Task<Dictionary<Guid, MagicCard>> GetCardsByIdsAsync(ICollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Collector progress for all sets — single SQL query.
    /// Returns per-set: total unique cards in KB vs unique cards the user owns in any deck.
    /// Replaces N+1 queries (GetEditions + GetBySet per edition + recursive tree walk).
    /// </summary>
    Task<ICollection<CollectorProgressRaw>> GetCollectorProgressRawAsync(CancellationToken ct = default);

    /// <summary>
    /// Batch-resolve full set names to set codes in a single SQL query.
    /// Used by ResolveLegacyCardsQuery to convert legacy set names ("Core Set 2019")
    /// to KB abbreviations ("m19").
    /// Returns a dictionary: FullName → SetCode. Only matched names are included.
    /// </summary>
    Task<Dictionary<string, string>> ResolveSetNamesAsync(ICollection<string> fullNames, CancellationToken ct = default);

    /// <summary>
    /// Batch-resolve cards by (Name, SetCode) pairs in a single SQL query.
    /// Used by ResolveLegacyCardsQuery to replace Gatherer IDs with Scryfall UUIDs.
    /// Returns a dictionary: "{Name}|{Set}" → MagicCard. Only matched pairs are included.
    /// </summary>
    Task<Dictionary<string, MagicCard>> ResolveCardsByNameAndSetAsync(ICollection<(string Name, string? Set)> pairs, CancellationToken ct = default);

    /// <summary>
    /// Sync status of the given full set names in a single SQL query (CardSet + Cards counts).
    /// Used by legacy import pre-check to detect "sets without cards" that need syncing from Scryfall.
    /// Returns one row per requested name: resolved code (null when absent from CardSet) and card count.
    /// </summary>
    Task<IReadOnlyList<SetSyncStatus>> GetSetSyncStatusAsync(ICollection<string> setNames, CancellationToken ct = default);

    /// <summary>
    /// Get distinct (Language, Id) pairs for all cards sharing the same OracleId.
    /// Used to display language toggle buttons in CardDescriptionView.
    /// </summary>
    Task<(string Language, Guid Id)[]> GetAvailableLanguagesAsync(Guid oracleId, CancellationToken ct = default);

    /// <summary>
    /// Paginated card list with SQL-level filtering, sorting and pagination.
    /// Filters (SetCode, Rarity), sort field/direction, LIMIT/OFFSET applied in SQL.
    /// Returns the page of cards + total count matching the filters.
    /// </summary>
    Task<CardListResult> GetPagedAsync(CardListFilter filter, CancellationToken ct = default);
}
