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
    Task<bool> CopyCardsAsync(int deckId, ICollection<IMagicCard> cards, CancellationToken ct = default);

    /// <summary>
    /// Add cards to a deck by resolving card IDs from KB and creating physical instances.
    /// Uses batch resolution for efficiency.
    /// </summary>
    Task<ICollection<MagicCardPhysical>> MaterializeFromIdsAsync(int deckId, ICollection<Guid> cardIds, CancellationToken ct = default);

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
    Task<MagicCardPhysical?> SplitCardAsync(int deckId, Guid cardId, int rightCount, CancellationToken ct = default);

    /// <summary>
    /// Split cards from a collection into two parts.
    /// </summary>
    Task<ICollection<MagicCardPhysical>> SplitCardsAsync(ICollection<IMagicCard> cards, int rightCount, CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════════
    //  Read operations
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Get all physical cards for a specific deck (non-sideboard only) with full card info.
    /// </summary>
    Task<ICollection<MagicCardPhysical>> GetDeckCardsAsync(int deckId, CancellationToken ct = default);

    /// <summary>
    /// Get all physical cards for a specific deck including sideboard with full card info.
    /// </summary>
    Task<ICollection<MagicCardPhysical>> GetDeckWithSideboardAsync(int deckId, CancellationToken ct = default);

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
