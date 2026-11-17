using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Queries;

public record SearchCardsQuery(string Query, int Limit = 100);

public class SearchCardsQueryHandler
{
    private readonly IDbCardStore _dbStore;

    public SearchCardsQueryHandler(IDbCardStore dbStore)
    {
        _dbStore = dbStore;
    }

    public async Task<ICollection<ICard>> Handle(SearchCardsQuery query, CancellationToken ct = default)
    {
        var cards = await _dbStore.SearchAsync(query.Query, query.Limit, ct);
        return cards.Cast<ICard>().ToList();
    }
}
