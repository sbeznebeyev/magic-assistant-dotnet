using Microsoft.Extensions.Logging;
using MagicAssistant.Core.Commands;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using MagicAssistant.Http.Contracts;

namespace MagicAssistant.Core.Services.KnowledgeBaseSync;

/// <summary>
/// Vertical slice: synchronizes the knowledge base from Scryfall.
/// 
/// 6-phase orchestration (mirrors Java DataManager.sync + original DataManager.SyncSetAsync):
///   Phase 0: Resolve set — look up set info in Scryfall
///   Phase 1: Download — fetch card checklist via IScrayfallClient
///   Phase 2: Map — DTO → MagicCard via ScryfallMapper (static)
///   Phase 3: Images — download & cache via IImageCacheService
///   Phase 4: Save — persist to IDbCardStore (batch UPSERT)
///   Phase 5: Done — report result
/// 
/// Progress is tracked via SyncSetStateRegistry for UI polling.
/// </summary>
public class KnowledgeBaseSyncService : IKnowledgeBaseSyncService
{
    private readonly IScrayfallClient _scrayflClient;
    private readonly IDbCardStore _dbCardStore;
    private readonly IImageCacheService _imageCacheService;
    private readonly SyncSetStateRegistry _syncSetStateRegistry;
    private readonly ILogger _logger;

    /// <summary>Concurrent card-image downloads in phase 3. Scryfall's file origins
    /// (*.scryfall.io) carry no rate limits (docs checked 2026-10-21); 10 is a polite
    /// ceiling, not a maximum.</summary>
    private const int MaxImageDownloads = 10;

    public KnowledgeBaseSyncService(
        IScrayfallClient scrayflClient,
        IDbCardStore dbCardStore,
        IImageCacheService imageCacheService,
        SyncSetStateRegistry syncSetStateRegistry,
        ILogger<KnowledgeBaseSyncService> logger)
    {
        _scrayflClient = scrayflClient;
        _dbCardStore = dbCardStore;
        _imageCacheService = imageCacheService;
        _syncSetStateRegistry = syncSetStateRegistry;
        _logger = logger;
    }

    public async Task<SyncResult> SyncSetAsync(string setCode, string[] languages, bool downloadImages = true, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new SyncResult { SetCode = setCode };

        if (languages.Length == 0)
            return result;

        var state = new SyncSetState(setCode);
        _syncSetStateRegistry.Register(state);

        _logger.LogInformation("[KB Sync] Starting sync for '{Set}'", setCode);

        try
        {
            // ── Phase 0: Resolve set ──
            state.Phase = 0;
            state.PhaseName = "Resolving";
            state.Message = $"Looking up '{setCode}'...";

            var sets = await _scrayflClient.GetSetsAsync(ct);
            var setDto = sets.FirstOrDefault(
                s => s.Code?.ToLowerInvariant() == setCode);

            if (setDto == null)
            {
                SetError(state, result, $"Set '{setCode}' not found.");
                return result;
            }

            result.SetName = setDto.Name;
            state.SetName = setDto.Name;
            state.TotalCards = setDto.CardCount;  // known from Scryfall set metadata — enables progress bar during download
            _logger.LogInformation("[KB Sync] Found set '{Name}' ({Count} cards)", setDto.Name, setDto.CardCount);

            // ── Phase 1: Download checklist ──
            state.Phase = 1;
            state.PhaseName = "Downloading";
            state.Message = "Downloading cards...";

            var englishOnly = languages.Length == 1 && languages[0] == "en";

            var downloadedDtoCards = await _scrayflClient.GetCardsAsync(
                setCode, englishOnly,
                onPageDownloaded: (count) =>
                {
                    state.DownloadedPages++;
                    state.ReadyCardsToSave = count;  // cumulative cards — drives progress bar
                    state.Message = $"Downloading… ({state.DownloadedPages} pages, {count} cards)";
                },
                ct);
            var dtoCards = downloadedDtoCards.Where(c => Enumerable.Contains(languages, c.Language)).ToList();
            result.CardsDownloaded = dtoCards.Count;
            state.TotalCards = dtoCards.Count;  // adjust to actual filtered count

            // ── Phase 2: Map DTOs → MagicCard ──
            state.Phase = 2;
            state.PhaseName = "Mapping";
            state.Message = "Mapping cards...";

            var magicCards = new List<Card>(dtoCards.Count);
            for (var i = 0; i < dtoCards.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                magicCards.Add(ScryfallMapper.ToMagicCard(dtoCards[i]));
                state.ReadyCardsToSave = i + 1;
                if (i % 50 == 0)
                    state.Message = $"Mapped {i + 1}/{dtoCards.Count}";
            }

            // ── Phase 3: Download images ──
            int imgOk = 0, imgFail = 0, imgSkip = 0;

            if (downloadImages)
            {
                state.Phase = 3;
                state.PhaseName = "Downloading images";
                state.Message = "Checking existing...";

                _imageCacheService.CreateSetLanguageDirs(setCode, languages);
                var existing = _imageCacheService.GetExistingCardUidsForSet(setCode);
                imgSkip = existing.Count;
                state.Message = "Downloading images";
                // Static label for the whole loop: the live counters live in the stats row, and a
                // second copy of them here only updated on a different (25-item) cadence — the
                // old duplicated lagging line.

                // Concurrent slots: each card downloads into its own unique file
                // (<set>/<lang>/front|back/<uid>.png), so the workers never touch the same
                // path; `existing` is read-only here. Counters cross threads → Interlocked.
                var localErrors = 0;
                var opts = new ParallelOptions { MaxDegreeOfParallelism = MaxImageDownloads, CancellationToken = ct };
                await Parallel.ForEachAsync(magicCards, opts, async (card, token) =>
                {
                    if (existing.Contains(card.Id))
                    {
                        Interlocked.Increment(ref imgOk);
                        return;
                    }

                    var cardErrors = 0;
                    try
                    {
                        if (await _imageCacheService.DownloadAsync(card, token))
                            Interlocked.Increment(ref imgOk);
                        else
                            Interlocked.Increment(ref imgFail);
                    }
                    catch
                    {
                        Interlocked.Increment(ref imgFail);
                        cardErrors = 1;
                    }

                    if (cardErrors > 0)
                        Interlocked.Add(ref localErrors, cardErrors);

                    // Last-writer-wins snapshots: the UI polls at 500ms, exactness per frame
                    // is irrelevant; the authoritative values are published after the loop.
                    state.ImagesDownloaded = Volatile.Read(ref imgOk);
                    state.ImagesFailed = Volatile.Read(ref imgFail);
                });
                state.ImagesDownloaded = imgOk;
                state.ImagesFailed = imgFail;
                result.Errors += localErrors;
            }

            result.ImagesDownloaded = imgOk;

            // ── Phase 4: Save to KB ──
            state.Phase = 4;
            state.PhaseName = "Saving";
            state.Message = $"Saving {magicCards.Count} cards...";

            await _dbCardStore.AddAllAsync(magicCards, ct);
            result.CardsPersisted = magicCards.Count;

            // ── Phase 5: Done ──
            sw.Stop();
            state.Phase = 5;
            state.PhaseName = "Done";
            state.Message = $"{result.CardsPersisted} cards, {imgOk} images in {sw.Elapsed.TotalSeconds:F1}s";
            state.Completed = true;
            result.Duration = sw.Elapsed;
            result.Success = true;

            _logger.LogInformation(
                "[KB Sync] Done: {N} cards, {I} images in {T:F1}s",
                result.CardsPersisted, imgOk, sw.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            SetError(state, result, "Cancelled.");
        }
        catch (Exception ex)
        {
            SetError(state, result, ex.Message);
            result.Errors++;
            _logger.LogError(ex, "[KB Sync] Error: {Msg}", ex.Message);
        }

        return result;
    }

    private static void SetError(SyncSetState state, SyncResult result, string message)
    {
        state.Failed = true;
        state.ErrorMessage = message;
        result.Success = false;
        result.ErrorMessages.Add(message);
    }
}
