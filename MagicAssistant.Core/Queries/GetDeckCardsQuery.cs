using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Queries;

public record GetDeckCardsQuery(int DeckId);

public class GetDeckCardsQueryHandler
{
    private readonly ICollectionStore _collectionStore;

    public GetDeckCardsQueryHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    public async Task<ICollection<ICard>> Handle(GetDeckCardsQuery query, CancellationToken ct = default)
    {
        // Now uses the optimized SQL-level filter with JOIN to fetch all cards for the deck
        var deckCards = await _collectionStore.GetDeckCardsAsync(query.DeckId, ct);
        return deckCards.Cast<ICard>().ToList();
    }
}
