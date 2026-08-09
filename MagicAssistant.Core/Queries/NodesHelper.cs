
using MagicAssistant.Data.DbModel;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Core.Queries;

public static class NodesHelper
{
    public static NavigatorNodeBase GetNode(Collection collection, ICollection<Collection> allCollection)
    {
        return collection.NodeType switch
        {
            NodeType.Folder => ToNavigatorNodeFolder(collection, allCollection),
            NodeType.Collection => ToNavigatorNodeCollection(collection),
            NodeType.Deck => ToNavigatorNodeDeck(collection, allCollection),
            _ => throw new NotSupportedException($"Node with {collection.NodeType} type is not supported as independent node"),
        };
    }

    private static NavigatorNodeFolder ToNavigatorNodeFolder(Collection collection, ICollection<Collection> allCollection) => new NavigatorNodeFolder
    {
        Id = collection.Id,
        CardCount = collection.CardCount,
        Locked = collection.Locked,
        Name = collection.Name,
        Children = [.. allCollection.Where(c => c.ParentId == collection.Id).Select(c => GetNode(c, allCollection))],
    };

    private static NavigatorNodeCollection ToNavigatorNodeCollection(Collection collection) => new()
    {
        Id = collection.Id,
        CardCount = collection.CardCount,
        Locked = collection.Locked,
        Name = collection.Name,
    };

    private static NavigatorNodeSideboard ToNavigatorNodeSideboard(Collection collection) => new()
    {
        Id = collection.Id,
        CardCount = collection.CardCount,
        Locked = collection.Locked,
        Name = collection.Name,
    };

    private static NavigatorNodeDeck ToNavigatorNodeDeck(Collection collection, ICollection<Collection> allCollection) => new()
    {
        Id = collection.Id,
        CardCount = collection.CardCount,
        Locked = collection.Locked,
        Name = collection.Name,
        Sideboard = ToNavigatorNodeSideboard(allCollection.First(c => c.ParentId == collection.Id))
    };
}
