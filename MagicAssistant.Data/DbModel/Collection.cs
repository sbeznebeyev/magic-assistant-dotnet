using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Data.DbModel;

/// <summary>
/// Represents a node in the collection/deck tree hierarchy.
///
/// Tree structure constraints:
/// - <see cref="NodeType.Folder" /> — the only node type that can have children.
/// - <see cref="NodeType.Collection" /> — leaf node; cannot have children.
/// - <see cref="NodeType.Deck" /> — leaf node in the tree; cannot have ordinary children.
///   Its associated <see cref="NodeType.Sideboard" /> is a hidden database child, never a separate tree item.
/// - <see cref="NodeType.Sideboard" /> — always a child of exactly one Deck. Never appears in the tree.
/// </summary>
public class Collection
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public NodeType NodeType { get; set; }
    public int CardCount { get; set; }
    public bool Locked { get; set; }

    /// <summary>Positional constructor for Dapper mapping and legacy callers.</summary>
    public Collection(int id, string name, int? parentId, NodeType nodeType, int cardCount, bool locked = false)
    {
        Id = id;
        Name = name;
        ParentId = parentId;
        NodeType = nodeType;
        CardCount = cardCount;
        Locked = locked;
    }

    /// <summary>Parameterless constructor for object initializers.</summary>
    public Collection() { }

    /// <summary>
    /// Creates a virtual "Add Node" button for the given parent.
    /// Id=-1 is never persisted to DB.
    /// </summary>
    public static Collection AddNodeFor(int? parentId) => new(-1, "+", parentId, NodeType.Folder, 0);

    /// <summary>
    /// True for the virtual AddNode button (Id == -1).
    /// </summary>
    public bool IsAddNode => Id == -1;
}
