using MagicAssistant.Core.Analysis;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Services.DeckAnalysis;

/// <summary>
/// Vertical slice: deck analysis and collector progress.
/// Computes mana curve, color distribution, creature stats, ability stats, legality checks.
/// Also provides collector progress tracking across sets.
/// 
/// Wraps IAnalysisService and the deck-analysis / collector-progress query paths.
/// </summary>
public interface IDeckAnalysisService
{
    /// <summary>
    /// Analyze a deck's composition: mana curve, colors, types, creatures, abilities, basic stats.
    /// Single-pass computation for efficiency.
    /// </summary>
    Task<DeckAnalysisResult> AnalyzeDeckAsync(int deckId, CancellationToken ct = default);

    /// <summary>
    /// Get collector progress across all available sets.
    /// Shows how many cards from each set the user owns vs total available.
    /// When <paramref name="setTypes"/> is non-empty, only sets of those types are returned.
    /// </summary>
    Task<ICollection<CollectorProgressEntry>> GetCollectorProgressAsync(IReadOnlyCollection<CardSetType>? setTypes = null, CancellationToken ct = default);

    /// <summary>
    /// Get collector progress for a specific set.
    /// Shows owned cards vs total checklist for that set.
    /// </summary>
    Task<CollectorProgressEntry?> GetCollectorProgressForSetAsync(
        string setAbbreviation, CancellationToken ct = default);

    /// <summary>
    /// Get progress details for a single set: identity (name / release date / type) plus
    /// owned-vs-total breakdowns by card rarity and by mana color. Null when the set is unknown.
    /// </summary>
    Task<SetProgressDetails?> GetSetProgressDetailsAsync(string setCode, CancellationToken ct = default);
}

/// <summary>
/// Collector progress entry for a single set.
/// </summary>
public record CollectorProgressEntry(
    string SetAbbreviation,
    string SetName,
    int OwnedCards,
    int TotalCards,
    double ProgressPercent,          // owned-only: OwnedCards / TotalCards * 100
    DateTime? ReleaseDate = null,   // set release date (Scryfall)
    string? ParentSetCode = null);  // parent set for child sets (promos, digital, etc.)
