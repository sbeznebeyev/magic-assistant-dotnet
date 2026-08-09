using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Get the navigation tree of collections and decks.
/// Mirrors Java: DecksNavigator + ModelRoot/CardOrganizer hierarchy
/// </summary>
public record GetNodesBranchQuery(int CollectionId);

public class GetNodesBranchQueryHandler
{
    private readonly ICollectionStore _collectionStore;

    public GetNodesBranchQueryHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    public async Task<NavigatorNodeBase?> HandleAsync(GetNodesBranchQuery query, CancellationToken ct = default)
    {
        var collections = await _collectionStore.GetBranchAsync(query.CollectionId, ct);

        if (collections is null || collections.Count == 0)
            return null;

        var root = collections.First(c => c.Id == query.CollectionId);
        return NodesHelper.GetNode(root, collections); ;
    }
}
