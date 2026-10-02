using System.Collections.Generic;
using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.App.Shared;

/// <summary>
/// Canonical ColorBitFlags for the 12 color filter checkboxes.
/// Base colors + color types used for filtering. Modifiers (And/Only/Identity) are separate booleans.
/// </summary>
public static class ColorFilterFlags
{
    /// <summary>All 9 filterable color/category flags.</summary>
    public static readonly ColorBitFlags[] All =
    {
        ColorBitFlags.C,
        ColorBitFlags.W,
        ColorBitFlags.U,
        ColorBitFlags.B,
        ColorBitFlags.R,
        ColorBitFlags.G,
        ColorBitFlags.MULTI,
        ColorBitFlags.MONO,
        ColorBitFlags.HYBRID,
    };
}

/// <summary>
/// Central view model for KB card list filtering and sorting.
/// All filter/sort UI components mutate this single instance.
/// CardsListControl subscribes to Changed event and re-queries.
/// CardFilterDialog is the central control interface for full filter management.
///
/// Note: Uses standard C# event (Action) — not EventCallback, because
/// FilterViewModel is a POCO, not a Blazor component. EventCallback requires
/// an invocation context from a component parameter, which a plain class cannot provide.
/// Subscribers (Blazor components) wrap the async work via InvokeAsync.
/// </summary>
public class FilterViewModel
{
    public FilterViewModel()
    {
        foreach (var flag in ColorFilterFlags.All)
            ColorFilters[flag] = false;
    }
    /// <summary>Fired when Apply() is called — subscribers should re-query.</summary>
    public event Action? Changed;

    /// <summary>Increments on every Clear() call — subscribers use it to detect an external reset
    /// and sync their local input state (e.g. the debounced name-filter buffer in CardsListControl).</summary>
    public int ClearGeneration { get; private set; }

    // ── Filter properties ──

    /// <summary>Scope of the displayed set list (FilteredCardSetsCollection). Empty = all sets in the KB.
    /// Written only by location-mode auto-fill (the node's card set codes) and by <see cref="Clear" />;
    /// set clicks and the sidebar search text do NOT touch it. Use <see cref="SetFilteredSetCodes" />.</summary>
    public List<string> FilteredSetCodes { get; set; } = new();

    /// <summary>Set selection made by clicking a set in the sidebar. Null = no selection —
    /// the card filter then falls back to <see cref="FilteredSetCodes" />.
    /// A click sets exactly one code; ALL / ✕ / <see cref="Clear" /> reset it to null.</summary>
    public List<string>? ActiveCardSets { get; set; }

    /// <summary>Text from the "Filter sets..." sidebar input. Narrows only the visible set list in
    /// CardsListControl's sidebar — the card grid follows the explicit single selection, not the search text.
    /// The property persists across component remounts (e.g. deck → Knowledge Base), so an active set search
    /// survives navigation. Reset by <see cref="Clear()" />.</summary>
    public string SetSearchText { get; set; } = string.Empty;

    /// <summary>Rarity filters — multiple values combined as OR expression (common OR uncommon OR ...).
    /// External components should use <see cref="SetNewRarityFilter(string)" /> instead of mutating directly.</summary>
    public List<string> Rarity { get; set; } = new();

    /// <summary>Name filter (debounced in CardsListControl, 3+ chars).</summary>
    public string NameFilter { get; set; } = string.Empty;

    /// <summary>FTS5 full-text search query (raw FTS syntax: "phrase", name:term, OR/AND/NOT). Empty = inactive.</summary>
    public string SearchQuery { get; set; } = string.Empty;

    /// <summary>Card type filters — multiple values combined as OR expression (item1 OR item2 OR ...).</summary>
    /// <remarks>External components should use <see cref="SetNewTypeFilter(string)" /> and
    /// <see cref="SetTypeFilters(IEnumerable{string})" /> instead of mutating this property directly.</remarks>
    public List<string> TypeFilter { get; set; } = new();

    public string Artist { get; set; } = string.Empty;

    public string ColorType { get; set; } = string.Empty;

    /// <summary>Color filter checkboxes keyed by <see cref="ColorBitFlags" />.
    /// CardFilterDialog mutates this dictionary; ColorFilterHelper.GetFilter() consumes it.</summary>
    public Dictionary<ColorBitFlags, bool> ColorFilters { get; set; } = new();

    /// <summary>And modifier: all selected colors must be present simultaneously.</summary>
    public bool ColorAndMode { get; set; }

    /// <summary>Only modifier: exclude all unselected colors.</summary>
    public bool ColorOnlyMode { get; set; }

    /// <summary>Identity modifier: filter by color identity (cost + oracle text) instead of mana cost.
    /// Automatically implies Only behavior.</summary>
    public bool ColorIdentityMode { get; set; }

    // ── Properties filters (Power / Toughness / CMC) ──

    /// <summary>Power filter operator: ">=", "=", "<=".</summary>
    public string PowerOp { get; set; } = ">=";

    /// <summary>Power filter value. Empty string = inactive filter.</summary>
    public string PowerValue { get; set; } = string.Empty;

    /// <summary>Toughness filter operator: ">=", "=", "<=".</summary>
    public string ToughnessOp { get; set; } = ">=";

    /// <summary>Toughness filter value. Empty string = inactive filter.</summary>
    public string ToughnessValue { get; set; } = string.Empty;

    /// <summary>CMC filter operator: ">=", "=", "<=".</summary>
    public string CmcOp { get; set; } = ">=";

    /// <summary>CMC filter value. Empty string = inactive filter.</summary>
    public string CmcValue { get; set; } = string.Empty;

    // ── Layout filter ──

    /// <summary>Selected layout filter value. Empty string = no filter (show all layouts).</summary>
    public string LayoutFilter { get; set; } = string.Empty;

    // ── Legality filter ──

    /// <summary>Selected legality format filter value. Empty string = no filter (show all formats).</summary>
    public string LegalityFilter { get; set; } = string.Empty;

    // ── GameChanger filter ──

    /// <summary>When true, filters only Game Changer cards (Commander banned list). False = no filter.</summary>
    public bool GameChangerFilter { get; set; }

    // ── Collection filters (active only when CurrentCollectionId is set) ──

    /// <summary>Ownership filter: "all" | "owned" | "borrowed". Default "all" = no filter.</summary>
    public string Ownership { get; set; } = "all";

    /// <summary>Minimum card count in the location (Deck.Count). Parsed at query time. Empty string = inactive.</summary>
    public string MinCount { get; set; } = string.Empty;

    /// <summary>Maximum card count in the location (Deck.Count). Parsed at query time. Empty string = inactive.</summary>
    public string MaxCount { get; set; } = string.Empty;

    /// <summary>Minimum price in USD (Cards.Usd). Parsed at query time. Empty string = inactive.</summary>
    public string MinPrice { get; set; } = string.Empty;

    /// <summary>Maximum price in USD (Cards.Usd). Parsed at query time. Empty string = inactive.</summary>
    public string MaxPrice { get; set; } = string.Empty;

    /// <summary>Foil filter — carried to the DB layer, not used in SQL yet (no foil flag column exists).</summary>
    public bool IsFoil { get; set; }

    /// <summary>Only cards marked For Trade (Deck.ForTradeCount &gt; 0).</summary>
    public bool HasForTrade { get; set; }

    /// <summary>Only cards with a non-empty comment (Deck.Comment).</summary>
    public bool HasComment { get; set; }

    // ── Current collection (navigation state) ──

    /// <summary>Collection node Id currently displayed in the card list (deck/sideboard/collection/folder).
    /// Navigation state, not a filter — intentionally excluded from Clear(). Null = Knowledge Base view.</summary>
    public int? CurrentCollectionId { get; set; }

    /// <summary>Node type of <see cref="CurrentCollectionId"/> (Collection/Deck/Sideboard — folders never select).
    /// Navigation state, not a filter — intentionally excluded from Clear(). Null = Knowledge Base view.</summary>
    public NodeType? CurrentCollectionNodeType { get; set; }

    // ── Sort properties ──

    public string SortField { get; set; } = "Name";

    public bool SortAsc { get; set; } = true;

    /// <summary>Replace the set-list scope with a normalized collection of codes (trimmed, distinct).
    /// Null/empty = all sets in the KB.</summary>
    public void SetFilteredSetCodes(IEnumerable<string>? codes)
    {
        FilteredSetCodes = codes is null
            ? new List<string>()
            : codes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToList();
    }

    /// <summary>Set the clicked-set selection. Null/empty clears it (back to the scope filter).</summary>
    public void SetActiveCardSets(string? setCode)
    {
        ActiveCardSets = string.IsNullOrWhiteSpace(setCode)
            ? null
            : new List<string> { setCode.Trim() };
    }

    /// <summary>Effective card-grid set filter: the clicked selection wins over the scope.
    /// Null = no set filtering at all.</summary>
    public List<string>? GetEffectiveSetCodes()
    {
        var codes = ActiveCardSets ?? FilteredSetCodes;
        return codes.Count > 0 ? codes : null;
    }

    /// <summary>Peresozdaet TypeFilter как новый List&lt;string&gt; с одним значением.
    /// Если строка пустая — создаёт пустой список.
    /// Это единственный способ изменить тип фильтра из внешних компонентов.</summary>
    /// <param name="value">Card type string, or empty/whitespace to clear the list.</param>
    public void SetNewTypeFilter(string? value)
    {
        TypeFilter = string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : new List<string> { value.Trim() };
    }

    /// <summary>Replace TypeFilter with a collection of values.</summary>
    public void SetTypeFilters(IEnumerable<string> values)
    {
        TypeFilter = new List<string>(values);
    }

    /// <summary>Превращает текущий список типов в выражение "item1 OR item2 OR ...".
    /// Используется при построении SQL-запроса через CardListFilter.TypeFilters.</summary>
    public string GetTypesOrExpression()
    {
        return string.Join(" OR ", TypeFilter.Where(t => !string.IsNullOrWhiteSpace(t)));
    }

    /// <summary>Peresozdaet Rarity как новый List&lt;string&gt; с одним значением.
    /// Если строка пустая — создаёт пустой список.
    /// Это единственный способ изменить редкость фильтра из внешних компонентов.</summary>
    /// <param name="value">Rarity string (common/uncommon/rare/mythic), or empty/whitespace to clear.</param>
    public void SetNewRarityFilter(string? value)
    {
        Rarity = string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : new List<string> { value.Trim() };
    }

    /// <summary>Превращает текущий список редкостей в выражение "common OR uncommon OR ...".
    /// Используется при построении SQL-запроса через CardListFilter.Rarity.</summary>
    public string GetRarityOrExpression()
    {
        return string.Join(" OR ", Rarity.Where(r => !string.IsNullOrWhiteSpace(r)));
    }

    /// <summary>Notify all subscribers to re-query with current filter/sort state.</summary>
    public void Apply() => Changed?.Invoke();

    /// <summary>Reset all filters and sort to defaults, then notify.</summary>
    public void ClearAndApply()
    {
        Clear();
        Apply();
    }

    /// <summary>Reset all filters and sort to defaults without notifying.</summary>
    public void Clear()
    {
        ClearGeneration++;
        FilteredSetCodes = new List<string>();
        ActiveCardSets = null;
        SetSearchText = string.Empty;
        Rarity = new List<string>();
        NameFilter = string.Empty;
        SearchQuery = string.Empty;
        TypeFilter = new List<string>();
        Artist = string.Empty;
        ColorType = string.Empty;
        SortField = "Name";
        SortAsc = true;
        ResetColorFilters();
    }

    /// <summary>Reset all color filter checkboxes and modifiers to defaults.</summary>
    private void ResetColorFilters()
    {
        ColorFilters.Clear();
        foreach (var flag in ColorFilterFlags.All)
            ColorFilters[flag] = false;
        ColorAndMode = false;
        ColorOnlyMode = false;
        ColorIdentityMode = false;

        // Properties filters
        PowerOp = ">=";
        PowerValue = string.Empty;
        ToughnessOp = ">=";
        ToughnessValue = string.Empty;
        CmcOp = ">=";
        CmcValue = string.Empty;

        // Layout / Legality / GameChanger filters
        LayoutFilter = string.Empty;
        LegalityFilter = string.Empty;
        GameChangerFilter = false;

        // Collection filters (location mode)
        Ownership = "all";
        MinCount = string.Empty;
        MaxCount = string.Empty;
        MinPrice = string.Empty;
        MaxPrice = string.Empty;
        IsFoil = false;
        HasForTrade = false;
        HasComment = false;
    }
}
