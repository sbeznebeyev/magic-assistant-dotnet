using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Get all physical instances of a card across all collections/decks.
/// Mirrors Java: InstancesView + DataManager.searchInstances()
/// </summary>
public record GetCardInstancesQuery(Guid CardId);

public class GetCardInstancesQueryHandler
{
    private readonly ICardStore<MagicCardPhysical> _cardStore;

    public GetCardInstancesQueryHandler(ICardStore<MagicCardPhysical> cardStore)
    {
        _cardStore = cardStore;
    }

    /// <summary>
    /// Returns all physical instances of a card across all decks and collections.
    /// Each instance includes the deck/collection location info.
    /// </summary>
    public async Task<ICollection<MagicCardPhysical>> Handle(GetCardInstancesQuery query, CancellationToken ct = default)
    {
        return await _cardStore.GetCardsAsync(query.CardId, ct);
    }
}
