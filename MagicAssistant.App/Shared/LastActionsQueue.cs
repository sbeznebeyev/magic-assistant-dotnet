namespace MagicAssistant.App.Shared;

/// <summary>
/// In-memory fixed-capacity queue of the last card actions.
/// New items are appended to the tail; when the capacity (<see cref="QueueLength"/>) is exceeded,
/// the oldest item is evicted from the head. Re-enqueueing an existing (LocationId, Card.Id) pair
/// replaces that entry with the new item at the tail (no duplicate rows).
/// Uses a standard C# event (not EventCallback) because subscribers are components — same pattern as FilterViewModel.
/// </summary>
public class LastActionsQueue
{
    private readonly List<LastActionItem> _items = new();

    /// <summary>Fixed capacity of the queue (N). Configurable.</summary>
    public int QueueLength { get; set; } = 3;

    /// <summary>Items in queue order: oldest first, newest last.</summary>
    public IReadOnlyList<LastActionItem> Items => _items;

    /// <summary>Raised after the queue contents change.</summary>
    public event Action? Changed;

    /// <summary>Appends an item to the tail, evicting the oldest item if capacity is exceeded.
    /// If an entry with the same (LocationId, Card.Id) pair already exists, it is replaced by the
    /// incoming item at the tail — so the row always shows the latest operation (Add/Sub) and count;
    /// the total stays live via the grouped counts query in LastActions.</summary>
    public void Enqueue(LastActionItem item)
    {
        _items.RemoveAll(i => i.LocationId == item.LocationId && i.Card.Id == item.Card.Id);
        _items.Add(item);
        while (_items.Count > QueueLength)
        {
            _items.RemoveAt(0);
        }

        Changed?.Invoke();
    }

    /// <summary>Removes all items. No-op (no event) when the queue is already empty.</summary>
    public void Clear()
    {
        if (_items.Count == 0) return;

        _items.Clear();
        Changed?.Invoke();
    }
}
