using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Services.DeckCard;

/// <summary>
/// Vertical slice: card operations within decks.
/// Handles adding, removing, moving, updating, and splitting cards in deck contexts.
/// 
/// Replaces DataManager.CopyCardsAsync, SplitCardAsync, SplitCardsAsync, MaterializeCardsAsync,
/// RemoveCardsAsync, MoveCardsAsync, UpdateCardAsync, GetDeckCardsAsync, GetDeckWithSideboardAsync.
/// </summary>
public interface IDeckCardService
{
    // ═══════════════════════════════════════════════════════════
    //  CRUD operations
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Copy cards to a target deck. Creates new MagicCardPhysical instances with target location.
    /// </summary>
    Task<bool> CopyCardsAsync(int deckId, ICollection<ICard> cards, CancellationToken ct = default);

    /// <summary>
    /// Add cards to a deck by resolving card IDs from KB and creating physical instances.
    /// Uses batch resolution for efficiency.
    /// </summary>
    Task<ICollection<MyCard>> MaterializeFromIdsAsync(int deckId, ICollection<Guid> cardIds, CancellationToken ct = default);

    /// <summary>
    /// Remove ALL copies of the given cards from a collection node (full-row removal via RemoveCardsFromDeckCommand).
    /// Returns the number of card copies actually removed.
    /// </summary>
    Task<int> RemoveCardsAsync(int deckId, ICollection<Guid> cardIds, CancellationToken ct = default);

    /// <summary>
    /// Wipe every card out of a node — nodeId is the node's own ID (deck, collection or sideboard;
    /// for the sideboard that is NavigatorNodeDeck.Sideboard.Id, not the parent deck's).
    /// The node itself is kept; only its Deck rows are removed (RemoveCardsFromDeckCommand with empty CardIds).
    /// Returns the number of card copies actually removed.
    /// </summary>
    Task<int> ClearNodeAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Move cards from source deck to target deck.
    /// Cards are resolved from KB. Source and target must be different.
    /// </summary>
    Task<int> MoveCardsAsync(int sourceDeckId, ICollection<Guid> cardIds, int targetDeckId, CancellationToken ct = default);

    /// <summary>
    /// Update properties of a specific card instance in a deck.
    /// Fields are specified as MagicCardField → value pairs.
    /// </summary>
    Task<bool> UpdateCardAsync(int deckId, Guid cardId, Dictionary<MagicCardField, object?> updates, CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════════
    //  Split operations
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Split a card pile into two parts (e.g., 4 copies → 1 + 3).
    /// Returns the right part, or null if no split needed.
    /// </summary>
    Task<MyCard?> SplitCardAsync(int deckId, Guid cardId, int rightCount, CancellationToken ct = default);

    /// <summary>
    /// Move copies from a deck's sideboard back into the deck (inverse of SplitCardAsync).
    /// Returns the deck part, or null if nothing to move.
    /// </summary>
    Task<MyCard?> SplitCardBackAsync(int deckId, Guid cardId, int count, CancellationToken ct = default);

    /// <summary>
    /// Move part of a card pile between any two collection nodes (deck/collection/sideboard).
    /// The target row merges counts when the card is already there. Returns the target part, or
    /// null when there is nothing to move (count ≤ 0, source row missing, or count ≥ source —
    /// a whole-row move belongs to MoveCardsAsync).
    /// </summary>
    Task<MyCard?> TransferCardsAsync(int sourceCollectionId, Guid cardId, int count, int targetCollectionId, CancellationToken ct = default);

    /// <summary>
    /// Split cards from a collection into two parts.
    /// </summary>
    Task<ICollection<MyCard>> SplitCardsAsync(ICollection<ICard> cards, int rightCount, CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════════
    //  Read operations
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Get all physical cards for a specific deck (non-sideboard only) with full card info.
    /// </summary>
    Task<ICollection<MyCard>> GetDeckCardsAsync(int deckId, CancellationToken ct = default);

    /// <summary>
    /// (unique, total) stat pair for a node — one SQL aggregate, no card materialization.
    /// </summary>
    Task<(int Unique, int Total)> GetCollectionStatsAsync(int collectionId, CancellationToken ct = default);

    /// <summary>
    /// Full physical rows without CardFaces — the list view path; dialog previews load faces per card.
    /// </summary>
    Task<ICollection<MyCard>> GetDeckCardsForListAsync(int deckId, CancellationToken ct = default);

    /// <summary>Distinct SetCodes present in the node — one DISTINCT query.</summary>
    Task<ICollection<string>> GetNodeSetCodesAsync(int collectionId, CancellationToken ct = default);

    /// <summary>
    /// Get all physical cards for a specific deck including sideboard with full card info.
    /// </summary>
    Task<ICollection<MyCard>> GetDeckWithSideboardAsync(int deckId, CancellationToken ct = default);

    /// <summary>
    /// Sum of counts for the given card IDs within the given decks/collections (exact collection rows only).
    /// Lightweight alternative to GetDeckCardsAsync when only totals are needed.
    /// Returns (CollectionId, CardId) → total count; absent keys mean the card is not present in that collection (treat as 0).
    /// </summary>
    Task<Dictionary<(int CollectionId, Guid CardId), int>> GetDeckCardCountsAsync(ICollection<int> collectionIds, ICollection<Guid> cardIds, CancellationToken ct = default);

    /// <summary>
    /// Total copies of the card within a single deck that are owned (IsOwn = true).
    /// Used to bound the for-trade count in the edit dialog.
    /// </summary>
    Task<int> GetOwnedCountAsync(int deckId, Guid cardId, CancellationToken ct = default);
}
