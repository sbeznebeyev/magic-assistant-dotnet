namespace MagicAssistant.App.Shared;

/// <summary>
/// Non-generic base class for display in UI components.
/// Exposes DisplayValue so the component can iterate without knowing TKey/TItem.
/// </summary>
public abstract class MultiSelectItemBase
{
    public string DisplayValue { get; }

    protected MultiSelectItemBase(string displayValue)
    {
        DisplayValue = displayValue;
    }
}

/// <summary>
/// Generic item for MultiSelectList. Holds a strongly-typed key, the original object, and a display string.
/// </summary>
/// <typeparam name="TKey">Unique identifier type (Guid, int, string, etc.).</typeparam>
/// <typeparam name="TItem">Original object type.</typeparam>
public class MultiSelectItem<TKey, TItem> : MultiSelectItemBase
    where TKey : notnull
{
    public TKey Key { get; }
    public TItem Item { get; }

    public MultiSelectItem(TKey key, TItem item, string displayValue)
        : base(displayValue)
    {
        Key = key;
        Item = item;
    }
}
