namespace MagicAssistant.Core;

/// <summary>
/// Downloads and caches set icon SVGs locally.
/// 
/// File naming: &lt;set-icons-dir&gt;/&lt;set-code&gt;.svg
/// Examples:
///   m19.svg
///   afr.svg
///   neo.svg
/// </summary>
public class SetIconsCacheService : ISetIconsCacheService
{
    private readonly string _setIconsDir;
    private readonly HttpClient _httpClient;

    public SetIconsCacheService(string setIconsDir, HttpClient httpClient)
    {
        _setIconsDir = Path.GetFullPath(setIconsDir);
        if (!Directory.Exists(_setIconsDir))
            Directory.CreateDirectory(_setIconsDir);

        _httpClient = httpClient;
    }

    /// <summary>
    /// Returns the full local file path for a set icon.
    /// </summary>
    public string GetFullPath(string setCode)
    {
        return Path.Combine(_setIconsDir, $"{setCode}.svg");
    }

    /// <summary>
    /// Returns the set of set codes that already have icon SVGs downloaded.
    /// Scans the directory for *.svg files and extracts set codes from filenames.
    /// </summary>
    public ICollection<string> GetExistingSetCodes()
    {
        if (!Directory.Exists(_setIconsDir))
            return Array.Empty<string>();

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var filePath in Directory.GetFiles(_setIconsDir, "*.svg"))
        {
            var code = Path.GetFileNameWithoutExtension(filePath);
            if (!string.IsNullOrEmpty(code))
                codes.Add(code.ToLowerInvariant());
        }

        return codes;
    }

    /// <summary>
    /// Downloads missing set icons from Scryfall.
    /// Only downloads icons that do not already exist on disk.
    /// Returns the number of successfully downloaded icons.
    /// </summary>
    public async Task<int> DownloadAsync(
        IEnumerable<(string Code, string IconSvgUri)> icons,
        CancellationToken ct = default)
    {
        var existingCodes = GetExistingSetCodes();
        var toDownload = icons
            .Where(i => !existingCodes.Contains(i.Code.ToLowerInvariant()))
            .ToList();

        if (toDownload.Count == 0)
            return 0;

        int downloaded = 0;

        foreach (var (code, uri) in toDownload)
        {
            if (ct.IsCancellationRequested)
                break;

            if (string.IsNullOrEmpty(uri))
                continue;

            var fullPath = GetFullPath(code);

            try
            {
                var response = await _httpClient.GetAsync(uri, ct);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var fileStream = File.Create(fullPath);
                await stream.CopyToAsync(fileStream, ct);

                downloaded++;
            }
            catch (HttpRequestException)
            {
                // Skip failed downloads — continue with next icon
                continue;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return downloaded;
    }

    public string SetIconsDir => _setIconsDir;
}

public interface ISetIconsCacheService
{
    /// <summary>
    /// Returns set codes that already have icon SVGs on disk.
    /// </summary>
    ICollection<string> GetExistingSetCodes();

    /// <summary>
    /// Downloads missing set icons from Scryfall.
    /// Returns the number of successfully downloaded icons.
    /// </summary>
    Task<int> DownloadAsync(IEnumerable<(string Code, string IconSvgUri)> icons, CancellationToken ct = default);

    /// <summary>
    /// Returns the full local file path for a set icon.
    /// </summary>
    string GetFullPath(string setCode);

    string SetIconsDir { get; }
}
