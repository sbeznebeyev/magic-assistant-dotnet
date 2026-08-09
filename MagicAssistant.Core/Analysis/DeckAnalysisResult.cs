namespace MagicAssistant.Core.Analysis;

/// <summary>
/// Result of a full deck analysis — single-pass computation.
/// Mirrors Java CardStoreUtils + all aggr classes combined into one efficient result.
/// </summary>
public class DeckAnalysisResult
{
    // === Basic Stats ===
    public int TotalCards { get; set; } = 0;
    public int TotalManaCost { get; set; } = 0;
    public int MaxRepeats { get; set; } = 0;

    // === Mana Curve (CMC distribution, 0-7+) ===
    /// <summary>
    /// Index 0 = CMC 0, index 7 = CMC 7+. Non-land cards only.
    /// </summary>
    public int[] ManaCurve { get; set; } = new int[8];

    // === Color Distribution (non-land cards) ===
    /// <summary>
    /// Color → count mapping. Keys: "W", "U", "B", "R", "G", "Colorless"
    /// </summary>
    public Dictionary<string, int> ColorDistribution { get; set; } = new();

    // === Type Distribution ===
    public int LandCount { get; set; } = 0;
    public int CreatureCount { get; set; } = 0;
    public int NonCreatureCount { get; set; } = 0;

    // === Creature Subtypes ===
    /// <summary>
    /// Creature subtype → count of cards with that subtype.
    /// </summary>
    public Dictionary<string, int> CreatureSubtypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // === Ability Stats (keywords only) ===
    /// <summary>
    /// Ability name → count of cards with that ability.
    /// </summary>
    public Dictionary<string, int> AbilityStats { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // === Deck Colors (combined unique colors from all non-land cards) ===
    public string DeckColors => ComputeDeckColors();

    private string ComputeDeckColors()
    {
        if (!ColorDistribution.Any()) return "";
        var sorted = ColorDistribution.Keys.OrderBy(k => k).ToList();
        return "{" + string.Join("/", sorted) + "}";
    }
}
