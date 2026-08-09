using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Get the navigation tree of collections and decks.
/// Mirrors Java: DecksNavigator + ModelRoot/CardOrganizer hierarchy
/// </summary>
public record GetNodesTreeQuery();

public class GetNodesTreeQueryHandler
{
    private readonly ICollectionStore _collectionStore;

    public GetNodesTreeQueryHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    public async Task<Dictionary<int, NavigatorNodeBase>> HandleAsync(CancellationToken ct = default)
    {
        var collections = await _collectionStore.GetAllCollectionsAsync(ct);

        if (collections is null || collections.Count == 0)
            return [];

        var nodes = new List<NavigatorNodeBase>();
        foreach (var col in collections.Where(c => c.ParentId is null))
        {
            var node = NodesHelper.GetNode(col, collections);
            nodes.Add(node);
        }
        return nodes.ToDictionary(i => i.Id);
    }
}
