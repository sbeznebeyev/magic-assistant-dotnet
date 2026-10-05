using MagicAssistant.Core.Analysis;
using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Services.DeckAnalysis;

/// <summary>
/// Vertical slice: deck analysis and collector progress.
/// Computes mana curve, color distribution, creature stats, ability stats, legality checks.
/// Also provides collector progress tracking across sets.
/// 
/// Collector progress uses a single SQL query (CTE-based) instead of N+1 queries.
/// </summary>
public class DeckAnalysisService : IDeckAnalysisService
{
    private readonly ICollectionStore _collectionStore;
    private readonly IDbCardStore _dbStore;
    private readonly IAnalysisService _analysisService;

    public DeckAnalysisService(
        ICollectionStore collectionStore,
        IDbCardStore dbStore,
        IAnalysisService analysisService)
    {
        _collectionStore = collectionStore;
        _dbStore = dbStore;
        _analysisService = analysisService;
    }

    /// <inheritdoc />
    public async Task<DeckAnalysisResult> AnalyzeDeckAsync(int deckId, CancellationToken ct = default)
    {
        if (deckId <= 0)
            throw new ArgumentException("Invalid deck ID.", nameof(deckId));

        var cards = await _collectionStore.GetDeckCardsAsync(deckId, ct);
        return _analysisService.Analyze(cards);
    }

    /// <inheritdoc />
    public async Task<ICollection<CollectorProgressEntry>> GetCollectorProgressAsync(CancellationToken ct = default)
    {
        // Single SQL query — replaces: GetEditionsAsync + N×GetBySetAsync + recursive tree walk
        var rawRows = await _dbStore.GetCollectorProgressRawAsync(ct);

        return rawRows.Select(r => new CollectorProgressEntry(
            SetAbbreviation: r.SetAbbreviation,
            SetName: r.SetName ?? r.SetAbbreviation,
            OwnedCards: (int)r.OwnedCards,
            TotalCards: (int)r.TotalCards,
            ProgressPercent: r.TotalCards > 0 ? (double)r.OwnedCards / r.TotalCards * 100 : 0,
            ReleaseDate: r.ReleaseDate,
            ParentSetCode: r.ParentSetCode)).ToList();
    }

    /// <inheritdoc />
    public async Task<CollectorProgressEntry?> GetCollectorProgressForSetAsync(
        string setAbbreviation, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(setAbbreviation))
            throw new ArgumentNullException(nameof(setAbbreviation));

        var rawRows = await _dbStore.GetCollectorProgressRawAsync(ct);
        var row = rawRows.FirstOrDefault(r => r.SetAbbreviation == setAbbreviation);

        return row != null ? new CollectorProgressEntry(
            SetAbbreviation: row.SetAbbreviation,
            SetName: row.SetName ?? setAbbreviation,
            OwnedCards: (int)row.OwnedCards,
            TotalCards: (int)row.TotalCards,
            ProgressPercent: row.TotalCards > 0 ? (double)row.OwnedCards / row.TotalCards * 100 : 0,
            ReleaseDate: row.ReleaseDate,
            ParentSetCode: row.ParentSetCode) : null;
    }
}
