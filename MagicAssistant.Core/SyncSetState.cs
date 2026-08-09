namespace MagicAssistant.Core;

/// <summary>
/// Thread-safe per-set sync state tracker.
/// Used by SyncKBCommand to report progress, and by polling endpoint for UI.
/// Lives in Core so it can be shared between command handler and query handler.
/// </summary>
public sealed class SyncSetState
{
    private readonly object _lock = new();

    public string SetCode { get; }
    public string? SetName { get; set; }

    // === Download progress ===
    /// <summary>Number of pages downloaded from Scryfall during Phase 1.</summary>
    public int DownloadedPages { get; set; }

    // === Card progress ===
    /// <summary>Total cards in the set (known from Scryfall).</summary>
    public int TotalCards { get; set; }

    /// <summary>Cards that have full data downloaded (description + images) and are ready to save.</summary>
    public int ReadyCardsToSave
    {
        get => _readyCardsToSave;
        set
        {
            lock (_lock)
            {
                if (value > TotalCards && TotalCards > 0)
                    value = TotalCards;
                _readyCardsToSave = value;
            }
        }
    }
    private int _readyCardsToSave;

    // === Image download progress ===
    public int ImagesDownloaded { get; set; }
    public int ImagesFailed { get; set; }

    // === Phase tracking ===
    public int Phase { get; set; }          // 0=resolve, 1=download, 2=map, 3=images, 4=save, 5=done
    public string PhaseName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    // === Completion ===
    public bool Completed { get; set; }
    public bool Failed { get; set; }
    public string? ErrorMessage { get; set; }

    // === Timing ===
    public DateTimeOffset StartedAt { get; set; }
    public TimeSpan Elapsed => Completed || Failed ? DateTimeOffset.UtcNow - StartedAt : default;

    /// <summary>Percentage of cards ready to save (0-100).</summary>
    public double ProgressPercent
    {
        get
        {
            lock (_lock)
            {
                if (TotalCards <= 0) return 0;
                return Math.Min(ReadyCardsToSave * 100.0 / TotalCards, 100);
            }
        }
    }

    public SyncSetState(string setCode)
    {
        SetCode = setCode;
        StartedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Singleton registry of active sync states, keyed by set abbreviation.
/// </summary>
public sealed class SyncSetStateRegistry
{
    private readonly Dictionary<string, SyncSetState> _states = new();
    private readonly object _lock = new();

    public SyncSetState? Get(string setAbbreviation)
    {
        lock (_lock)
            return _states.TryGetValue(setAbbreviation, out var state) ? state : null;
    }

    public void Register(SyncSetState state)
    {
        lock (_lock)
            _states[state.SetCode] = state;
    }

    public void Remove(string setAbbreviation)
    {
        lock (_lock)
            _states.Remove(setAbbreviation);
    }

    /// <summary>
    /// Returns snapshot of all active states for debugging/monitoring.
    /// </summary>
    public ICollection<SyncSetState> GetAllActive()
    {
        lock (_lock)
            return _states.Values.ToList();
    }
}
