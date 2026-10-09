using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Get all printings of a card by name (different sets, editions).
/// Mirrors Java: PrintingsView + DbMultiFileCardStore.getPrintingsAsync()
/// </summary>
public record GetCardPrintingsQuery(string CardName);

public class GetCardPrintingsQueryHandler
{
    private readonly IDbCardStore _dbStore;

    public GetCardPrintingsQueryHandler(IDbCardStore dbStore)
    {
        _dbStore = dbStore;
    }

    /// <summary>
    /// Returns all known printings of a card across different sets.
    /// </summary>
    public async Task<ICollection<Card>> Handle(GetCardPrintingsQuery query, CancellationToken ct = default)
    {
        return await _dbStore.GetPrintingsAsync(query.CardName, ct);
    }
}
