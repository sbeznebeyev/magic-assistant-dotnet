namespace MagicAssistant.Data.Model.NavigatorNodes;

/// <summary>
/// Type of a Collection node.
/// 0 = Folder — container for organizing other nodes. Cannot contain cards.
/// 1 = Collection — logical grouping that can contain cards (legacy compatibility).
/// 2 = Deck — standard deck with an associated sideboard child.
/// 3 = Sideboard — child of a Deck, stores backup cards.
/// </summary>
public enum NodeType : int
{
    Folder = 0,
    Collection = 1,
    Deck = 2,
    Sideboard = 3
}
