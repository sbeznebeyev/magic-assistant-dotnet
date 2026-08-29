/// <summary>
/// Result of a filter operation. Either text search or field filters, not both.
/// </summary>
public class FilterResult
{
    public string? TextSearch { get; set; }
    public Dictionary<string, string>? FieldFilters { get; set; }
}

/// <summary>
/// Result of location selection.
/// </summary>
public class LocationResult
{
    public int DeckId { get; set; }
    public bool IsSideboard { get; set; }
}