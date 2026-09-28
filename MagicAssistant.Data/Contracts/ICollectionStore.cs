using MagicAssistant.Data.DbModel;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Data.Contracts;

public interface ICollectionStore
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<ICollection<Collection>> GetBranchAsync(int collectionId, CancellationToken ct = default);

    /// <summary>
    /// Fetch ALL non-sideboard nodes in a single SQL query.
    /// Returns flat list; caller builds the tree on .NET side.
    /// </summary>
    Task<ICollection<Collection>> GetAllCollectionsAsync(CancellationToken ct = default);

    Task<Collection?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Create a deck (NodeType=2) with an automatically created sideboard child (NodeType=3).
    /// Decks are leaf nodes in the navigation tree — they cannot have ordinary children.
    /// The sideboard is a hidden child row: it exists in the database but is never displayed
    /// as a separate tree item; it is rendered inline with its parent deck.
    /// </summary>
    Task<Collection> CreateDeckAsync(string name, int? parentId = null, CancellationToken ct = default);

    /// <summary>
    /// Atomic import commit: creates a deck (NodeType=2) with its sideboard child and adds
    /// the given cards — all in ONE transaction. If any insert fails, neither the node
    /// nor the cards appear in the database.
    /// Cards must already carry valid CardIds present in the knowledge base;
    /// their Location is stamped from the created node before insertion.
    /// </summary>
    Task<Collection> CreateDeckWithCardsAsync(string name, IEnumerable<MagicCardPhysical> cards, int? parentId = null, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<bool> RenameAsync(int id, string newName, CancellationToken ct = default);

    /// <summary>
    /// Lock a node (deck, folder, collection, sideboard). Locked nodes cannot be deleted or renamed.
    /// For Collection/Deck/Sideboard: card operations (add/remove/update) are also blocked.
    /// </summary>
    Task<bool> LockAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Unlock a node. Removes all restrictions.
    /// </summary>
    Task<bool> UnlockAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Get direct children of a parent node.
    /// Excludes sideboard nodes (NodeType=3) from results — they are not tree items.
    /// Only folders can have children; decks and collections are leaf nodes.
    /// </summary>
    Task<IEnumerable<Collection>> GetChildrenAsync(int? parentId, CancellationToken ct = default);

    /// <summary>
    /// Get all physical cards for a specific deck (non-sideboard only) with full card info.
    /// </summary>
    Task<ICollection<MagicCardPhysical>> GetDeckCardsAsync(int deckId, CancellationToken ct = default);

    /// <summary>
    /// Get all physical cards for a specific deck including sideboard with full card info.
    /// </summary>
    Task<ICollection<MagicCardPhysical>> GetDeckWithSideboardAsync(int deckId, CancellationToken ct = default);

    /// <summary>
    /// Get ALL physical cards across ALL decks (for reconciliation).
    /// </summary>
    Task<ICollection<MagicCardPhysical>> GetAllPhysicalCardsAsync(CancellationToken ct = default);

    /// <summary>
    /// Create a collection node (NodeType = 1) WITHOUT sideboard.
    /// Collections are leaf nodes — they cannot have children in the tree.
    /// Unlike CreateDeckAsync, this creates only one row in Collection table with NodeType = Collection.
    /// </summary>
    Task<Collection> CreateCollectionAsync(string name, int? parentId, CancellationToken ct = default);

    /// <summary>
    /// Create a folder node (NodeType = 0) WITHOUT sideboard.
    /// Folders are the only node type that can have children. Cannot contain cards directly.
    /// </summary>
    Task<Collection> CreateFolderAsync(string name, int? parentId, CancellationToken ct = default);

    /// <summary>
    /// Compute ownership statistics for all unique card IDs in one SQL query.
    /// Returns CardId → { OwnCount, TotalCount, OwnUnique } for efficient reconciliation.
    /// </summary>
    Task<Dictionary<Guid, OwnCountStats>> GetOwnCountStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Sum of counts for the given card IDs within the given collections (exact collection rows only).
    /// Returns (CollectionId, CardId) → total count; absent keys mean the card is not present in that collection (treat as 0).
    /// Lightweight alternative to GetDeckCardsAsync when only totals are needed.
    /// </summary>
    Task<Dictionary<(int CollectionId, Guid CardId), int>> GetCardCountsAsync(IEnumerable<int> collectionIds, IEnumerable<Guid> cardIds, CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════════
    //  Card CRUD — implemented by SqliteCollectionStore via ICardStore&lt;MagicCardPhysical&gt;
    //  Exposed here so vertical services can work with ICollectionStore directly.
    //  ═══════════════════════════════════════════════════════════

    Task<bool> AddAsync(MagicCardPhysical card, CancellationToken ct = default);
    Task<bool> AddAllAsync(IEnumerable<MagicCardPhysical> cards, CancellationToken ct = default);
    Task<bool> RemoveAsync(MagicCardPhysical card, CancellationToken ct = default);
    Task<bool> UpdateAsync(MagicCardPhysical card, ICollection<MagicCardField> fields, CancellationToken ct = default);
    Task<ICollection<MagicCardPhysical>> GetCardsAsync(Guid cardId, CancellationToken ct = default);
    Task<IEnumerable<MagicCardPhysical>> GetAllDeckCardsAsync(CancellationToken ct = default);
}
