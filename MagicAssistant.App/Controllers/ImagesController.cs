using MagicAssistant.Core;
using Microsoft.AspNetCore.Mvc;

namespace MagicAssistant.App.Controllers;

/// <summary>
/// Serves locally cached card images and set icons.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    private readonly IImageCacheService _imageCache;
    private readonly ISetIconsCacheService _setIconsCache;

    public ImagesController(IImageCacheService imageCache, ISetIconsCacheService setIconsCache)
    {
        _imageCache = imageCache;
        _setIconsCache = setIconsCache;
    }

    /// <summary>
    /// GET /api/images/sets/{setCode}.svg
    /// Returns a cached set icon (SVG).
    /// </summary>
    [HttpGet("sets/{setCode}.svg")]
    public IActionResult GetSetIcon(string setCode)
    {
        var fullPath = _setIconsCache.GetFullPath(setCode);

        if (!System.IO.File.Exists(fullPath))
            return NotFound($"Set icon '{setCode}' not found.");

        return PhysicalFile(fullPath, "image/svg+xml");
    }

    /// <summary>
    /// GET /api/images/{setFolder}/front/{fileName}
    /// Returns a cached card image (PNG).
    /// </summary>
    [HttpGet("{setFolder}/{lang}/front/{fileName}")]
    public IActionResult GetFrontImage(string setFolder, string lang, string fileName, [FromServices] IImageCacheService imageCacheService)
    {
        return GetFile(Path.Combine(setFolder, lang, "front", fileName));
    }

    /// <summary>
    /// GET /api/images/{setFolder}/back/{fileName}
    /// Returns a cached card image (PNG).
    /// </summary>
    [HttpGet("{setFolder}/{lang}/back/{fileName}")]
    public IActionResult GetBackImage(string setFolder, string lang, string fileName, [FromServices] IImageCacheService imageCacheService)
    {
        return GetFile(Path.Combine(setFolder, lang, "back", fileName));
    }

    private IActionResult GetFile(string relativePath)
    {
        var fullPath = $"{Path.Combine(_imageCache.ImagesDir, relativePath)}.png";

        if (!System.IO.File.Exists(fullPath))
            return NotFound($"Image '{relativePath}' not found.");

        return PhysicalFile(fullPath, "image/png", enableRangeProcessing: true);
    }
}
