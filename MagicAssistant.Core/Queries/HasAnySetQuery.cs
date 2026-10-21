using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Checks whether the knowledge base contains at least one set in the CardSet table.
/// Drives the first-run bootstrap: MainLayout opens the Sync Sets dialog automatically when
/// a fresh database has nothing to show. Direct DB check — AllCardSetsCache is deliberately
/// bypassed, an empty cache must not mask rows that are already committed.
/// </summary>
public record HasAnySetQuery;

public class HasAnySetQueryHandler
{
    private readonly IDbCardStore _cardStore;

    public HasAnySetQueryHandler(IDbCardStore cardStore)
    {
        _cardStore = cardStore;
    }

    public Task<bool> Handle(HasAnySetQuery query, CancellationToken ct = default)
    {
        return _cardStore.HasAnySetAsync(ct);
    }
}
