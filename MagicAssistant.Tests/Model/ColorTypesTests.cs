using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.SearchableProperties;

namespace MagicAssistant.Tests.Model;

[TestFixture]
public class ColorTypesTests
{
    #region ManaSplit — root parser for all mana symbol extraction

    [Test]
    public void ManaSplit_ExtractsSymbolsFromBrackets()
    {
        var symbols = ColorTypes.ManaSplit("{2}{W}{U}");

        Assert.That(symbols, Is.EquivalentTo(new[] { "2", "W", "U" }));
    }

    [Test]
    public void ManaSplit_HandlesHybridAndPitch()
    {
        var symbols = ColorTypes.ManaSplit("{W/U}{B/P}");

        Assert.That(symbols, Is.EquivalentTo(new[] { "W/U", "B/P" }));
    }

    [Test]
    public void ManaSplit_ReturnsEmptyForNullOrEmpty()
    {
        Assert.That(ColorTypes.ManaSplit(null), Is.Empty);
        Assert.That(ColorTypes.ManaSplit(""), Is.Empty);
    }

    [Test]
    public void ManaSplit_SingleSymbol()
    {
        var symbols = ColorTypes.ManaSplit("{W}");

        Assert.That(symbols, Is.EquivalentTo(new[] { "W" }));
    }

    #endregion

    #region GetConvertedManaCost — CMC calculation (depends on ManaSplit)

    /// <summary>
    /// Per MTG rules, each { } symbol contributes to CMC:
    /// - Generic digits ({2}, {7}) → their numeric value
    /// - Colored mana ({W}, {U}) → 1 each
    /// - Hybrid colored ({W/U}) → 1
    /// - Pitch ({W/P}) → 1
    /// - Variable ({X}, {Y}, {Z}) → 0
    /// - Tap/etc ({T}, {Q}) → 0
    /// </summary>
    [Test]
    public void GetConvertedManaCost_NullAndEmptyReturnZero()
    {
        Assert.That(ColorTypes.GetConvertedManaCost(null), Is.EqualTo(0));
        Assert.That(ColorTypes.GetConvertedManaCost(""), Is.EqualTo(0));
    }

    [Test]
    public void GetConvertedManaCost_SingleGenericDigit()
    {
        Assert.That(ColorTypes.GetConvertedManaCost("{3}"), Is.EqualTo(3));
    }

    [Test]
    public void GetConvertedManaCost_GenericPlusColor()
    {
        Assert.That(ColorTypes.GetConvertedManaCost("{2}{W}"), Is.EqualTo(3));
    }

    [Test]
    public void GetConvertedManaCost_GenericPlusTwoColors()
    {
        Assert.That(ColorTypes.GetConvertedManaCost("{2}{W}{U}"), Is.EqualTo(4));
    }

    [Test]
    public void GetConvertedManaCost_VariableManaDoesNotCount()
    {
        Assert.That(ColorTypes.GetConvertedManaCost("{X}{X}{B}"), Is.EqualTo(1));
    }

    [Test]
    public void GetConvertedManaCost_HybridColoredCountsAsOne()
    {
        Assert.That(ColorTypes.GetConvertedManaCost("{W/U}"), Is.EqualTo(1));
    }

    [Test]
    public void GetConvertedManaCost_PitchManaCountsAsOne()
    {
        Assert.That(ColorTypes.GetConvertedManaCost("{W/P}"), Is.EqualTo(1));
    }

    [Test]
    public void GetConvertedManaCost_TapReturnsZero()
    {
        Assert.That(ColorTypes.GetConvertedManaCost("{T}"), Is.EqualTo(0));
    }

    [Test]
    public void GetConvertedManaCost_ZeroCostReturnsZero()
    {
        Assert.That(ColorTypes.GetConvertedManaCost("{0}"), Is.EqualTo(0));
    }

    [Test]
    public void GetConvertedManaCost_GenericHybridTakesNumber()
    {
        Assert.That(ColorTypes.GetConvertedManaCost("{2/W}"), Is.EqualTo(2));
    }

    /// <summary>
    /// BUG: "{R}" (red mana) is treated as zero-cost reuse symbol in GetConvertedManaCost.
    /// The code has: x is "T" or "Q" or "L" or "S" or "D" or "N" or "R" or "E"
    /// This skips {R} entirely, counting it as 0 instead of 1.
    /// Fix: remove "R" from the zero-cost skip list (reuse is rare and ambiguous anyway).
    /// </summary>
    [Test]
    public void GetConvertedManaCost_ComplexFiveColor()
    {
        var result = ColorTypes.GetConvertedManaCost("{3}{W}{U}{B}{R}{G}");

        Assert.That(result, Is.EqualTo(8),
            $"BUG IN GetConvertedManaCost: {{R}} (red mana) is incorrectly skipped as a zero-cost reuse symbol. " +
            $"Expected CMC=8, got {result}. " +
            "The skip list contains \"R\" which matches red mana {{R}}, treating it as the reuse ability symbol. " +
            "Fix: remove \"R\" from `if (x is \"T\" or \"Q\" or \"L\" or \"S\" or \"D\" or \"N\" or \"R\" or \"E\")`.");
    }

    [Test]
    public void GetConvertedManaCost_SplitCardTakesMax()
    {
        Assert.That(ColorTypes.GetConvertedManaCost("{1}{W}, {2}{B}"), Is.EqualTo(3));
    }

    #endregion

    #region GetColorType — derived from ColorBits (single source of truth)

    [Test]
    public void GetColorType_IdentifiesMonoColors()
    {
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(["W"], "{W}")), Is.EqualTo("MONO-W"));
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(["U"], "{U}")), Is.EqualTo("MONO-U"));
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(["B"], "{B}")), Is.EqualTo("MONO-B"));
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(["R"], "{R}")), Is.EqualTo("MONO-R"));
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(["G"], "{G}")), Is.EqualTo("MONO-G"));
    }

    [Test]
    public void GetColorType_IdentifiesMulticolor()
    {
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(["W", "B"], "{W}{B}")), Is.EqualTo("MULTI"));
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(["W", "U", "B"], "{W}{U}{B}")), Is.EqualTo("MULTI"));
    }

    [Test]
    public void GetColorType_IdentifiesHybrid()
    {
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(["W", "U"], "{W/U}")), Is.EqualTo("HYBRID"));
    }

    [Test]
    public void GetColorType_IdentifiesColorless()
    {
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(Array.Empty<string>(), "{3}")), Is.EqualTo("C"));
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(Array.Empty<string>(), "")), Is.EqualTo("C"));
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(null, null)), Is.EqualTo("C"));
    }

    [Test]
    public void GetColorType_MixedNumericAndColorIsMono()
    {
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(["W"], "{1}{W}")), Is.EqualTo("MONO-W"));
    }

    [Test]
    public void GetColorType_GenericHybrid_IsHybrid()
    {
        // {2/W} — generic hybrid: HYBRID bit set, not MULTI
        Assert.That(ColorTypes.GetColorType(ColorBitBuilder.Build(["W"], "{2/W}")), Is.EqualTo("HYBRID"));
    }

    #endregion

    #region GetColorPresence — color extraction (depends on ManaSplit)

    [Test]
    public void GetColorPresence_ExtractsFromManaCost()
    {
        var colors = ColorTypes.GetColorPresence("{W}{B}{2}");

        Assert.That(colors, Is.EquivalentTo(new[] { "W", "B" }));
    }

    [Test]
    public void GetColorPresence_ExtractsFromHybridMana()
    {
        var colors = ColorTypes.GetColorPresence("{W/U}{2/B}");

        Assert.That(colors, Is.EquivalentTo(new[] { "W", "U", "B" }));
    }

    [Test]
    public void GetColorPresence_ReturnsEmptyForNull()
    {
        Assert.That(ColorTypes.GetColorPresence(null), Is.Empty);
        Assert.That(ColorTypes.GetColorPresence(""), Is.Empty);
    }

    #endregion

    #region GetColorIdentity — combined cost + oracle text (depends on ManaSplit)

    [Test]
    public void GetColorIdentity_CombinesCostAndOracleText()
    {
        var identity = ColorTypes.GetColorIdentity("{W}{B}", "It has a G ability");

        Assert.That(identity, Is.EquivalentTo(new[] { "W", "B", "G" }));
    }

    [Test]
    public void GetColorIdentity_HandlesNullOracleText()
    {
        var identity = ColorTypes.GetColorIdentity("{R}", null);

        Assert.That(identity, Is.EquivalentTo(new[] { "R" }));
    }

    #endregion

    #region SortColors — WUBRG order (independent, no ManaSplit dependency)

    [Test]
    public void SortColors_SortsInWUBRGOrder()
    {
        var sorted = ColorTypes.SortColors(new[] { "G", "W", "B", "R", "U" });

        Assert.That(sorted, Is.EqualTo(new[] { "W", "U", "B", "R", "G" }));
    }

    [Test]
    public void SortColors_OnlyReturnsKnownColors()
    {
        var sorted = ColorTypes.SortColors(new[] { "W", "X", "G" });

        Assert.That(sorted, Is.EqualTo(new[] { "W", "G" }));
    }

    #endregion

    #region ToCost — tags to mana cost format (independent)

    [Test]
    public void ToCost_ConvertsTagsToManaCost()
    {
        var cost = ColorTypes.ToCost(new[] { "B", "W" });

        Assert.That(cost, Is.EqualTo("{W}{B}"));
    }

    [Test]
    public void ToCost_EmptyTagsReturnsEmptyString()
    {
        var cost = ColorTypes.ToCost(Array.Empty<string>());

        Assert.That(cost, Is.EqualTo(""));
    }

    #endregion

    #region GetColorName — human-readable name (depends on ManaSplit)

    [Test]
    public void GetColorName_ReturnsHumanReadableName()
    {
        Assert.That(ColorTypes.GetColorName("{W}"), Is.EqualTo("White"));
        Assert.That(ColorTypes.GetColorName("{W}{B}"), Is.EqualTo("White-Black"));
        Assert.That(ColorTypes.GetColorName(null), Is.EqualTo("No Cost"));
    }

    [Test]
    public void GetColorName_ColorlessAndNoCost()
    {
        Assert.That(ColorTypes.GetColorName("{3}"), Is.EqualTo("Colorless"));
        Assert.That(ColorTypes.GetColorName(""), Is.EqualTo("No Cost"));
    }

    #endregion

    #region HasColor — color type containment check (independent)

    /// <summary>
    /// BUG: HasColor uses simple string.Contains() which produces false positives.
    /// "MULTI".Contains("R") → true (letter R in word MULTI), but MULTI is not red-specific.
    /// "MONO-W".Contains("W") → true, which happens to be correct by accident.
    /// 
    /// The method should parse the color type properly:
    /// - "MONO-W" contains W only
    /// - "MULTI" does NOT contain any specific color (it's a general category)
    /// - "HYBRID" does NOT contain any specific color
    /// </summary>
    [Test]
    public void HasColor_ChecksColorTypeForSpecificColor()
    {
        // MONO-W should contain W
        Assert.That(ColorTypes.HasColor("MONO-W", "W"), Is.True,
            "\"MONO-W\" should contain color W");

        // MONO-W should NOT contain B
        Assert.That(ColorTypes.HasColor("MONO-W", "B"), Is.False,
            "\"MONO-W\" should NOT contain color B");

        // BUG: MULTI does not specify a particular color — it's a general category.
        // HasColor("MULTI", "R") should be FALSE because MULTI means "some combination of 2+ colors"
        // but doesn't tell us WHICH colors. Current substring-based implementation returns TRUE
        // because the letter 'R' exists in the word "MULTI" — a false positive.
        Assert.That(ColorTypes.HasColor("MULTI", "R"), Is.False,
            "\"MULTI\" is a general category, NOT red-specific. " +
            "BUG: HasColor uses string.Contains() which produces a false positive: " +
            "the letter 'R' exists in the word \"MULTI\" but MULTI does not mean \"contains red\". " +
            "Fix: parse MONO-X format to extract the actual color, return false for generic categories.");

        // BUG: HYBRID same issue — letter 'B' exists in "HYBRID", so Contains("B") returns true
        Assert.That(ColorTypes.HasColor("HYBRID", "B"), Is.False,
            "\"HYBRID\" is a general category, NOT black-specific. " +
            "BUG: HasColor uses string.Contains() which produces a false positive: " +
            "the letter 'B' exists in the word \"HYBRID\" but HYBRID does not mean \"contains black\". " +
            "Fix: parse MONO-X format to extract the actual color, return false for generic categories.");

        // Null should return false
        Assert.That(ColorTypes.HasColor(null, "W"), Is.False);
    }

    #endregion

    #region GetColorSort — sort value calculation (depends on ManaSplit)

    [Test]
    public void GetColorSort_NullAndEmptyReturnZero()
    {
        Assert.That(ColorTypes.GetColorSort(null), Is.EqualTo(0));
        Assert.That(ColorTypes.GetColorSort(""), Is.EqualTo(0));
    }

    [Test]
    public void GetColorSort_WildcardReturnsMaxBitmask()
    {
        Assert.That(ColorTypes.GetColorSort("*"), Is.EqualTo(63));
    }

    [Test]
    public void GetColorSort_MonoColorsAreDistinct()
    {
        var w = ColorTypes.GetColorSort("{W}");
        var u = ColorTypes.GetColorSort("{U}");
        var b = ColorTypes.GetColorSort("{B}");
        var r = ColorTypes.GetColorSort("{R}");
        var g = ColorTypes.GetColorSort("{G}");

        var values = new[] { w, u, b, r, g };
        Assert.That(values.Distinct().Count(), Is.EqualTo(5),
            $"Mono colors should have distinct sort values. Got W={w}, U={u}, B={b}, R={r}, G={g}.");
    }

    [Test]
    public void GetColorSort_MonoColorsAreNegative()
    {
        Assert.That(ColorTypes.GetColorSort("{W}"), Is.LessThan(0));
    }

    #endregion

    #region ColorTypesSearch — searchable property (independent)

    /// <summary>
    /// BUG: ColorTypesSearch uses lowercase keys ("mono-W") but GetColorType returns uppercase ("MONO-W").
    /// This case mismatch means filter grouping by color type will NEVER match cards.
    /// Keys should be uppercase to match GetColorType output: "MONO-W", "MULTI", "HYBRID", "C".
    /// </summary>
    [Test]
    public void ColorTypesSearch_ContainsExpectedColorIds()
    {
        var ids = ColorTypesSearch.Instance.GetIds().ToList();

        Assert.That(ids, Does.Contain("MONO-W"),
            $"BUG: ColorTypesSearch should contain 'MONO-W' (uppercase) to match GetColorType output. " +
            $"Current keys: [{string.Join(", ", ids)}]");
        Assert.That(ids, Does.Contain("MONO-U"));
        Assert.That(ids, Does.Contain("MONO-B"));
        Assert.That(ids, Does.Contain("MONO-R"));
        Assert.That(ids, Does.Contain("MONO-G"));
        Assert.That(ids, Does.Contain("MULTI"),
            $"BUG: ColorTypesSearch should contain 'MULTI' (uppercase) to match GetColorType output.");
        Assert.That(ids, Does.Contain("C"));
    }

    [Test]
    public void ColorTypesSearch_ReturnsCorrectNames()
    {
        Assert.That(ColorTypesSearch.Instance.GetNameById("MONO-W"), Is.EqualTo("White"),
            "BUG: GetNameById('MONO-W') should return 'White'. Key must be uppercase to match GetColorType output.");
        Assert.That(ColorTypesSearch.Instance.GetNameById("MULTI"), Is.EqualTo("Multicolor"),
            "BUG: GetNameById('MULTI') should return 'Multicolor'. Key must be uppercase.");
        Assert.That(ColorTypesSearch.Instance.GetNameById("C"), Is.EqualTo("Colorless"));
    }

    [Test]
    public void ColorTypesSearch_FilterFieldIsColorType()
    {
        Assert.That(ColorTypesSearch.Instance.GetFilterField(), Is.EqualTo("ColorType"));
    }

    #endregion

    #region Cross-module — GetColorType output vs ColorTypesSearch keys

    /// <summary>
    /// Verifies that GetColorType(colorBits) output matches ColorTypesSearch keys.
    /// Both must use the same uppercase format: "MONO-W", "MULTI", "HYBRID", "C".
    /// </summary>
    [Test]
    public void GetColorType_OutputMatchesColorTypesSearch_Keys()
    {
        var search = ColorTypesSearch.Instance;
        var searchIds = search.GetIds().ToHashSet();

        // Test MONO-W
        var monoBits = ColorBitBuilder.Build(["W"], "{W}");
        var monoResult = ColorTypes.GetColorType(monoBits);
        Assert.That(searchIds.Contains(monoResult), Is.True,
            $"GetColorType returns '{monoResult}' but ColorTypesSearch does not contain it. " +
            $"Available keys: [{string.Join(", ", searchIds)}].");

        // Test MULTI
        var multiBits = ColorBitBuilder.Build(["W", "B"], "{W}{B}");
        var multiResult = ColorTypes.GetColorType(multiBits);
        Assert.That(searchIds.Contains(multiResult), Is.True,
            $"GetColorType returns '{multiResult}' for multicolor but ColorTypesSearch does not contain it. " +
            $"Available keys: [{string.Join(", ", searchIds)}].");

        // Test C (colorless)
        var colorlessBits = ColorBitBuilder.Build(Array.Empty<string>(), "{3}");
        var colorlessResult = ColorTypes.GetColorType(colorlessBits);
        Assert.That(searchIds.Contains(colorlessResult), Is.True,
            $"GetColorType returns '{colorlessResult}' for colorless but ColorTypesSearch does not contain it.");

        // Test HYBRID
        var hybridBits = ColorBitBuilder.Build(["W", "U"], "{W/U}");
        var hybridResult = ColorTypes.GetColorType(hybridBits);
        Assert.That(searchIds.Contains(hybridResult), Is.True,
            $"GetColorType returns '{hybridResult}' for hybrid but ColorTypesSearch does not contain it. " +
            $"Available keys: [{string.Join(", ", searchIds)}].");
    }

    #endregion
}
