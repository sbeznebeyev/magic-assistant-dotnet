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

    /// <summary>Export a deck to the native Magic Assistant JSON format (v1). Returns the JSON string.</summary>
    Task<string> ExportDeckToJsonAsync(int deckId, CancellationToken ct = default);

    // ═══════════════════════════════════════════════════════
    //  Import pre-checks (read-only)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Parses a legacy Magic Assistant XML and reports which sets need syncing from Scryfall
    /// before import: sets present in CardSet but without card data, and unknown set names.
    /// </summary>
    Task<LegacyImportCheckResult> CheckLegacyImportAsync(string xmlContent, CancellationToken ct = default);

    /// <summary>
    /// Parses a native Magic Assistant JSON and reports which sets need syncing from Scryfall
    /// before import (set codes from cards + sideboard with no card data in the knowledge base),
    /// plus how many card IDs are currently unknown — they become known once their sets sync.
    /// </summary>
    Task<ModernImportCheckResult> CheckModernImportAsync(string jsonContent, CancellationToken ct = default);

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
    //  Import collection assembly (legacy)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Assembles the single import collection for a legacy file: every physical from the XML
    /// with its final Scryfall UUID — auto-resolved ones stamped from
    /// <paramref name="resolution"/>, manually mapped ones from
    /// <paramref name="cardOverrides"/> (key "OriginalName|SetCode").
    /// Throws when any card is neither resolved nor mapped — the commit invariant.
    /// </summary>
    Task<IReadOnlyList<MagicCardPhysical>> BuildLegacyImportCardsAsync(
        string xmlContent,
        LegacyCardResolution resolution,
        IReadOnlyDictionary<string, Guid>? cardOverrides = null,
        CancellationToken ct = default);

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
    /// Modern import (Scryfall UUIDs in the file): parses native JSON, validates that every
    /// card exists in the knowledge base, then commits atomically. Throws when unknown cards
    /// are present — no node is created in that case.
    /// When a new deck is created, <paramref name="deckName"/> overrides the name from the file
    /// (null/blank = use the file's name).
    /// </summary>
    Task<ImportCommitResult> CommitModernImportAsync(
        string jsonContent,
        int? parentId,
        int? targetCollectionId,
        string? deckName = null,
        CancellationToken ct = default);
}

// ═══════════════════════════════════════════════════════
//  Result types
// ═══════════════════════════════════════════════════════

/// <summary>A set referenced by the file that has metadata in KB but no card data.</summary>
public record MissingSetInfo(string Code, string Name, int CardsInFile);

/// <summary>Result of the native JSON import pre-check (read-only).</summary>
public class ModernImportCheckResult
{
    public string DeckName { get; init; } = string.Empty;
    public int TotalCards { get; init; }

    /// <summary>Set codes referenced by the file (cards + sideboard) with no card data in KB.
    /// Syncable from Scryfall even when their metadata is absent from CardSet.</summary>
    public List<MissingSetInfo> MissingSets { get; init; } = new();

    /// <summary>Card IDs not found in the knowledge base — resolved by syncing their sets.</summary>
    public int UnknownCardCount { get; init; }
}

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
