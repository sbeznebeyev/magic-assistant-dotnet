using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Bulk presence check for the read-only SB badge in the deck view: given the Card.Ids of
/// the displayed page, answers which cards also have at least one copy in the deck's
/// sideboard. One query per page — no per-row round-trips.
/// </summary>
public record GetSideboardPresenceQuery(int DeckId, ICollection<Guid> CardIds);

public class GetSideboardPresenceQueryHandler
{
    private readonly ICollectionStore _collectionStore;

    public GetSideboardPresenceQueryHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    /// <summary>CardId → true when the sideboard holds ≥1 copy. A deck without a sideboard
    /// node answers all-false; every requested id is always present in the result.</summary>
    public async Task<Dictionary<Guid, bool>> Handle(GetSideboardPresenceQuery query, CancellationToken ct = default)
    {
        var result = query.CardIds.Distinct().ToDictionary(id => id, _ => false);
        if (result.Count == 0) return result;

        var sbId = await _collectionStore.GetSideboardIdAsync(query.DeckId, ct);
        if (sbId is null) return result;

        var counts = await _collectionStore.GetCardCountsAsync(new[] { sbId.Value }, result.Keys, ct);
        foreach (var (key, count) in counts)
        {
            if (count > 0 && result.TryGetValue(key.CardId, out _))
                result[key.CardId] = true;
        }

        return result;
    }
}
