using MagicAssistant.Core.Commands;
using MagicAssistant.Core.Queries;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Services.DeckImportExport;

/// <summary>
/// Vertical slice: deck import/export operations — the domain module for ALL import actions.
/// Orchestrates interactive import flows by calling atomic CQRS units (commands/queries);
/// contains no business logic of its own.
///
/// Invariant: a new deck/collection node is never created before every card is resolved.
/// Commit happens only after full resolution (and optional user mapping of unresolved cards).
/// </summary>
public interface IDeckImportExportService
{
    // ═══════════════════════════════════════════════════════
    //  Export
    // ═══════════════════════════════════════════════════════

    /// <summary>Export a deck to Magic Assistant XML format. Returns the XML string.</summary>
    Task<string> ExportDeckToXmlAsync(int deckId, CancellationToken ct = default);

    /// <summary>Export a deck to Magic Assistant XML and save to file.</summary>
    Task<ExportDeckResult> ExportToFileAsync(int deckId, string filePath, CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════
    //  Import pre-checks (read-only)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Parses a legacy Magic Assistant XML and reports which sets need syncing from Scryfall
    /// before import: sets present in CardSet but without card data, and unknown set names.
    /// </summary>
    Task<LegacyImportCheckResult> CheckLegacyImportAsync(string xmlContent, CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════
    //  Set sync (for missing sets)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Syncs the given sets from Scryfall sequentially via SyncKBCommand.
    /// Progress is reported through the IProgress callback; per-set progress state is
    /// available via GetSyncProgressQuery while a set is syncing.
    /// Throws when a set fails or the operation is cancelled.
    /// </summary>
    Task SyncMissingSetsAsync(
        IReadOnlyList<string> setCodes,
        string[] languages,
        bool downloadImages,
        IProgress<SetSyncProgress>? progress = null,
        CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════
    //  Resolution (read-only)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Parses a legacy XML and resolves its cards against the knowledge base.
    /// Returns resolved physicals (real Scryfall UUIDs stamped) and unresolved rows
    /// that need manual mapping or set sync.
    /// </summary>
    Task<LegacyCardResolution> ResolveLegacyCardsAsync(string xmlContent, CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════
    //  Search (for the unresolved-cards table)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Searches the knowledge base by card name — candidates for mapping an unresolved card.
    /// </summary>
    Task<IReadOnlyList<MagicCard>> SearchCardMatchesAsync(string query, CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════
    //  Commit (atomic)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Commits a fully resolved legacy import. New mode: creates the deck under ParentId.
    /// Existing mode: adds to TargetCollectionId. CardOverrides — user mappings from
    /// the unresolved-cards table ("OriginalName|SetCode" → Scryfall UUID).
    /// </summary>
    Task<ImportCommitResult> CommitImportAsync(
        string deckName,
        int? parentId,
        int? targetCollectionId,
        IReadOnlyList<MagicCardPhysical> cards,
        IReadOnlyDictionary<string, Guid>? cardOverrides = null,
        CancellationToken ct = default);

    /// <summary>
    /// Modern import (Scryfall UUIDs in the file): parses XML, validates that every card
    /// exists in the knowledge base, then commits atomically. Throws when unknown cards
    /// are present — no node is created in that case.
    /// </summary>
    Task<ImportCommitResult> CommitModernImportAsync(
        string xmlContent,
        int? parentId,
        int? targetCollectionId,
        CancellationToken ct = default);
}

// ═══════════════════════════════════════════════════════
//  Result types
// ═══════════════════════════════════════════════════════

/// <summary>A set referenced by the file that has metadata in KB but no card data.</summary>
public record MissingSetInfo(string Code, string Name, int CardsInFile);

/// <summary>Result of the legacy import pre-check (read-only).</summary>
public class LegacyImportCheckResult
{
    public string DeckName { get; init; } = string.Empty;
    public int TotalCards { get; init; }

    /// <summary>Sets present in CardSet but without card data — need syncing from Scryfall.</summary>
    public List<MissingSetInfo> MissingSets { get; init; } = new();

    /// <summary>Full set names not found in CardSet at all (original spelling from the file).</summary>
    public List<string> UnknownSets { get; init; } = new();
}

/// <summary>Progress report for sequential multi-set sync: index of the set currently syncing.</summary>
public record SetSyncProgress(int Index, int Total, string SetCode);
