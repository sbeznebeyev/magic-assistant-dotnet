using System.Text.RegularExpressions;

namespace MagicAssistant.Data.Model;

public static class ColorTypes
{
    public const string White = "W";
    public const string Blue = "U";
    public const string Black = "B";
    public const string Red = "R";
    public const string Green = "G";
    public const string Colorless = "C";

    private static readonly HashSet<char> ColorSymbols = ['W', 'U', 'B', 'R', 'G'];
    private static readonly Dictionary<char, string> ColorMap = new()
    {
        ['W'] = White,
        ['U'] = Blue,
        ['B'] = Black,
        ['R'] = Red,
        ['G'] = Green,
    };

    // Pattern to match color presence in text (e.g. "WP" for white pitch)
    private static readonly Regex ColorPresencePattern = new(@"\b([WUBRG])P?\b", RegexOptions.Compiled);

    /// <summary>
    /// Splits a mana cost string into individual mana symbols by { } brackets.
    /// Mirrors Java Colors.manaSplit().
    /// </summary>
    public static ICollection<string> ManaSplit(string? cost)
    {
        if (string.IsNullOrEmpty(cost))
            return Array.Empty<string>();

        // If cost contains comma, it's a multi-part cost (split card) — treat as one big value
        if (cost.Contains(','))
            return ["1000000"];

        var manas = new List<string>();
        for (var i = 0; i < cost.Length; i++)
        {
            if (cost[i] == '{')
            {
                var end = cost.IndexOf('}', i + 1);
                if (end > 0)
                {
                    manas.Add(cost[(i + 1)..end]);
                    i = end;
                }
            }
        }

        return manas;
    }

    /// <summary>
    /// Derives the color type string from ColorBits bitmask.
    /// Single source of truth — guaranteed to match ColorBits.
    /// Returns: "C", "MONO-W", "MONO-U", "MONO-B", "MONO-R", "MONO-G", "MULTI", "HYBRID"
    /// </summary>
    public static string GetColorType(int colorBits)
    {
        // HYBRID is mutually exclusive with MONO/MULTI/C
        if ((colorBits & (int)ColorBitFlags.HYBRID) != 0)
            return "HYBRID";

        if ((colorBits & (int)ColorBitFlags.MONO) != 0)
        {
            // Find which single color is set
            if ((colorBits & (int)ColorBitFlags.W) != 0) return "MONO-W";
            if ((colorBits & (int)ColorBitFlags.U) != 0) return "MONO-U";
            if ((colorBits & (int)ColorBitFlags.B) != 0) return "MONO-B";
            if ((colorBits & (int)ColorBitFlags.R) != 0) return "MONO-R";
            if ((colorBits & (int)ColorBitFlags.G) != 0) return "MONO-G";
        }

        if ((colorBits & (int)ColorBitFlags.MULTI) != 0)
            return "MULTI";

        if ((colorBits & (int)ColorBitFlags.C) != 0)
            return Colorless;

        // Fallback: no computed flag set — treat as colorless
        return Colorless;
    }

    /// <summary>
    /// Calculates the Converted Mana Cost from a mana cost string.
    /// Mirrors Java Colors.getConvertedManaCost().
    /// Handles: {X}/{Y}/{Z} (variable = 0), digits, {W/P}, generic colors,
    /// {T}/{Q} (tap/untap = 0), {0} (zero cost), multi-part split card costs.
    /// </summary>
    public static int GetConvertedManaCost(string? cost)
    {
        if (string.IsNullOrEmpty(cost))
            return 0;

        // Multi-part cost (split cards) — e.g. "1000000,200" 
        // Each part is a separate cost; take the max or sum depending on context.
        // For CMC purposes, Scryfall provides pre-computed CMC, so this is a fallback.
        if (cost.Contains(','))
        {
            var parts = cost.Split(',');
            var maxCmc = 0;
            foreach (var part in parts)
            {
                var partCmc = GetConvertedManaCost(part.Trim());
                if (partCmc > maxCmc)
                    maxCmc = partCmc;
            }
            return maxCmc;
        }

        var manas = ManaSplit(cost).ToList();
        var cmc = 0;

        foreach (var x in manas)
        {
            // Variable mana — doesn't contribute to CMC
            if (x is "X" or "Y" or "Z")
                continue;

            // Tap/untap/sacrifice — zero cost
            // NOTE: "R" (red mana) is NOT in this list — {R} counts as 1 CMC (colored mana), not 0.
            // Reuse symbol {R} is rare and ambiguous; Scryfall provides pre-computed CMC anyway.
            if (x is "T" or "Q" or "L" or "S" or "D" or "N" or "E")
                continue;

            // Numeric mana (e.g. "2", "7", "10", "0")
            if (x.Length > 0 && char.IsDigit(x[0]))
            {
                var slash = x.IndexOf('/');
                if (slash > 0)
                {
                    // Generic hybrid like {2/W} — take the number part
                    if (int.TryParse(x[..slash], out var num))
                        cmc += num;
                }
                else if (int.TryParse(x, out var n))
                {
                    cmc += n;  // {0} adds 0, which is correct
                }
                continue;
            }

            // Color mana or generic pitch — counts as 1
            cmc++;
        }

        return cmc;
    }

    /// <summary>
    /// Finds all color presence in a mana cost string or oracle text.
    /// For mana costs like "{W}{B}{2}", parses individual { } symbols.
    /// For oracle text, uses regex to find color symbols (e.g. "WP" for white pitch).
    /// Mirrors Java Colors.getColorPresense().
    /// </summary>
    public static ICollection<string> GetColorPresence(string? text)
    {
        var colors = new HashSet<string>();
        if (string.IsNullOrEmpty(text))
            return colors;

        // If the text contains '{', treat it as a mana cost and parse { } symbols
        if (text.Contains('{'))
        {
            var manas = ManaSplit(text);
            foreach (var mana in manas)
            {
                // Single color symbol: W, U, B, R, G
                if (mana.Length == 1 && ColorSymbols.Contains(mana[0]))
                {
                    colors.Add(mana);
                    continue;
                }

                // Pitch mana: W/P, U/P, etc. — second part is exactly "P"
                if (mana.EndsWith("/P") && ColorSymbols.Contains(mana[0]))
                {
                    colors.Add(mana[..1]);
                    continue;
                }

                // Hybrid mana: W/U, 2/W, etc. — check both parts
                if (mana.Contains('/'))
                {
                    var parts = mana.Split('/');
                    foreach (var part in parts)
                    {
                        if (part.Length == 1 && ColorSymbols.Contains(part[0]))
                            colors.Add(part);
                    }
                }
            }
        }
        else
        {
            // Oracle text or plain text — use regex for color presence
            foreach (Match match in ColorPresencePattern.Matches(text))
                colors.Add(match.Groups[1].Value);
        }

        return colors;
    }

    /// <summary>
    /// Gets the color identity of a card from its mana cost.
    /// Mirrors Java Colors.getColorIdentity().
    /// </summary>
    public static ICollection<string> GetColorIdentity(string? cost, string? oracleText)
    {
        var colors = new HashSet<string>();
        foreach (var c in GetColorPresence(cost))
            colors.Add(c);
        foreach (var c in GetColorPresence(oracleText))
            colors.Add(c);
        return colors;
    }

    /// <summary>
    /// Sorts color tags in WUBRG order. Mirrors Java Colors.sortTags().
    /// </summary>
    public static ICollection<string> SortColors(ICollection<string> tags)
    {
        var order = new[] { White, Blue, Black, Red, Green };
        return order.Where(c => tags.Contains(c)).ToList();
    }

    /// <summary>
    /// Converts a collection of color tags to mana cost format: {W}{U}.
    /// Mirrors Java Colors.toCost().
    /// </summary>
    public static string ToCost(ICollection<string> tags)
    {
        var sorted = SortColors(tags);
        return string.Concat(sorted.Select(c => $"{{{c}}}"));
    }

    /// <summary>
    /// Gets the human-readable color name from a cost string.
    /// Mirrors Java Colors.getColorName().
    /// </summary>
    public static string GetColorName(string? cost)
    {
        if (string.IsNullOrEmpty(cost))
            return "No Cost";

        var colors = SortColors(GetColorPresence(cost));
        if (!colors.Any())
            return "Colorless";

        var names = colors.Select(c => c switch
        {
            White => "White",
            Blue => "Blue",
            Black => "Black",
            Red => "Red",
            Green => "Green",
            _ => c
        });

        return string.Join("-", names);
    }

    /// <summary>
    /// Checks if a color type contains a specific color.
    /// </summary>
    public static bool HasColor(string? colorType, string color)
    {
        if (string.IsNullOrEmpty(colorType) || string.IsNullOrEmpty(color))
            return false;

        // MONO-X: check if the specific mono-color matches
        var monoPrefix = "MONO-" + color;
        if (colorType == monoPrefix)
            return true;

        // MULTI, HYBRID, C (Colorless) and other generic categories do NOT imply a specific color.
        // They are general categories, not color-specific identifiers.
        return false;
    }

    /// <summary>
    /// Gets the integer sort value for colors (for UI sorting).
    /// Mirrors Java Colors.getColorSort().
    /// </summary>
    public static int GetColorSort(string? cost)
    {
        if (string.IsNullOrEmpty(cost)) return 0;
        if (cost == "*") return 0b111111;

        var colors = GetColorPresence(cost);
        var sum = 1;
        var times = 0;

        foreach (var c in new[] { White, Blue, Black, Red, Green })
        {
            sum <<= 1;
            if (colors.Contains(c))
            {
                times++;
                sum |= 0b1;
            }
        }

        if (sum == 0) return 1;
        if (times == 1) sum <<= 7;
        return -sum;
    }
}
