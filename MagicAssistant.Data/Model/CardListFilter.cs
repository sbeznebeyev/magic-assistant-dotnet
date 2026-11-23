using System;
using System.Collections.Generic;

namespace MagicAssistant.Data.Model;

/// <summary>
/// Filter parameters for paginated card list queries.
/// All filtering, sorting and pagination is applied at the SQL level (WHERE + ORDER BY + LIMIT/OFFSET).
/// </summary>
public class CardListFilter
{
    /// <summary>Set code filters — multiple values combined as OR expression (set1 OR set2 OR ...). Applied via exact match.</summary>
    public List<string>? SetCodes { get; init; }

    public string SortField { get; init; } = SortFields.Name;
    public bool SortAsc { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }

    /// <summary>Full-text search query (applied via FTS5 on Name/Type/OracleText/Artist).</summary>
    public string? Query { get; init; }

    /// <summary>Location filter: Collection node Id (deck/sideboard/collection/folder).
    /// When set, only cards present in that location are returned (INNER JOIN Deck).
    /// Null = full Knowledge Base.</summary>
    public int? LocationFilter { get; init; }

    /// <summary>Locations multi-chip filter (the Locations row in Card Filter). Mutually exclusive with
    /// <see cref="LocationFilter"/> (single chip keeps the deck-mode JOIN path).
    /// Non-null restricts the list to physical cards via EXISTS over Deck:
    /// empty list = every collection/deck/sideboard; non-empty = only these nodes.
    /// Null = locations filter off (KB mode). The Deck alias inside EXISTS also carries the
    /// collection filters (ownership/count/price/forTrade/comment).</summary>
    public List<int>? LocationNodeIds { get; init; }

    /// <summary>Card name filter (LIKE-based). Searches PrintedName/Name on Cards, plus all face names on CardFaces for double-faced cards.</summary>
    public string? NameFilter { get; init; }

    /// <summary>Card type filters — multiple values combined as OR expression (item1 OR item2 OR ...). Applied via LIKE.</summary>
    public List<string>? TypeFilters { get; init; }

    /// <summary>Rarity filters — multiple values combined as OR expression (common OR uncommon OR ...). Applied via exact match.</summary>
    public List<string>? Rarity { get; init; }

    /// <summary>Artist name filter. Applied via LIKE (case-insensitive).</summary>
    public string? Artist { get; init; }

    /// <summary>Color type filter (e.g. "monoW", "multicolor"). Exact match. Legacy — use ColorFilter instead.</summary>
    public string? ColorType { get; init; }

    /// <summary>Bitmask-based color filter. Covers all 12-checkbox combinations.
    /// When set, takes precedence over ColorType.</summary>
    public ColorFilter? ColorFilter { get; init; }

    /// <summary>Numeric property filter for Power. Applied via CAST/real comparison.</summary>
    public NumericPropertyFilter? PowerFilter { get; init; }

    /// <summary>Numeric property filter for Toughness. Applied via CAST/real comparison.</summary>
    public NumericPropertyFilter? ToughnessFilter { get; init; }

    /// <summary>Numeric property filter for CMC. Applied via integer comparison.</summary>
    public NumericPropertyFilter? CmcFilter { get; init; }

    /// <summary>Layout filter (e.g. "normal", "split", "transform"). Exact match on Layout column.</summary>
    public string? LayoutFilter { get; init; }

    /// <summary>Legality format filter (e.g. "Standard", "Modern"). Filters via Legal column LIKE match.</summary>
    public string? LegalityFilter { get; init; }

    /// <summary>When true, filters only cards where GameChanger = 1.</summary>
    public bool GameChangerFilter { get; init; }

    // ── Collection filters — applied only when LocationFilter is set ──

    /// <summary>Ownership filter: "all" | "owned" | "borrowed". "all"/null = no filter. Maps to Deck.IsOwn.</summary>
    public string? Ownership { get; init; }

    /// <summary>Minimum card count in the location (Deck.Count &gt;= value). Null = inactive.</summary>
    public int? MinCount { get; init; }

    /// <summary>Maximum card count in the location (Deck.Count &lt;= value). Null = inactive.</summary>
    public int? MaxCount { get; init; }

    /// <summary>Minimum price in USD (CAST(Cards.Usd AS REAL) &gt;= value). Null = inactive.</summary>
    public float? MinPrice { get; init; }

    /// <summary>Maximum price in USD (CAST(Cards.Usd AS REAL) &lt;= value). Null = inactive.</summary>
    public float? MaxPrice { get; init; }

    /// <summary>Foil filter — carried to the DB layer, intentionally not used in SQL yet (no foil flag column exists).</summary>
    public bool IsFoil { get; init; }

    /// <summary>Only cards marked For Trade (Deck.ForTradeCount IS NOT NULL AND &gt; 0). Requires LocationFilter.</summary>
    public bool HasForTrade { get; init; }

    /// <summary>Only cards with a non-empty comment (Deck.Comment). Requires LocationFilter.</summary>
    public bool HasComment { get; init; }

    public int Offset => (Page - 1) * PageSize;

    /// Valid sort column names in the Cards table.
    public static class SortFields
    {
        public const string Name = "Name";
        public const string CmcNumber = "CmcNumber";
        public const string Type = "Type";
        public const string SetCode = "SetCode";
        public const string Rarity = "Rarity";
    }
}

/// <summary>
/// Result of a paginated card list query.
/// </summary>
/// <param name="LocationNames">Locations multi-chip mode only: the names of the selected nodes each
/// displayed card lives in, comma-joined (a card in two decks shows both). One GROUP BY over Deck
/// joined to Collection for the page ids. Null outside that mode.</param>
/// <param name="Truncated">True when the FTS hit set hit the top-5000 working-set cap — the UI
/// shows "5000+" instead of pretending the count is exact. False outside search.</param>
public record CardListResult(
    ICollection<Card> Cards,
    int TotalCount,
    IReadOnlyDictionary<Guid, string>? LocationNames = null,
    bool Truncated = false);

/// <summary>
/// Numeric property filter (Power / Toughness / CMC).
/// Operator: ">=", "=", "<=". Value: nullable double.
/// When Value is null, the filter is inactive and generates no SQL clause.
///
/// Uses pre-computed NUMBER columns (PowerNumber, ToughnessNumber, CmcNumber) which are INTEGER.
/// This avoids CAST(TEXT AS REAL) on every row and enables index usage.
/// </summary>
public sealed class NumericPropertyFilter
{
    /// <summary>Comparison operator: ">=", "=", "<=".</summary>
    public string Operator { get; init; } = ">=";

    /// <summary>Filter value. Null = inactive filter (empty input field).</summary>
    public double? Value { get; init; }

    /// <summary>True if the filter is active (Value is set).</summary>
    public bool IsActive => Value.HasValue;

    /// <summary>
    /// Builds the SQL WHERE clause fragment that includes DFC support.
    /// Uses pre-computed NUMBER columns (PowerNumber, ToughnessNumber, CmcNumber) which are INTEGER.
    /// Returns a condition that matches cards where EITHER the parent card's number value matches
    /// (normal cards) OR any face's number value matches (double-faced cards).
    /// </summary>
    public string? ToSqlWhereWithDfcSupport(string cardNumberColumn, string faceNumberColumn)
    {
        if (!IsActive) return null;

        var intVal = (int)Math.Floor(Value!.Value);

        var cardCondition = BuildCondition(cardNumberColumn, intVal);
        var faceCondition = BuildCondition(faceNumberColumn, intVal);

        // Cards where number value matches directly, OR cards with NULL parent number (DFC) that have at least one matching face.
        // Uses EXISTS with correlated subquery: only checks CardFaces for the current card's Id.
        // Outer parentheses are MANDATORY: the fragment's top level is an OR, and GetPagedAsync
        // joins whereParts with AND — without them SQL precedence re-groups the whole WHERE as
        // "(otherFilters AND cardCondition) OR faceSubquery", leaking the face branch past every
        // other filter (set, rarity, name, location...).
        return $"(({cardCondition}) OR ({cardNumberColumn} IS NULL AND EXISTS (SELECT 1 FROM CardFaces cf WHERE cf.CardId = Cards.Id AND {faceCondition})))";
    }

    private string BuildCondition(string col, int value)
    {
        var op = Operator switch
        {
            ">=" => ">=",
            "<=" => "<=",
            _    => "=",
        };

        return $"{col} {op} {value}";
    }
}
