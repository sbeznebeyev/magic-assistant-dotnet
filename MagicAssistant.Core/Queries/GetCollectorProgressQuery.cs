using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Queries;

/// <summary>Progress tracking for collector sets — shared DTO.</summary>
public class CollectorProgressItem
{
    public string SetAbbreviation { get; init; } = "";
    public string? SetName { get; init; }
    public int TotalCards { get; init; }
    public int Collected { get; init; }
    public int Owned { get; init; }
    public double Percent => TotalCards > 0 ? (double)Collected / TotalCards * 100 : 0;
    public bool IsComplete => Collected >= TotalCards && TotalCards > 0;
    public int FourOfEach { get; init; }
}

/// <summary>
/// Get collector progress per set — how many cards of each set the user has collected.
/// Mirrors Java: CollectorView + ProgressColumn (X/Y Z% tracking)
/// </summary>
public record GetCollectorProgressQuery(bool OnlyOwn = false);

public class GetCollectorProgressQueryHandler
{
    private readonly IDbCardStore _dbStore;

    public GetCollectorProgressQueryHandler(IDbCardStore dbStore)
    {
        _dbStore = dbStore;
    }

    /// <summary>
    /// Returns collector progress for all sets.
    /// For each set: total cards in knowledge base vs unique cards collected by user.
    /// Uses a single SQL query (CTE-based) — no recursive tree walk needed.
    /// </summary>
    public async Task<ICollection<CollectorProgressItem>> Handle(GetCollectorProgressQuery query, CancellationToken ct = default)
    {
        var rawRows = await _dbStore.GetCollectorProgressRawAsync(ct);

        var results = rawRows.Select(r => new CollectorProgressItem
        {
            SetAbbreviation = r.SetAbbreviation,
            SetName = r.SetName ?? r.SetAbbreviation,
            TotalCards = (int)r.TotalCards,
            Collected = query.OnlyOwn ? (int)r.OwnedCards : (int)r.CollectedCards,
            Owned = query.OnlyOwn ? (int)r.OwnedCards : (int)r.CollectedCards,
        }).ToList();

        // Sort by progress (complete first), then by set name
        return results.OrderByDescending(r => r.Percent).ThenBy(r => r.SetName).ToList();
    }
}
