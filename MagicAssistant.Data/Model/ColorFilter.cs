using System;
using System.Collections.Generic;

namespace MagicAssistant.Data.Model;

/// <summary>
/// Color filter expressed as bit masks over a single INTEGER column (ColorBits or IdentityBits).
/// Built by ColorFilterHelper from 12-checkbox UI state. Consumed by SqliteCardStore.GetPagedAsync.
/// 
/// SQL translation:
///   OR mode:  (col & requiredMask) != 0
///   AND mode: (col & requiredMask) = requiredMask
///   Excluded: (col & excludedMask) == 0
///   Full mask optimization: col = exactValue
/// </summary>
public sealed class ColorFilter : IEquatable<ColorFilter>
{
    /// <summary>Bitwise OR of all color/category bits that must be present.</summary>
    public int RequiredMask { get; init; }

    /// <summary>Bitwise OR of all color bits that must NOT be present (Only / Identity mode).</summary>
    public int ExcludedMask { get; init; }

    /// <summary>When true, base colors in RequiredMask must ALL be present (AND). Otherwise any one suffices (OR).</summary>
    public bool AndMode { get; init; }

    /// <summary>When true, filter against IdentityBits column instead of ColorBits.</summary>
    public bool IdentityMode { get; init; }

    /// <summary>When true, requires the card to be multi-color (2+ colored bits). Computed flag — not part of RequiredMask.</summary>
    public bool RequiresMultiColor { get; init; }

    /// <summary>When true, requires the card to be mono-color (exactly 1 colored bit). Computed flag — not part of RequiredMask.</summary>
    public bool RequiresMonoColor { get; init; }

    /// <summary>When true, requires the card to have hybrid mana ({W/U}, {2/W}). Bit flag — not part of RequiredMask.</summary>
    public bool RequiresHybrid { get; init; }

    /// <summary>True if no filters are set — ToSqlWhere() returns null.</summary>
    public bool IsEmpty => RequiredMask == 0 && ExcludedMask == 0 && !RequiresMultiColor && !RequiresMonoColor && !RequiresHybrid;

    /// <summary>Bit mask for the five basic colors (WUBRG) — bits 1-5.</summary>
    private const int ColoredMask = 0x03E; // W|U|B|R|G

    /// <summary>
    /// Builds the SQL WHERE clause fragment for filtering the Cards table.
    /// Returns null if IsEmpty.
    /// For double-faced cards (DFC), ColorBits/IdentityBits in Cards are NULL;
    /// use ToSqlWhereWithDfcSupport() to include DFC cards via CardFaces subquery.
    /// </summary>
    public string? ToSqlWhere()
    {
        if (IsEmpty) return null;

        var col = IdentityMode ? "IdentityBits" : "ColorBits";
        return BuildCondition(col);
    }

    /// <summary>
    /// Builds the SQL WHERE clause fragment that includes DFC support.
    /// Returns a condition that matches cards where EITHER the parent card's bits match
    /// (normal cards) OR any face's bits match (double-faced cards).
    /// For normal cards: ColorBits/IdentityBits in Cards are non-null and checked directly.
    /// For DFC cards: ColorBits/IdentityBits in Cards are NULL; we check CardFaces via subquery.
    /// </summary>
    public string? ToSqlWhereWithDfcSupport()
    {
        if (IsEmpty) return null;

        var col = IdentityMode ? "IdentityBits" : "ColorBits";
        var faceCol = col; // CardFaces also uses ColorBits/IdentityBits column names
        var cardCondition = BuildCondition(col);
        var faceCondition = BuildCondition(faceCol);

        // Cards where bits match directly, OR cards that have at least one matching face
        return $"({cardCondition}) OR (Id IN (SELECT CardId FROM CardFaces WHERE {faceCondition}))";
    }

    private string BuildCondition(string col)
    {
        var parts = new List<string>();

        // Required colors: AND or OR — only if there's something to require.
        // When RequiredMask == 0 (e.g. MULTI-only without base colors), generating
        // "(col & 0) != 0" would be always FALSE and kill all results.
        if (RequiredMask > 0)
        {
            if (AndMode)
                parts.Add($"({col} & {RequiredMask}) = {RequiredMask}");
            else
                parts.Add($"({col} & {RequiredMask}) != 0");
        }

        // Excluded colors: must not be present
        if (ExcludedMask > 0)
            parts.Add($"({col} & {ExcludedMask}) == 0");

        // Multi-color: at least 2 colored bits set.
        // SQLite has no popcount, so use: coloredBits != 0 AND NOT powerOfTwo(coloredBits).
        // powerOfTwo(x) = (x & (x - 1)) == 0
        // CRITICAL: '-' has HIGHER precedence than '&' in SQLite, so we MUST parenthesize:
        //   (col & mask) - 1  NOT  col & mask - 1 (which parses as col & (mask - 1))
        if (RequiresMultiColor)
            parts.Add($"(({col} & {ColoredMask}) != 0) AND (({col} & {ColoredMask}) & (({col} & {ColoredMask}) - 1) != 0)");

        // Mono-color: exactly 1 colored bit set.
        // powerOfTwo(x) = x != 0 AND (x & (x - 1)) == 0
        if (RequiresMonoColor)
            parts.Add($"(({col} & {ColoredMask}) != 0) AND (({col} & {ColoredMask}) & (({col} & {ColoredMask}) - 1) == 0)");

        // Hybrid: HYBRID bit must be set (0x100)
        if (RequiresHybrid)
            parts.Add($"({col} & {(int)ColorBitFlags.HYBRID}) != 0");

        return string.Join(" AND ", parts);
    }

    // ── IEquatable ──

    public bool Equals(ColorFilter? other)
    {
        if (other is null) return false;
        return RequiredMask == other.RequiredMask
            && ExcludedMask == other.ExcludedMask
            && AndMode == other.AndMode
            && IdentityMode == other.IdentityMode
            && RequiresMultiColor == other.RequiresMultiColor
            && RequiresMonoColor == other.RequiresMonoColor
            && RequiresHybrid == other.RequiresHybrid;
    }

    public override bool Equals(object? obj) => Equals(obj as ColorFilter);

    public override int GetHashCode()
        => HashCode.Combine(RequiredMask, ExcludedMask, AndMode, IdentityMode, RequiresMultiColor, RequiresMonoColor, RequiresHybrid);

    public override string ToString()
        => $"ColorFilter(req=0x{RequiredMask:X}, excl=0x{ExcludedMask:X}, and={AndMode}, identity={IdentityMode}, multi={RequiresMultiColor}, mono={RequiresMonoColor}, hybrid={RequiresHybrid})";
}
