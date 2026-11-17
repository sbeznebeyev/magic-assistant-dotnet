using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using MagicAssistant.Http.Contracts;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Syncs the list of available MTG sets from Scryfall into the database.
/// </summary>
public record SyncCardSetsCommand(bool FullUpdate = false);

/// <summary>
/// Result of syncing card sets from Scryfall.
/// </summary>
public class SyncCardSetsResult
{
    public bool Success { get; set; }
    public int TotalScryfallSets { get; set; }
    public int NewSetsAdded { get; set; }
    public int ExistingSetsUpdated { get; set; }
    public int ExistingSetsSkipped { get; set; }
    public int IconsDownloaded { get; set; }

    /// <summary>.set-symbol rules appended to sets.css by this run — icons that had no class yet.
    /// 0 when every cached icon was already represented.</summary>
    public int CssRulesAdded { get; set; }

    /// <summary>Set when the stylesheet could not be refreshed. Downloads already succeeded, so the
    /// sync still reports Success — the UI shows this as a warning next to the icon count.</summary>
    public string? CssError { get; set; }

    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Handler for SyncCardSetsCommand.
/// Fetches all sets from Scryfall and syncs them into the database.
/// </summary>
public class SyncCardSetsCommandHandler
{
    private readonly IDbCardStore _dbCardStore;
    private readonly IScrayfallClient _scryfallClient;
    private readonly ISetIconsCacheService _setIconsCache;
    private readonly SetIconsCssGenerator? _setIconsCss;

    public SyncCardSetsCommandHandler(
        IDbCardStore dbCardStore,
        IScrayfallClient scryfallClient,
        ISetIconsCacheService setIconsCache,
        SetIconsCssGenerator? setIconsCss = null)
    {
        _dbCardStore = dbCardStore;
        _scryfallClient = scryfallClient;
        _setIconsCache = setIconsCache;
        _setIconsCss = setIconsCss;
    }

    public async Task<SyncCardSetsResult> Handle(
        SyncCardSetsCommand command,
        CancellationToken ct = default)
    {
        var result = new SyncCardSetsResult();

        try
        {
            // 1. Download all sets from Scryfall
            var scryfallSets = await _scryfallClient.GetSetsAsync(ct);
            result.TotalScryfallSets = scryfallSets.Count;

            // 2. Get existing sets from DB
            var dbSets = await _dbCardStore.GetCardSetsAsync(ct);
            var dbIds = new HashSet<Guid>(dbSets.Select(s => s.Id));

            // 3. Map Scryfall DTOs → CardSet
            CardSet[] mappedSets = scryfallSets.Select(dto => new CardSet(
                id: dto.Id,
                setCode: dto.Code.ToLowerInvariant(),
                name: dto.Name,
                uri: dto.Uri,
                scryfallUri: dto.ScryfallUri,
                searchUri: dto.SearchUri,
                setType: CardSetTypes.Parse(dto.SetType),
                cardCount: dto.CardCount,
                parentSetCode: dto.ParentSetCode,
                digital: dto.Digital,
                nonfoilOnly: dto.NonfoilOnly,
                foilOnly: dto.FoilOnly,
                iconSvgUri: dto.IconSvgUri,
                releaseDate: string.IsNullOrEmpty(dto.ReleasedAt) ? null : DateTime.Parse(dto.ReleasedAt)
            )).ToArray();

            // 4. Split into existing (by Guid Id) and new
            CardSet[] existingToUpsert = mappedSets.Where(s => dbIds.Contains(s.Id)).ToArray();
            CardSet[] newOnly = mappedSets.Where(s => !dbIds.Contains(s.Id)).ToArray();

            if (command.FullUpdate)
            {
                // Full update: UPDATE existing + INSERT new
                if (existingToUpsert.Length > 0)
                {
                    result.ExistingSetsUpdated = await _dbCardStore.BulkUpdateSetsAsync(existingToUpsert, ct);
                }

                if (newOnly.Length > 0)
                {
                    result.NewSetsAdded = await _dbCardStore.BulkInsertNewSetsAsync(newOnly, ct);
                }
            }
            else
            {
                // Normal: upsert only missing sets (by Id)
                if (newOnly.Length > 0)
                {
                    result.NewSetsAdded = await _dbCardStore.BulkUpsertSetsAsync(newOnly, ct);
                }

                result.ExistingSetsSkipped = existingToUpsert.Length;
            }

            // 5. Download set icons
            var iconPairs = scryfallSets
                .Where(dto => !string.IsNullOrEmpty(dto.IconSvgUri))
                .Select(dto => (dto.Code.ToLowerInvariant(), dto.IconSvgUri!))
                .ToArray();

            result.IconsDownloaded = await _setIconsCache.DownloadAsync(iconPairs, ct);

            // 6. Keep the stylesheet in step with the cache: every cached icon without a
            //    .set-symbol class gets one appended. Optional — a missing generator (tests) skips it.
            if (_setIconsCss is not null)
            {
                var css = _setIconsCss.SyncMissing();
                result.CssRulesAdded = css.RulesAdded;
                result.CssError = css.Error;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}
