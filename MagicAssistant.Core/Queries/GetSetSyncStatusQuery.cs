using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Sync status of full set names in the knowledge base (read-only).
/// Used by the legacy import pre-check to detect "sets without cards"
/// (metadata synced, card data missing) that need downloading from Scryfall.
/// </summary>
public record GetSetSyncStatusQuery(ICollection<string> SetNames);

public class GetSetSyncStatusQueryHandler
{
    private readonly IDbCardStore _kbStore;

    public GetSetSyncStatusQueryHandler(IDbCardStore kbStore)
    {
        _kbStore = kbStore;
    }

    public Task<IReadOnlyList<SetSyncStatus>> Handle(
        GetSetSyncStatusQuery query, CancellationToken ct = default)
    {
        return _kbStore.GetSetSyncStatusAsync(query.SetNames, ct);
    }
}
