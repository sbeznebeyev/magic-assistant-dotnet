using MagicAssistant.Data.Model;

namespace MagicAssistant.Core;

public interface IImageCacheService
{
    bool FrontImageExists(Card card);
    bool BackImageExists(Card card);
    Task<bool> DownloadAsync(Card card, CancellationToken ct = default);
    string BuildFrontFullPath(Card card);
    string BuildBackFullPath(Card card);
    string GetFrontFileName(Card card);

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
