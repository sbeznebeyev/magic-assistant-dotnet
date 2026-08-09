
using System.Text.Json.Serialization;

namespace MagicAssistant.Http.Dtos;

public record ScryfallSetDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("scryfall_uri")] string ScryfallUri,
    [property: JsonPropertyName("search_uri")] string SearchUri,
    [property: JsonPropertyName("released_at")] string ReleasedAt,
    [property: JsonPropertyName("set_type")] string SetType,
    [property: JsonPropertyName("card_count")] int CardCount,
    [property: JsonPropertyName("parent_set_code")] string? ParentSetCode,
    [property: JsonPropertyName("digital")] bool Digital,
    [property: JsonPropertyName("nonfoil_only")] bool NonfoilOnly,
    [property: JsonPropertyName("foil_only")] bool FoilOnly,
    [property: JsonPropertyName("icon_svg_uri")] string IconSvgUri
);
