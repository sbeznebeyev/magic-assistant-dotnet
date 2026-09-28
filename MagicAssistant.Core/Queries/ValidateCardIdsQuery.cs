using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Splits card IDs into known (present in the knowledge base) and unknown — read-only.
/// Used by modern import pre-check: every card must exist in KB before a node is created.
/// </summary>
public record ValidateCardIdsQuery(ICollection<Guid> CardIds);

public class ValidateCardIdsQueryHandler
{
    private readonly IDbCardStore _kbStore;

    public ValidateCardIdsQueryHandler(IDbCardStore kbStore)
    {
        _kbStore = kbStore;
    }

    public async Task<(IReadOnlyList<Guid> Known, IReadOnlyList<Guid> Unknown)> Handle(
        ValidateCardIdsQuery query, CancellationToken ct = default)
    {
        var ids = query.CardIds.Distinct().ToList();
        if (ids.Count == 0) return (Array.Empty<Guid>(), Array.Empty<Guid>());

        var found = await _kbStore.GetCardsByIdsAsync(ids, ct);

        var known = new List<Guid>(ids.Count);
        var unknown = new List<Guid>();
        foreach (var id in ids)
            (found.ContainsKey(id) ? known : unknown).Add(id);

        return (known, unknown);
    }
}
