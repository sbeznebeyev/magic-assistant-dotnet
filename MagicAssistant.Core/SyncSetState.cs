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

    /// <summary>
    /// Phase-weighted overall progress (0-100). Each phase owns its own band, which keeps the bar
    /// monotonic even though ReadyCardsToSave is re-counted from zero during mapping: download
    /// 0-35, mapping 35-70, images 70-95, saving pinned at 95 (one bulk write, no finer signal),
    /// done 100. The old cards-only formula sat at 100% for the entire image phase.
    /// </summary>
    public double ProgressPercent
    {
        get
        {
            lock (_lock)
            {
                if (Completed) return 100;
                if (TotalCards <= 0) return 0;
                return Phase switch
                {
                    0 => 0,   // resolving the set code
                    1 => Math.Min(ReadyCardsToSave * 35.0 / TotalCards, 35),
                    2 => 35 + Math.Min(ReadyCardsToSave * 35.0 / TotalCards, 35),
                    // Skipped-existing images count into ImagesDownloaded, so ok+fail reaches TotalCards.
                    3 => 70 + Math.Min((ImagesDownloaded + ImagesFailed) * 25.0 / TotalCards, 25),
                    4 => 95,
                    _ => 100,
                };
            }
        }
    }

    /// <summary>
    /// Whole-percent bar width for the inline style — the CreaturePage.BarWidthPct pattern. An int
    /// never carries a decimal separator, so a comma culture cannot corrupt the CSS: the raw double
    /// renders as "70,5" under ru-RU, the browser drops the invalid width declaration, and the bar
    /// then sits at its default full width — the "always 100%" symptom, independent of the formula.
    /// </summary>
    public int ProgressWidthPct => (int)Math.Round(ProgressPercent);

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
