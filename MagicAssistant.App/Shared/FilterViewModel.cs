using System.Collections.Generic;
using MagicAssistant.Data.Model;

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
/// KBCardsListControl subscribes to Changed event and re-queries.
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

    // ── Filter properties ──

    /// <summary>Set code filters — multiple values combined as OR expression (set1 OR set2 OR ...).
    /// External components should use <see cref="SetNewSetCodeFilter(string)" /> instead of mutating directly.</summary>
    public List<string> SetCodes { get; set; } = new();

    /// <summary>Rarity filters — multiple values combined as OR expression (common OR uncommon OR ...).
    /// External components should use <see cref="SetNewRarityFilter(string)" /> instead of mutating directly.</summary>
    public List<string> Rarity { get; set; } = new();

    /// <summary>Name filter (debounced in KBCardsListControl, 3+ chars).</summary>
    public string NameFilter { get; set; } = string.Empty;

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

    // ── Sort properties ──

    public string SortField { get; set; } = "Name";

    public bool SortAsc { get; set; } = true;

    /// <summary>Peresozdaet SetCodes как новый List&lt;string&gt; с одним значением.
    /// Если строка пустая — создаёт пустой список.
    /// Это единственный способ изменить фильтр сетов из внешних компонентов (тулбар/сайдбар).</summary>
    /// <param name="value">Set code string, or empty/whitespace to clear the list.</param>
    public void SetNewSetCodeFilter(string? value)
    {
        SetCodes = string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : new List<string> { value.Trim() };
    }

    /// <summary>Replace SetCodes with a collection of values.</summary>
    public void SetSetCodes(IEnumerable<string> values)
    {
        SetCodes = new List<string>(values);
    }

    /// <summary>Превращает текущий список сетов в выражение "set1 OR set2 OR ...".
    /// Используется при построении SQL-запроса через CardListFilter.SetCodes.</summary>
    public string GetSetCodesOrExpression()
    {
        return string.Join(" OR ", SetCodes.Where(s => !string.IsNullOrWhiteSpace(s)));
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
        SetCodes = new List<string>();
        Rarity = new List<string>();
        NameFilter = string.Empty;
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
    }
}
