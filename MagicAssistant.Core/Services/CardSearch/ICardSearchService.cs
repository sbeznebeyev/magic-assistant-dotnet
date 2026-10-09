using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Services.CardSearch;

/// <summary>
/// Vertical slice: card search and filtering operations.
/// Handles FTS5 full-text search, field-based filtering, printings lookup, and instance tracking.
/// 
/// Wraps existing queries (SearchCardsQuery, GetCardByIdQuery, GetPrintingsQuery, GetInstancesQuery)
/// into a unified service with MagicCardFilter integration.
/// </summary>
public interface ICardSearchService
{
    /// <summary>
    /// Full-text search across knowledge base using FTS5.
    /// Supports: simple terms, column-specific (name:x), phrases ("exact phrase"), boolean (OR/AND).
    /// Results are ranked by relevance.
    /// </summary>
    Task<ICollection<IMagicCard>> SearchAsync(string query, int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Get a single card from KB by its Scryfall ID.
    /// Uses L1 cache (ConcurrentDictionary) for O(1) lookup when available.
    /// </summary>
    Task<MagicCard?> GetCardByIdAsync(Guid cardId, CancellationToken ct = default);

    /// <summary>
    /// Get all printings of a card by its name across all sets.
    /// Results ordered by set abbreviation.
    /// </summary>
    Task<ICollection<MagicCard>> GetPrintingsAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Get all physical instances of a card across all user decks/collections.
    /// Returns MagicCardPhysical objects with location and ownership info.
    /// </summary>
    Task<ICollection<MyCard>> GetInstancesAsync(Guid cardId, CancellationToken ct = default);

    /// <summary>
    /// Get all cards from a specific set in the knowledge base.
    /// Results ordered by collector number.
    /// </summary>
    Task<ICollection<MagicCard>> GetBySetAsync(string setCode, CancellationToken ct = default);

    /// <summary>
    /// Get total card count in the knowledge base.
    /// </summary>
    Task<int> GetTotalCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Get all cards from the knowledge base up to a specified limit.
    /// Uses SQL LIMIT for efficiency — does not load all cards into memory.
    /// </summary>
    Task<ICollection<MagicCard>> GetAllAsync(int limit, CancellationToken ct = default);

    /// <summary>
    /// Apply a MagicCardFilter to a collection of cards.
    /// Uses AST-based expression evaluation for efficient filtering.
    /// </summary>
    ICollection<IMagicCard> ApplyFilter(ICollection<IMagicCard> cards, MagicCardFilter filter);

    /// <summary>
    /// Paginated card list with SQL-level filtering, sorting and pagination.
    /// Filters (SetCode, Rarity), sort field/direction, LIMIT/OFFSET applied in SQL.
    /// Returns the page of cards + total count matching the filters.
    /// </summary>
    Task<CardListResult> GetPagedAsync(CardListFilter filter, CancellationToken ct = default);
}
