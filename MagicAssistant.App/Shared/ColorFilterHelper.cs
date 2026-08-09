using System.Collections.Generic;
using MagicAssistant.Data.Model;

namespace MagicAssistant.App.Shared;

/// <summary>
/// Builds a <see cref="ColorFilter" /> from color checkbox state + modifiers.
///
/// Algorithm mirrors Java:
/// - ColorsPreferenceGroup.java — UI layout
/// - MagicCardFilter.createColorGroup() — expression build
/// - FilterField.case COLOR / COLOR_IDENTITY — value mapping
/// - Colors.getColorType() — color type computation
///
/// Detailed algorithm documentation: /home/node/.openclaw/ws/docs/color-filters.md
/// </summary>
public static class ColorFilterHelper
{
    /// <summary>
    /// Builds a <see cref="ColorFilter" /> from the checkbox dictionary and modifier flags.
    /// </summary>
    /// <param name="checkboxes">Dictionary keyed by <see cref="ColorBitFlags" /> — true = checked.</param>
    /// <param name="andMode">All selected colors must be present simultaneously (AND vs OR).</param>
    /// <param name="onlyMode">Exclude all unselected colors.</param>
    /// <param name="identityMode">Filter by color identity instead of mana cost. Implies Only.</param>
    /// <returns>ColorFilter with computed bit masks, or null if no checkboxes are checked.</returns>
    public static ColorFilter? GetFilter(
        Dictionary<ColorBitFlags, bool> checkboxes,
        bool andMode = false,
        bool onlyMode = false,
        bool identityMode = false)
    {
        int requiredMask = 0;
        int excludedMask = 0;

        // Identity mode automatically implies Only (Java behavior)
        bool effectiveOnly = onlyMode || identityMode;

        // Build required mask from checked flags.
        // MULTI/MONO/HYBRID are handled via separate Requires* properties — they must be
        // AND'ed with base colors, not mixed into RequiredMask (which uses OR).
        bool requiresMulti = false;
        bool requiresMono = false;
        bool requiresHybrid = false;

        foreach (var kvp in checkboxes)
        {
            if (!kvp.Value)
                continue;

            // Skip colorless in identity mode (Java: Expr.EMPTY for colorless in COLOR_IDENTITY)
            if (kvp.Key == ColorBitFlags.C && identityMode)
                continue;

            // Computed flags → separate properties, not bitmask
            if (kvp.Key == ColorBitFlags.MULTI)
            {
                requiresMulti = true;
                continue;
            }
            if (kvp.Key == ColorBitFlags.MONO)
            {
                requiresMono = true;
                continue;
            }
            if (kvp.Key == ColorBitFlags.HYBRID)
            {
                requiresHybrid = true;
                continue;
            }

            requiredMask |= (int)kvp.Key;
        }

        // Build excluded mask from unchecked BASE flags when Only/Identity is active.
        // Only base colors (C,W,U,B,R,G) are excluded. Computed flags (MULTI/MONO/HYBRID)
        // are derived from mana cost and must NOT be excluded by only-mode.
        if (effectiveOnly)
        {
            foreach (var flag in ColorFilterFlags.All)
            {
                // Skip computed flags — they are derived, not user-selected
                if (flag == ColorBitFlags.MULTI || flag == ColorBitFlags.MONO || flag == ColorBitFlags.HYBRID)
                    continue;

                if (checkboxes.GetValueOrDefault(flag))
                    continue;

                // Skip colorless in identity mode (Java: Expr.EMPTY for colorless in COLOR_IDENTITY)
                if (flag == ColorBitFlags.C && identityMode)
                    continue;

                excludedMask |= (int)flag;
            }
        }

        // No filter if nothing is selected
        if (requiredMask == 0 && excludedMask == 0 && !requiresMulti && !requiresMono && !requiresHybrid)
            return null;

        return new ColorFilter
        {
            RequiredMask = requiredMask,
            ExcludedMask = excludedMask,
            AndMode = andMode,
            IdentityMode = identityMode,
            RequiresMultiColor = requiresMulti,
            RequiresMonoColor = requiresMono,
            RequiresHybrid = requiresHybrid
        };
    }

    private static bool GetValueOrDefault(this Dictionary<ColorBitFlags, bool> dict, ColorBitFlags key)
    {
        return dict.TryGetValue(key, out var value) && value;
    }
}
