namespace MagicAssistant.Data.Model;

public class CardSet : IEquatable<CardSet>
{
    private static readonly CardSet Unknown = new(Guid.NewGuid(), "???", "Unknown Set");
    private static int _idCounter;

    // ── Primary constructor (from Scryfall data) ──────────────

    public CardSet(
        Guid id,
        string setCode,
        string name,
        string? uri = null,
        string? scryfallUri = null,
        string? searchUri = null,
        string? setType = null,
        int cardCount = 0,
        string? parentSetCode = null,
        bool digital = false,
        bool nonfoilOnly = false,
        bool foilOnly = false,
        string? iconSvgUri = null,
        DateTime? releaseDate = null)
    {
        Id = id;
        SetCode = setCode?.ToLowerInvariant() ?? "???";
        Name = name;
        Uri = uri;
        ScryfallUri = scryfallUri;
        SearchUri = searchUri;
        SetType = setType ?? "?";
        CardCount = cardCount;
        ParentSetCode = parentSetCode;
        Digital = digital;
        NonfoilOnly = nonfoilOnly;
        FoilOnly = foilOnly;
        IconSvgUri = iconSvgUri;
        ReleaseDate = releaseDate;
    }

    // ── Legacy constructor (backward compat for tests) ───────
    // block and type are ignored; use setType via the primary constructor instead.

    public CardSet(string setCode, string name, string? block, string? type): this(Guid.NewGuid(), setCode, name, setType: type ?? "?")
    {
    }

    // ── Properties ────────────────────────────────────────────

    public static CardSet UnknownEdition => Unknown;

    /// <summary>Lowercase set code</summary>
    public string SetCode { get; private set; }

    /// <summary>Display name (e.g. "Wilds of Eldraine Promos").</summary>
    public string Name { get; }

    /// <summary>Scryfall API URI for this set.</summary>
    public string? Uri { get; }

    /// <summary>Human-readable Scryfall page URI.</summary>
    public string? ScryfallUri { get; }

    /// <summary>Search URI for cards in this set.</summary>
    public string? SearchUri { get; }

    /// <summary>Raw set type from Scryfall (e.g. "promo", "core", "expansion").</summary>
    public string SetType { get; private set; } = "?";

    /// <summary>Total number of cards in this set.</summary>
    public int CardCount { get; }

    /// <summary>Parent set code (e.g. "woe" for promos).</summary>
    public string? ParentSetCode { get; }

    /// <summary>Whether this is a digital-only set.</summary>
    public bool Digital { get; }

    /// <summary>Whether this set has only nonfoil cards.</summary>
    public bool NonfoilOnly { get; }

    /// <summary>Whether this set has only foil cards.</summary>
    public bool FoilOnly { get; }

    /// <summary>Icon SVG URI for this set.</summary>
    public string? IconSvgUri { get; }

    /// <summary>Release date of the set.</summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>Scryfall ID of this set.</summary>
    public Guid Id { get; }

    // ── Computed helpers ──────────────────────────────────────

    public bool IsUnknown => this == Unknown || SetCode == "???";

    /// <summary>Alias for IsHidden — kept for backward compatibility.</summary>
    [System.Obsolete("Use the SetType-based hidden check instead")]
    public bool IsHidden => SetType.StartsWith("--");

    // ── Methods ───────────────────────────────────────────────

    /// <summary>
    /// Checks if this edition is referenced by any cards in the knowledge base.
    /// Uses EditionsInUseRegistry populated by Core layer during reconciliation.
    /// Mirrors Java Edition.isUsedByPrintings().
    /// </summary>
    public bool IsUsedByPrintings()
    {
        // Fast path: check if registry is available
        if (EditionsInUseRegistry != null)
            return EditionsInUseRegistry.Contains(SetCode);

        // Fallback: unknown editions are never "used"
        return !IsUnknown;
    }

    /// <summary>
    /// Set of set abbreviations that have at least one card in the KB.
    /// Populated by Core layer (KnowledgeBaseSync).
    /// </summary>
    public static HashSet<string>? EditionsInUseRegistry { get; set; }

    // === Equality ===
    public override string ToString() => $"{SetCode} - {Name}";

    public bool Equals(CardSet? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        // Different sets can share the same display name (reprints, etc).
        return SetCode == other.SetCode;
    }

    public override bool Equals(object? obj) => Equals(obj as CardSet);
}
