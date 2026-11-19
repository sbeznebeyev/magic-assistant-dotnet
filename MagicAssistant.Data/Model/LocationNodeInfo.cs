using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Data.Model;

/// <summary>
/// One navigator node for the Locations filter picker: folders (group headers), collections,
/// decks and sideboards (selectable). Returned by ICollectionStore.GetLocationNodesAsync —
/// a flat projection, the picker builds the grouping from ParentId.
/// </summary>
public sealed record LocationNodeInfo(int Id, string Name, NodeType NodeType, int? ParentId);
