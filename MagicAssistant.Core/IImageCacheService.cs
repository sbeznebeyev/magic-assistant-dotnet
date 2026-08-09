using MagicAssistant.Data.Model;

namespace MagicAssistant.Core;

public interface IImageCacheService
{
    bool FrontImageExists(MagicCard card);
    Task<bool> DownloadAsync(MagicCard card, CancellationToken ct = default);
    string BuildFrontFullPath(MagicCard card);
    string GetFrontFileName(MagicCard card);

    /// <summary>
    /// Creates language subdirectories for a set: {set}/{lang}/front and {set}/{lang}/back.
    /// </summary>
    void CreateSetLanguageDirs(string setCode, string[] languages);
    /// <summary>
    /// Returns Scryfall card IDs that already have images for a given set.
    /// </summary>
    ICollection<Guid> GetExistingCardUidsForSet(string setAbbreviation);
    string ImagesDir { get; }
}
