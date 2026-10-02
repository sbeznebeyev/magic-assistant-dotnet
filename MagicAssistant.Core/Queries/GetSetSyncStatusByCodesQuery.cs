using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Sync status of set codes in the knowledge base (read-only).
/// Used by the native JSON import pre-check: files carry set codes, not full names.
/// A code with CardCount == 0 needs syncing from Scryfall — even when its metadata is
/// absent from CardSet, because the sync resolves the set from Scryfall itself.
/// </summary>
public record GetSetSyncStatusByCodesQuery(ICollection<string> SetCodes);

public class GetSetSyncStatusByCodesQueryHandler
{
    private readonly IDbCardStore _kbStore;

    public GetSetSyncStatusByCodesQueryHandler(IDbCardStore kbStore)
    {
        _kbStore = kbStore;
    }

    public Task<IReadOnlyList<SetSyncStatus>> Handle(
        GetSetSyncStatusByCodesQuery query, CancellationToken ct = default)
    {
        return _kbStore.GetSetSyncStatusByCodeAsync(query.SetCodes, ct);
    }
}
