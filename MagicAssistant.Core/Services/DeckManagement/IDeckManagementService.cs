using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Core.Services.DeckManagement;

/// <summary>
/// Vertical slice: deck and collection management.
/// CRUD operations for decks and folders in the navigation tree.
/// 
/// Tree hierarchy rules:
/// - Folders (NodeType=0) are the only node type that can have children.
/// - Collections (NodeType=1) are leaf nodes — they cannot have children.
/// - Decks (NodeType=2) are leaf nodes in the navigation tree — they cannot have ordinary children.
///   The only exception is their associated sideboard (NodeType=3), which is a hidden database child
///   created automatically and never displayed as a separate tree item.
/// - Sideboards (NodeType=3) always belong to exactly one deck. They are rendered inline with their
///   parent deck inside the same TreeItem, never as independent tree nodes.
///
/// Replaces direct ICollectionStore calls scattered across controllers and handlers.
/// Wraps existing command handlers (CreateDeckCommand, CreateCollectionCommand) into a unified service.
/// </summary>
public interface IDeckManagementService
{
    /// <summary>
    /// Create a navigation node (Folder, Collection, or Deck).
    /// If NodeType == Deck, a sideboard child is also created automatically.
    /// Returns the ID of the created node only (sideboard ID is not returned).
    /// </summary>
    Task<int> CreateNodeAsync(int? parentId, string name, NodeType nodeType, CancellationToken ct = default);

    /// <summary>
    /// Delete a deck and its sideboard child.
    /// </summary>
    Task<bool> DeleteDeckAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Rename a deck or collection.
    /// </summary>
    Task<bool> RenameAsync(int id, string newName, CancellationToken ct = default);

    /// <summary>
    /// Fetch ALL nodes in hierarchy tree
    /// </summary>
    Task<Dictionary<int, NavigatorNodeBase>> GetAllNodesAsync(CancellationToken ct = default);

    /// <summary>
    /// Get a single node by ID.
    /// </summary>
    Task<Collection?> GetByIdAsync(int id, CancellationToken ct = default);

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
    /// Fetch the entire branch (subtree) rooted at a given node ID.
    /// Uses recursive SQL to traverse all descendants.
    /// For Folder nodes, Children are populated. For Deck nodes, Sideboard is populated.
    /// Returns the root node of the branch as a hierarchical NavigatorNodeBase.
    /// </summary>
    Task<NavigatorNodeBase?> GetNodesBranchAsync(int collectionId, CancellationToken ct = default);
}
