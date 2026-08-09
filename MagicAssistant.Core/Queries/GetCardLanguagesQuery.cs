using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Get distinct (Language, Id) pairs for all cards sharing the same OracleId.
/// Used to display language toggle buttons in CardDescriptionView.
/// </summary>
public record GetCardLanguagesQuery(Guid OracleId);

public class GetCardLanguagesQueryHandler
{
    private readonly IDbCardStore _dbStore;

    public GetCardLanguagesQueryHandler(IDbCardStore dbStore)
    {
        _dbStore = dbStore;
    }

    public async Task<(string Language, Guid Id)[]> Handle(GetCardLanguagesQuery query, CancellationToken ct = default)
    {
        return await _dbStore.GetAvailableLanguagesAsync(query.OracleId, ct);
    }
}
