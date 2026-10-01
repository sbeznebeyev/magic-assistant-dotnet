using MagicAssistant.Core.Commands;
using MagicAssistant.Core.Queries;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.IO;
using MagicAssistant.Data.IO.json;
using MagicAssistant.Data.IO.xml;
using MagicAssistant.Data.Model;
using MagicAssistant.Core.Services.CardSearch;

namespace MagicAssistant.Core.Services.DeckImportExport;

/// <summary>
/// Vertical slice: deck import/export operations — the domain module for ALL import actions.
/// Orchestrates interactive import flows by calling atomic CQRS units (commands/queries);
/// contains no business logic of its own.
/// </summary>
public class DeckImportExportService : IDeckImportExportService
{
    private readonly ICollectionStore _collectionStore;
    private readonly ICardSearchService _searchService;
    private readonly GetSetSyncStatusQueryHandler _setSyncStatusQuery;
    private readonly ResolveLegacyCardsQueryHandler _resolveLegacyCardsQuery;
    private readonly ValidateCardIdsQueryHandler _validateCardIdsQuery;
    private readonly SyncKBCommandHandler _syncKbCommand;
    private readonly ImportResolvedCardsCommandHandler _importResolvedCardsCommand;

    public DeckImportExportService(
        ICollectionStore collectionStore,
        ICardSearchService searchService,
        GetSetSyncStatusQueryHandler setSyncStatusQuery,
        ResolveLegacyCardsQueryHandler resolveLegacyCardsQuery,
        ValidateCardIdsQueryHandler validateCardIdsQuery,
        SyncKBCommandHandler syncKbCommand,
        ImportResolvedCardsCommandHandler importResolvedCardsCommand)
    {
        _collectionStore = collectionStore;
        _searchService = searchService;
        _setSyncStatusQuery = setSyncStatusQuery;
        _resolveLegacyCardsQuery = resolveLegacyCardsQuery;
        _validateCardIdsQuery = validateCardIdsQuery;
        _syncKbCommand = syncKbCommand;
        _importResolvedCardsCommand = importResolvedCardsCommand;
    }

    // ═══════════════════════════════════════════════════════
    //  Export
    // ═══════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<string> ExportDeckToJsonAsync(int deckId, CancellationToken ct = default)
    {
        if (deckId <= 0)
            throw new ArgumentException("Invalid deck ID.", nameof(deckId));

        var cards = await _collectionStore.GetDeckCardsAsync(deckId, ct);
        var deckNode = await _collectionStore.GetByIdAsync(deckId, ct);

        return JsonExport.ExportToJson(
            name: deckNode?.Name ?? "Unknown Deck",
            comment: null,
            type: "deck",
            cards);
    }

    // ═══════════════════════════════════════════════════════
    //  Import pre-checks (read-only)
    // ═══════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<LegacyImportCheckResult> CheckLegacyImportAsync(
        string xmlContent, CancellationToken ct = default)
    {
        var importResult = ParseOrThrow(xmlContent);

        // Distinct full set names from the file → sync status in one query
        var setNames = importResult.Cards
            .Select(c => c.SetCode!)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var statuses = await _setSyncStatusQuery.Handle(new GetSetSyncStatusQuery(setNames), ct);

        // How many file cards reference each set name
        var cardsPerSetName = importResult.Cards
            .Where(c => !string.IsNullOrEmpty(c.SetCode))
            .GroupBy(c => c.SetCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var result = new LegacyImportCheckResult
        {
            DeckName = importResult.Name,
            TotalCards = importResult.Cards.Count
        };

        foreach (var status in statuses)
        {
            if (status.SetCode == null)
            {
                // Not even metadata in CardSet — cannot auto-sync by name
                result.UnknownSets.Add(status.Name);
            }
            else if (status.CardCount == 0)
            {
                result.MissingSets.Add(new MissingSetInfo(
                    status.SetCode,
                    status.SetName ?? status.Name,
                    cardsPerSetName.TryGetValue(status.Name, out var n) ? n : 0));
            }
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════
    //  Set sync (for missing sets)
    // ═══════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task SyncMissingSetsAsync(
        IReadOnlyList<string> setCodes,
        string[] languages,
        bool downloadImages,
        IProgress<SetSyncProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (setCodes.Count == 0) return;

        for (int i = 0; i < setCodes.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var code = setCodes[i];
            progress?.Report(new SetSyncProgress(i, setCodes.Count, code));

            var syncResult = await _syncKbCommand.Handle(
                new SyncKBCommand(code, languages, downloadImages), ct);

            if (!syncResult.Success)
            {
                var detail = string.Join("; ", syncResult.ErrorMessages.Where(m => !string.IsNullOrEmpty(m)));
                throw new InvalidOperationException(
                    $"Set '{code}' failed to sync{(detail.Length > 0 ? $": {detail}" : "")}.");
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Resolution (read-only)
    // ═══════════════════════════════════════════════════════

    /// <inheritdoc />
    public Task<LegacyCardResolution> ResolveLegacyCardsAsync(
        string xmlContent, CancellationToken ct = default)
    {
        var importResult = ParseOrThrow(xmlContent);
        return _resolveLegacyCardsQuery.Handle(new ResolveLegacyCardsQuery(importResult.Cards), ct);
    }

    // ═══════════════════════════════════════════════════════
    //  Search (for the unresolved-cards table)
    // ═══════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<IReadOnlyList<MagicCard>> SearchCardMatchesAsync(
        string query, CancellationToken ct = default)
    {
        var result = await _searchService.GetPagedAsync(
            new CardListFilter { NameFilter = query, Page = 1, PageSize = 10 }, ct);

        return result.Cards.ToList();
    }

    // ═══════════════════════════════════════════════════════
    //  Import collection assembly (legacy)
    // ═══════════════════════════════════════════════════════

    /// <inheritdoc />
    public Task<IReadOnlyList<MagicCardPhysical>> BuildLegacyImportCardsAsync(
        string xmlContent,
        LegacyCardResolution resolution,
        IReadOnlyDictionary<string, Guid>? cardOverrides = null,
        CancellationToken ct = default)
    {
        var importResult = ParseOrThrow(xmlContent);

        // Auto-resolved (Name|SetCode) → Scryfall UUID; same key space as the overrides
        // (original file values — the query only stamps Id, never renames).
        var resolvedIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var p in resolution.Resolved)
            resolvedIds[$"{p.Name}|{p.SetCode}"] = p.Card.Id;

        var cards = new List<MagicCardPhysical>(importResult.Cards.Count);
        foreach (var card in importResult.Cards)
        {
            var physical = card is MagicCardPhysical p
                ? p
                : new MagicCardPhysical(card.GetBase(), Location.FromId(0, "unresolved", false), true);

            var key = $"{physical.Name}|{physical.SetCode}";
            if (cardOverrides is { Count: > 0 } && cardOverrides.TryGetValue(key, out var mapped))
                physical.Card.Id = mapped;
            else if (resolvedIds.TryGetValue(key, out var resolved))
                physical.Card.Id = resolved;
            else
                throw new InvalidOperationException(
                    $"Card '{key}' is neither resolved nor mapped — the import cannot be committed.");

            cards.Add(physical);
        }

        return Task.FromResult<IReadOnlyList<MagicCardPhysical>>(cards);
    }

    // ═══════════════════════════════════════════════════════
    //  Commit (atomic)
    // ═══════════════════════════════════════════════════════

    /// <inheritdoc />
    public Task<ImportCommitResult> CommitImportAsync(
        string deckName,
        int? parentId,
        int? targetCollectionId,
        IReadOnlyList<MagicCardPhysical> cards,
        IReadOnlyDictionary<string, Guid>? cardOverrides = null,
        CancellationToken ct = default)
    {
        return _importResolvedCardsCommand.Handle(
            new ImportResolvedCardsCommand(deckName, parentId, targetCollectionId, cards, cardOverrides), ct);
    }

    /// <inheritdoc />
    public async Task<ImportCommitResult> CommitModernImportAsync(
        string jsonContent,
        int? parentId,
        int? targetCollectionId,
        string? deckName = null,
        CancellationToken ct = default)
    {
        var importResult = ParseJsonOrThrow(jsonContent);

        // Invariant: validate every card against KB BEFORE anything is created
        var cardIds = importResult.Cards.Select(c => c.Id).ToList();
        var (known, unknown) = await _validateCardIdsQuery.Handle(new ValidateCardIdsQuery(cardIds), ct);

        if (unknown.Count > 0)
        {
            throw new InvalidOperationException(
                $"{unknown.Count} card{(unknown.Count == 1 ? "" : "s")} not found in the knowledge base. " +
                $"Sync the corresponding sets and retry.");
        }

        // Wrap into physicals; location is stamped by the commit command
        var physicals = importResult.Cards
            .Select(c => c is MagicCardPhysical p
                ? p
                : new MagicCardPhysical(c.GetBase(), Location.FromId(0, "unresolved", false), true))
            .ToList();

        var finalName = string.IsNullOrWhiteSpace(deckName) ? importResult.Name : deckName.Trim();

        return await _importResolvedCardsCommand.Handle(
            new ImportResolvedCardsCommand(finalName, parentId, targetCollectionId, physicals), ct);
    }

    // ═══════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════

    private static CardCollectionImportResult ParseOrThrow(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
            throw new ArgumentException("XML content is required.", nameof(xmlContent));

        var importResult = XmlImport.ImportFromXml(xmlContent);
        if (!importResult.Cards.Any())
            throw new InvalidOperationException("No cards found in the file.");

        return importResult;
    }

    private static CardCollectionImportResult ParseJsonOrThrow(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            throw new ArgumentException("JSON content is required.", nameof(jsonContent));

        var importResult = JsonImport.ImportFromJson(jsonContent);
        if (!importResult.Cards.Any())
            throw new InvalidOperationException("No cards found in the file.");

        return importResult;
    }
}
