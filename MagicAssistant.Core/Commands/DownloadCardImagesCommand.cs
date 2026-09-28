using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Download all images of a single card (front + back / every face) on demand.
/// This is the individual, per-card counterpart of the set-sync image phase:
/// the card's ImageUris/CardFaces are already persisted in the knowledge base,
/// so only the image files themselves are fetched — no Scryfall metadata call.
/// </summary>
public record DownloadCardImagesCommand(Guid CardId);

/// <summary>Result of an individual card image download.</summary>
public class DownloadCardImagesResult
{
    /// <summary>False when the card is not present in the knowledge base.</summary>
    public bool CardFound { get; set; }

    /// <summary>True when every available image file for the card is now on disk.</summary>
    public bool ImagesDownloaded { get; set; }
}

public class DownloadCardImagesCommandHandler
{
    private readonly IDbCardStore _cardStore;
    private readonly IImageCacheService _imageCache;

    public DownloadCardImagesCommandHandler(IDbCardStore cardStore, IImageCacheService imageCache)
    {
        _cardStore = cardStore;
        _imageCache = imageCache;
    }

    public async Task<DownloadCardImagesResult> Handle(DownloadCardImagesCommand command, CancellationToken ct = default)
    {
        // GetCardAsync returns the card WITH ImageUris and CardFaces loaded from the KB.
        var card = await _cardStore.GetCardAsync(command.CardId, ct);
        if (card is null)
            return new DownloadCardImagesResult { CardFound = false };

        // The exact per-card call set sync uses in its "Images" phase:
        // downloads front + back (all faces), skipping files that already exist.
        var downloaded = await _imageCache.DownloadAsync(card, ct);
        return new DownloadCardImagesResult { CardFound = true, ImagesDownloaded = downloaded };
    }
}
