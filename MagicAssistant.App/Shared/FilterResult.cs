/// <summary>
/// Result of a filter operation. Either text search or field filters, not both.
/// </summary>
public class FilterResult
{
    public string? TextSearch { get; set; }
    public Dictionary<string, string>? FieldFilters { get; set; }
}

/// <summary>
/// Result of a deck filter operation.
/// </summary>
public class DeckFilterResult
{
    public string Ownership { get; set; } = "all";
    public int? MinCount { get; set; }
    public int? MaxCount { get; set; }
    public float? MinPrice { get; set; }
    public float? MaxPrice { get; set; }
    public bool HasFoil { get; set; }
    public bool HasForTrade { get; set; }
    public bool HasComment { get; set; }
    public string? TextSearch { get; set; }
    public string? TypeFilter { get; set; }
    public string? SetFilter { get; set; }
    public string? ColorFilter { get; set; }
}

/// <summary>
/// Result of location selection.
/// </summary>
public class LocationResult
{
    public int DeckId { get; set; }
    public bool IsSideboard { get; set; }
}