using MagicAssistant.Data.Model;

namespace MagicAssistant.Core;

/// <summary>
/// Downloads and caches card images locally.
/// 
/// File naming: &lt;images-dir&gt;/&lt;set-abbreviation&gt;/&lt;sanitized-collector-number&gt;_&lt;card-uid&gt;.png
/// Examples:
///   afr/1_D89203BA-8EBA-41F5-8CC3-DF3327F577A9.png
///   m25/142a_F4A1B2C3-D4E5-F6A7-B8C9-D0E1F2A3B4C5.png
///   neo/A5_12345678-ABCD-EFGH-IJKL-MNOPQRSTUVWX.png
/// 
/// The card UID (Scryfall ID) ensures uniqueness for flip cards, alternate arts, tokens, etc.
/// </summary>
public class ImageCacheService : IImageCacheService
{
    private const string FRONT = "front";
    private const string BACK = "back";
    private readonly string _imagesDir;
    private readonly HttpClient _httpClient;

    public ImageCacheService(string imagesDir, HttpClient httpClient)
    {
        _imagesDir = Path.GetFullPath(imagesDir);
        if (!Directory.Exists(_imagesDir))
            Directory.CreateDirectory(_imagesDir);

        _httpClient = httpClient;
    }

    /// <summary>
    /// Builds the local file name for a card image.
    /// </summary>
    public static string GetRelativeFrontFilePath(Card card) => $"{card.SetCode}/{card.Language ?? "en"}/{FRONT}/{card.Id}.png";
    public static string GetRelativeBackFilePath(Card card) => $"{card.SetCode}/{card.Language ?? "en"}/{BACK}/{card.Id}.png";

    /// <summary>
    /// Returns the directory path for a specific set's images.
    /// </summary>
    public string GetSetDir(string setCode) => Path.Combine(_imagesDir, setCode);

    public void CreateSetLanguageDirs(string setCode, string[] languages)
    {
        foreach (var lang in languages)
        {
            var frontDir = Path.Combine(_imagesDir, setCode, lang, FRONT);
            var backDir = Path.Combine(_imagesDir, setCode, lang, BACK);
            Directory.CreateDirectory(frontDir);
            Directory.CreateDirectory(backDir);
        }
    }

    /// <summary>
    /// Returns the full local file path for a card image.
    /// </summary>
    public string BuildFrontFullPath(Card card) => Path.Combine(_imagesDir, GetRelativeFrontFilePath(card));

    public string BuildBackFullPath(Card card) => Path.Combine(_imagesDir, GetRelativeBackFilePath(card));

    /// <summary>
    /// Returns the relative file name (set/sanitized-num_uid.png).
    /// </summary>
    public string GetFrontFileName(Card card) => GetRelativeFrontFilePath(card);

    public string GetBackFileName(Card card) => GetRelativeBackFilePath(card);

    /// <summary>
    /// Checks if an image file already exists locally.
    /// </summary>
    public bool FrontImageExists(Card card) => File.Exists(BuildFrontFullPath(card));

    public bool BackImageExists(Card card) => File.Exists(BuildBackFullPath(card));

    /// <summary>
    /// Returns the set of Scryfall card IDs that already have images downloaded for a given set.
    /// Used to skip re-downloading images during sync.
    /// </summary>
    public ICollection<Guid> GetExistingCardUidsForSet(string setCode)
    {
        var dir = GetSetDir(setCode);

        // Each file matches: {uid}.png — extract the UID part.
        // Use TryParse to skip files with non-GUID names (e.g. leftover from old naming schemes).
        var uids = new HashSet<Guid>();
        foreach (var filePath in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (Guid.TryParse(fileName, out var uid))
                uids.Add(uid);
        }

        return uids;
    }

    /// <summary>
    /// Downloads a card image from Scryfall and saves it locally.
    /// Tries normal quality first, falls back to png, art_crop, then small.
    /// Returns true if the image was downloaded (or already existed).
    /// </summary>
    public async Task<bool> DownloadAsync(Card card, CancellationToken ct = default)
    {
        if (card is null || card.ImageUris is null && card.CardFaces is null) return false;

        if (card.ImageUris is not null)
        {
            var fullFileName = BuildFrontFullPath(card);
            return await DownloadAsync(fullFileName, card.ImageUris, ct);
        }
        else if (card.CardFaces?.Length > 0)
        {
            var allDownloaded = true;
            foreach (var face in card.CardFaces)
            {
                var imageUri = face.ImageUris?.Normal ?? face.ImageUris?.Large ?? face.ImageUris?.Png ?? face.ImageUris?.Small;

                if (imageUri is null)
                { 
                    allDownloaded = false;                   
                    continue;
                }

                var isFront = imageUri.Contains($"/front/");

                if (isFront)
                {
                    var fullFileName = BuildFrontFullPath(card);
                    var successful = await DownloadAsync(fullFileName, face.ImageUris!, ct);
                    if (!successful)
                    {
                        allDownloaded = false;
                    }
                    continue;
                }

                var isBack = imageUri.Contains($"/back/");
                if (isBack)
                {
                    var fullFileName = BuildBackFullPath(card);
                    var successful = await DownloadAsync(fullFileName, face.ImageUris!, ct);
                    if (!successful)
                    {
                        allDownloaded = false;
                    }
                    continue;
                }
            }
            return allDownloaded;
        }
        return false;
    }

    private async Task<bool> DownloadAsync(string fullFileName, ImageUris imageUris, CancellationToken ct = default)
    {
        // Already exists on disk — skip download regardless of imageUris
        if (File.Exists(fullFileName))
        {
            return true;
        }

        // Try URLs in priority order: normal → large → png → small
        string?[] orderedUrls = [imageUris.Normal, imageUris.Large, imageUris.Png, imageUris.Small];

        foreach (var url in orderedUrls)
        {
            if (string.IsNullOrEmpty(url))
                continue;

            try
            {
                var response = await _httpClient.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();

                // The set directory may not exist (set synced without images) — create it on the spot.
                var dir = Path.GetDirectoryName(fullFileName);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var fileStream = File.Create(fullFileName);
                await stream.CopyToAsync(fileStream, ct);
                return true;
            }
            catch (HttpRequestException)
            {
                // Try next URL
                continue;
            }
        }
        return false;
    }

    public string ImagesDir => _imagesDir;
}
