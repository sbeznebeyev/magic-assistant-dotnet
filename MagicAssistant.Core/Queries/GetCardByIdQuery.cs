using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Get a card from the knowledge base by its Scryfall ID.
/// Mirrors Java: DbMultiFileCardStore.getCard() + CardDescView
/// </summary>
public record GetCardByIdQuery(Guid CardId);

public class GetCardByIdQueryHandler
{
    private readonly IDbCardStore _dbStore;

    public GetCardByIdQueryHandler(IDbCardStore dbStore)
    {
        _dbStore = dbStore;
    }

    public async Task<Card?> Handle(GetCardByIdQuery query, CancellationToken ct = default)
    {
        return await _dbStore.GetCardAsync(query.CardId, ct);
    }
}
