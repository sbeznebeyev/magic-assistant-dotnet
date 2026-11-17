using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.App.Shared;

/// <summary>
/// A navigation node (deck/collection/sideboard) selected in the navigator,
/// together with its lock state and display name at the moment of selection.
/// <see cref="CollectionId"/> == null means "no collection" (Knowledge Base mode).
/// </summary>
public class LockableCollection
{
    public int? CollectionId { get; set; }
    public bool IsLocked { get; set; }
    /// <summary>Display name of the node (e.g. "My Deck", "MyDeck (sb)"). Null when no collection is set.</summary>
    public string? Name { get; set; }
    /// <summary>The node's type at selection time (Folder never selects — see DecksNavigator.OnSelectDeck).</summary>
    public NodeType? NodeType { get; set; }
}
