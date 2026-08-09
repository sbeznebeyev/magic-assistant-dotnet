using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.DbModel;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Get a single collection node by its ID.
/// </summary>
public record GetNodeByIdQuery(int Id);

public class GetNodeByIdQueryHandler
{
    private readonly ICollectionStore _collectionStore;

    public GetNodeByIdQueryHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    /// <summary>
    /// Returns the node with its card count, or null if not found.
    /// </summary>
    public async Task<Collection?> Handle(GetNodeByIdQuery query, CancellationToken ct = default)
    {
        return await _collectionStore.GetByIdAsync(query.Id, ct);
    }
}
