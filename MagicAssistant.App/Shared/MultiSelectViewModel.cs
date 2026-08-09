using System.Collections.ObjectModel;

namespace MagicAssistant.App.Shared;

/// <summary>
/// Generic view model for MultiSelectList. Holds a collection of typed items
/// and notifies subscribers via the Changed event when the collection mutates.
///
/// Usage follows the same POCO-event pattern as FilterViewModel:
/// consumers (Blazor components) wrap async work via InvokeAsync.
/// </summary>
/// <typeparam name="TKey">Unique identifier type (Guid, int, string, etc.).</typeparam>
/// <typeparam name="TItem">Original object type — can be anything.</typeparam>
public class MultiSelectViewModel<TKey, TItem>
    where TKey : notnull
{
    /// <summary>Fired when the collection changes — subscribers should re-render.</summary>
    public event Action? Changed;

    private readonly ObservableCollection<MultiSelectItemBase> _items = new();

    /// <summary>Read-only view of selected items. Safe for foreach iteration in Razor.</summary>
    public IReadOnlyList<MultiSelectItemBase> Items => _items;

    /// <summary>Number of selected items.</summary>
    public int Count => _items.Count;

    /// <summary>True when no items are selected.</summary>
    public bool IsEmpty => _items.Count == 0;

    /// <summary>
    /// Initialize or replace the entire selection from a collection of (key, item) pairs.
    /// Existing items are cleared first. Duplicates within the input are deduplicated by key.
    /// </summary>
    /// <param name="items">Collection of (Key, Item) tuples.</param>
    /// <param name="getDisplayValue">Function that extracts the display string from each item.</param>
    public void Initialize(IEnumerable<(TKey Key, TItem Item)> items, Func<TItem, string> getDisplayValue)
    {
        _items.Clear();
        var seenKeys = new HashSet<TKey>();

        foreach (var (key, item) in items)
        {
            if (item is null)
                continue;
            if (!seenKeys.Add(key))
                continue; // duplicate key — skip
            _items.Add(new MultiSelectItem<TKey, TItem>(key, item, getDisplayValue(item)));
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Initialize or replace the entire selection from a dictionary.
    /// Existing items are cleared first. Dictionary keys become item keys.
    /// </summary>
    /// <param name="dictionary">Dictionary where Key = item key, Value = item object.</param>
    /// <param name="getDisplayValue">Function that extracts the display string from each item.</param>
    public void Initialize(IDictionary<TKey, TItem> dictionary, Func<TItem, string> getDisplayValue)
    {
        _items.Clear();

        foreach (var kvp in dictionary)
        {
            if (kvp.Value is null)
                continue;
            _items.Add(new MultiSelectItem<TKey, TItem>(kvp.Key, kvp.Value, getDisplayValue(kvp.Value)));
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Initialize or replace the entire selection from a simple collection of items.
    /// A key selector function is required to extract unique keys.
    /// Existing items are cleared first. Duplicates within the input are deduplicated by key.
    /// </summary>
    /// <param name="items">Collection of item objects.</param>
    /// <param name="getKey">Function that extracts a unique key from each item.</param>
    /// <param name="getDisplayValue">Function that extracts the display string from each item.</param>
    public void Initialize(IEnumerable<TItem> items, Func<TItem, TKey> getKey, Func<TItem, string> getDisplayValue)
    {
        _items.Clear();
        var seenKeys = new HashSet<TKey>();

        foreach (var item in items)
        {
            if (item is null)
                continue;
            var key = getKey(item);
            if (!seenKeys.Add(key))
                continue; // duplicate key — skip
            _items.Add(new MultiSelectItem<TKey, TItem>(key, item, getDisplayValue(item)));
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Add a new item to the selection.
    /// Does nothing if an item with the same key already exists.
    /// </summary>
    /// <param name="key">Unique identifier.</param>
    /// <param name="item">Original object instance.</param>
    /// <param name="getDisplayValue">Function that extracts the display string from the item.</param>
    public void AddNewItem(TKey key, TItem item, Func<TItem, string> getDisplayValue)
    {
        if (item is null)
            return;
        if (_items.Any(i => i is MultiSelectItem<TKey, TItem> typed && Equals(typed.Key, key)))
            return; // duplicate

        _items.Add(new MultiSelectItem<TKey, TItem>(key, item, getDisplayValue(item)));
        Changed?.Invoke();
    }

    /// <summary>Remove the item with the given display value (matched by reference equality on the base).</summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool RemoveItem(MultiSelectItemBase item)
    {
        bool removed = _items.Remove(item);
        if (removed)
            Changed?.Invoke();
        return removed;
    }

    /// <summary>Remove the item identified by its key.</summary>
    /// <param name="key">Key to match.</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool RemoveByKey(TKey key)
    {
        var item = _items.FirstOrDefault(i =>
            i is MultiSelectItem<TKey, TItem> typed && Equals(typed.Key, key));
        if (item is null)
            return false;
        bool removed = _items.Remove(item);
        if (removed)
            Changed?.Invoke();
        return removed;
    }

    /// <summary>Clear all selected items.</summary>
    public void Clear()
    {
        if (_items.Count == 0)
            return;
        _items.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// Get the strongly-typed item by its key. Returns default(TItem) if not found.
    /// </summary>
    public TItem GetItemByKey(TKey key)
    {
        foreach (var item in _items)
        {
            if (item is MultiSelectItem<TKey, TItem> typed && Equals(typed.Key, key))
                return typed.Item;
        }
        return default!;
    }

    /// <summary>
    /// Get all strongly-typed original objects.
    /// </summary>
    public IReadOnlyList<TItem> GetAllItems()
    {
        return _items
            .OfType<MultiSelectItem<TKey, TItem>>()
            .Select(i => i.Item)
            .ToList();
    }
}
