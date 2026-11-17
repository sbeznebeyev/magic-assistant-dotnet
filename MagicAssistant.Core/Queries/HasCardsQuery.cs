using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Checks whether the knowledge base contains at least one card for a set.
/// Drives the KB empty-state "Sync from Scryfall" button visibility:
/// a selected set with no cards in the DB offers the sync button; a set that
/// has cards but shows an empty list means filters hid them — no button.
/// </summary>
public record HasCardsQuery(string SetCode);

public class HasCardsQueryHandler
{
    private readonly IDbCardStore _cardStore;

    public HasCardsQueryHandler(IDbCardStore cardStore)
    {
        _cardStore = cardStore;
    }

    public Task<bool> Handle(HasCardsQuery query, CancellationToken ct = default)
    {
        return _cardStore.HasCardsForSetAsync(query.SetCode, ct);
    }
}
