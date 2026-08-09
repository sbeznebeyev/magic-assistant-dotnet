using System;

namespace MagicAssistant.Data.Model;

/// <summary>
/// Bit flags for ColorBits / IdentityBits integer columns in the Cards table.
/// Each flag represents a color or color-category and maps to a single bit.
/// 
/// Bit layout:
///   0 (0x001) — C      Colorless
///   1 (0x002) — W      White
///   2 (0x004) — U      Blue
///   3 (0x008) — B      Black
///   4 (0x010) — R      Red
///   5 (0x020) — G      Green
///   6 (0x040) — MULTI  Multi-color (2+ colored bits set)
///   7 (0x080) — MONO   Mono-color (exactly 1 colored bit set)
///   8 (0x100) — HYBRID Hybrid mana symbols in cost ({W/U})
/// 
/// MONO, MULTI, C are mutually exclusive.
/// HYBRID is mutually exclusive with MONO and MULTI — if a card has hybrid mana,
/// only HYBRID is set (matching Java Colors.getColorType() where "/" in cost
/// returns "hybrid" and never "mono" or "multi").
/// </summary>
[Flags]
public enum ColorBitFlags : int
{
    None     = 0x000,
    C        = 0x001,   // Colorless
    W        = 0x002,   // White
    U        = 0x004,   // Blue
    B        = 0x008,   // Black
    R        = 0x010,   // Red
    G        = 0x020,   // Green
    MULTI    = 0x040,   // Multi-color
    MONO     = 0x080,   // Mono-color
    HYBRID   = 0x100,   // Hybrid mana

    /// <summary>Mask for the five basic colors (WUBRG) — bits 1-5.</summary>
    ColoredMask = 0x03E,  // W|U|B|R|G

    /// <summary>Mask for all defined bits.</summary>
    AllMask = 0x1FF,      // C|W|U|B|R|G|MULTI|MONO|HYBRID

    /// <summary>Mask for base colors only (C|W|U|B|R|G). Used by Only-mode to exclude unselected base colors.
    /// Computed flags (MULTI/MONO/HYBRID) are excluded — they are derived from mana cost, not user selection.
    /// HYBRID is mutually exclusive with MONO/MULTI/C (matching Java Colors.getColorType()).</summary>
    BaseColorsMask = 0x03F, // C|W|U|B|R|G
}

/// <summary>
/// Static helpers for computing ColorBits / IdentityBits from card data.
/// </summary>
public static class ColorBitBuilder
{
    /// <summary>
    /// Computes the full ColorBits integer from a card's colors (from Scryfall "colors" array),
    /// mana cost string, and optionally the ColorIdentity column.
    /// 
    /// Call for ColorBits: pass <paramref name="colors"/> from Scryfall DTO, <paramref name="manaCost"/>, identityColors=null.
    /// Call for IdentityBits: pass colors=null, manaCost, identityColors from Scryfall "color_identity" array.
    /// </summary>
    /// <param name="colors">Scryfall "colors" array (W/U/B/R/G). Null/empty = colorless.</param>
    /// <param name="manaCost">Mana cost string (e.g. "{W}{U}"). Used to detect HYBRID.</param>
    /// <param name="identityColors">Scryfall "color_identity" array. If provided, used instead of <paramref name="colors"/> for base color bits.</param>
    public static int Build(
        ICollection<string>? colors,
        string? manaCost,
        ICollection<string>? identityColors = null)
    {
        // Determine the effective color set
        var effectiveColors = identityColors ?? colors;
        var colorSet = effectiveColors as ISet<string>
            ?? new HashSet<string>(effectiveColors ?? Array.Empty<string>());

        // Colorless identity is ignored (Java: return Expr.EMPTY for colorless in COLOR_IDENTITY)
        bool isIdentityMode = identityColors != null;

        // Base color bits
        int coloredBits = 0;
        if (colorSet.Contains("W")) coloredBits |= (int)ColorBitFlags.W;
        if (colorSet.Contains("U")) coloredBits |= (int)ColorBitFlags.U;
        if (colorSet.Contains("B")) coloredBits |= (int)ColorBitFlags.B;
        if (colorSet.Contains("R")) coloredBits |= (int)ColorBitFlags.R;
        if (colorSet.Contains("G")) coloredBits |= (int)ColorBitFlags.G;

        int bits = coloredBits;

        // HYBRID is mutually exclusive with MONO/MULTI/C (matching Java Colors.getColorType()).
        // If mana cost contains "/" → only HYBRID is set; MONO/MULTI/C are never set.
        bool hasHybrid = !string.IsNullOrEmpty(manaCost) && ContainsHybridMana(manaCost);

        if (hasHybrid)
        {
            bits |= (int)ColorBitFlags.HYBRID;
        }
        else
        {
            // C — colorless: no colored bits set (ignored in identity mode, like Java)
            if (coloredBits == 0 && !isIdentityMode)
                bits |= (int)ColorBitFlags.C;

            // MONO — exactly one colored bit
            if (IsPowerOfTwo(coloredBits))
                bits |= (int)ColorBitFlags.MONO;

            // MULTI — two or more colored bits
            else if (PopCount(coloredBits) > 1)
                bits |= (int)ColorBitFlags.MULTI;
        }

        return bits;
    }

    /// <summary>
    /// Quick check: does the mana cost contain hybrid symbols like {W/U} or {2/W}?
    /// </summary>
    private static bool ContainsHybridMana(string manaCost)
    {
        for (int i = 0; i < manaCost.Length; i++)
        {
            if (manaCost[i] == '{')
            {
                int end = manaCost.IndexOf('}', i + 1);
                if (end > i + 1 && manaCost.Substring(i + 1, end - i - 1).Contains('/'))
                    return true;
            }
        }
        return false;
    }

    private static bool IsPowerOfTwo(int value) => value != 0 && (value & (value - 1)) == 0;

    private static int PopCount(int value)
    {
        // Software popcount (BitOperations.PopCount available in .NET Core 3+, but safe fallback)
#if NETCOREAPP3_0_OR_GREATER
        return System.Numerics.BitOperations.PopCount((uint)value);
#else
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }
        return count;
#endif
    }

    /// <summary>
    /// Human-readable name for a single flag bit.
    /// </summary>
    public static string FlagName(ColorBitFlags flag) => flag switch
    {
        ColorBitFlags.C      => "Colorless",
        ColorBitFlags.W      => "White",
        ColorBitFlags.U      => "Blue",
        ColorBitFlags.B      => "Black",
        ColorBitFlags.R      => "Red",
        ColorBitFlags.G      => "Green",
        ColorBitFlags.MULTI  => "Multi-Color",
        ColorBitFlags.MONO   => "Mono-Color",
        ColorBitFlags.HYBRID => "Hybrid",
        _ => flag.ToString()
    };

}
