namespace MagicAssistant.Data.Model;

/// <summary>
/// Ownership statistics for a single card ID across all decks.
/// Used by DataManager.ReconcileAsync for efficient bulk reconciliation.
/// </summary>
public record OwnCountStats(
    Guid CardId,
    int OwnCount,
    int TotalCount);
