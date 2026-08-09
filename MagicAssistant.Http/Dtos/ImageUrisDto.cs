
using System.Text.Json.Serialization;

namespace MagicAssistant.Http.Dtos;

/// <summary>
/// Links to images
/// </summary>
/// <param name="Small">146 × 204 (JPG)  A small full card image.Designed for use as thumbnail or list icon.</param>
/// <param name="Normal">488 × 680 (JPG) A medium - sized full card image</param>
/// <param name="Large">672 × 936 (JPG) A large full card image</param>
/// <param name="Png">744 × 1040 (PNG) A transparent, rounded full card PNG.This is the best image to use for videos or other high-quality content.</param>
/// <param name="ArtCrop">460 × 652 (JPG) An image of just the art from the card.</param>
public record ImageUrisDto(
    [property: JsonPropertyName("small")] string? Small,
    [property: JsonPropertyName("normal")] string? Normal,
    [property: JsonPropertyName("large")] string? Large,
    [property: JsonPropertyName("png")] string? Png
);
