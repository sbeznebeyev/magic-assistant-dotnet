using System.Text.Json.Serialization;

namespace MagicAssistant.Http.Dtos;

/// <summary>
/// A List object represents a requested sequence of other objects (Cards, Sets, etc). List objects may be paginated, and also include information about issues raised when generating the list.
/// </summary>
/// <typeparam name="T">Objects type</typeparam>
/// <param name="Object">A content type for this object, always list. </param>
/// <param name="HasMore">True if this List is paginated and there is a page beyond the current page.</param>
/// <param name="NextPage">If there is a page beyond the current page, this field will contain a full API URI to that page. You may submit a HTTP GET request to that URI to continue paginating forward on this List.</param>
/// <param name="TotalCards">If this is a list of Card objects, this field will contain the total number of cards found across all pages.</param>
/// <param name="Data">An array of the requested objects, in a specific order.</param>
/// <param name="Warnings">An array of human-readable warnings issued when generating this list, as strings. Warnings are non-fatal issues that the API discovered with your input. In general, they indicate that the List will not contain the all of the information you requested. You should fix the warnings and re-submit your request. </param>
public record ScryfallSearchResponse<T>(
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("has_more")] bool HasMore,
    [property: JsonPropertyName("next_page")] string? NextPage,
    [property: JsonPropertyName("total_cards")] int? TotalCards,
    [property: JsonPropertyName("data")] ICollection<T> Data,
    [property: JsonPropertyName("warnings")] string? Warnings
) where T : class;
