using MagicAssistant.App.Shared;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.ColorFilter;

[TestFixture]
public class ColorFilterHelperTests
{
    private static bool Has(int bits, ColorBitFlags flag) => (bits & (int)flag) != 0;
    private static bool HasAll(int bits, params ColorBitFlags[] flags)
        => flags.All(f => (bits & (int)f) != 0);
    private static bool Missing(int bits, ColorBitFlags flag) => (bits & (int)flag) == 0;

    private Dictionary<ColorBitFlags, bool> EmptyBoxes()
    {
        var boxes = new Dictionary<ColorBitFlags, bool>();
        foreach (var flag in ColorFilterFlags.All)
            boxes[flag] = false;
        return boxes;
    }

    private void Set(Dictionary<ColorBitFlags, bool> boxes, ColorBitFlags flag)
        => boxes[flag] = true;

    // ── Empty / null cases ──

    [Test]
    public void GetFilter_AllUnchecked_ReturnsNull()
    {
        Assert.That(ColorFilterHelper.GetFilter(EmptyBoxes()), Is.Null);
    }

    [Test]
    public void GetFilter_OnlyModifiersNoColors_ExcludesAllBaseColors()
    {
        // Only mode with no colors checked → required=0, only BASE flags excluded
        var filter = ColorFilterHelper.GetFilter(EmptyBoxes(), andMode: true, onlyMode: true);
        Assert.That(filter, Is.Not.Null);
        Assert.That(filter!.RequiredMask, Is.Zero);
        // Only base colors excluded: C|W|U|B|R|G = 63. Computed flags NOT excluded.
        Assert.That(filter.ExcludedMask, Is.EqualTo((int)ColorBitFlags.BaseColorsMask));
    }

    // ── OR mode (default) ──

    [Test]
    public void GetFilter_SingleColor_OR_Mode()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);

        var filter = ColorFilterHelper.GetFilter(boxes)!;
        Assert.That(Has(filter.RequiredMask, ColorBitFlags.W), Is.True);
        Assert.That(filter.ExcludedMask, Is.Zero);
        Assert.That(filter.AndMode, Is.False);
        Assert.That(filter.IdentityMode, Is.False);
    }

    [Test]
    public void GetFilter_MultipleColors_OR_Mode()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        Set(boxes, ColorBitFlags.U);
        Set(boxes, ColorBitFlags.B);

        var filter = ColorFilterHelper.GetFilter(boxes)!;
        Assert.That(HasAll(filter.RequiredMask, ColorBitFlags.W, ColorBitFlags.U, ColorBitFlags.B), Is.True);
        Assert.That(filter.ExcludedMask, Is.Zero);
    }

    // ── AND mode ──

    [Test]
    public void GetFilter_TwoColors_AND_Mode()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        Set(boxes, ColorBitFlags.U);

        var filter = ColorFilterHelper.GetFilter(boxes, andMode: true)!;
        Assert.That(filter.AndMode, Is.True);
        Assert.That(HasAll(filter.RequiredMask, ColorBitFlags.W, ColorBitFlags.U), Is.True);
    }

    [Test]
    public void GetFilter_AndMode_SingleColor()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.R);

        var filter = ColorFilterHelper.GetFilter(boxes, andMode: true)!;
        Assert.That(filter.AndMode, Is.True);
        Assert.That(Has(filter.RequiredMask, ColorBitFlags.R), Is.True);
    }

    // ── ONLY mode ──

    [Test]
    public void GetFilter_OnlyMode_ExcludesUnselectedBaseColors()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        Set(boxes, ColorBitFlags.U);

        var filter = ColorFilterHelper.GetFilter(boxes, onlyMode: true)!;
        Assert.That(HasAll(filter.RequiredMask, ColorBitFlags.W, ColorBitFlags.U), Is.True);

        // Only BASE colors are excluded (B,R,G,C). Computed flags (MULTI/MONO/HYBRID) are NOT excluded.
        Assert.That(HasAll(filter.ExcludedMask, ColorBitFlags.B, ColorBitFlags.R, ColorBitFlags.G, ColorBitFlags.C), Is.True);
        Assert.That(Missing(filter.ExcludedMask, ColorBitFlags.MULTI), Is.True);
        Assert.That(Missing(filter.ExcludedMask, ColorBitFlags.MONO), Is.True);
        Assert.That(Missing(filter.ExcludedMask, ColorBitFlags.HYBRID), Is.True);
    }

    [Test]
    public void GetFilter_OnlyMode_AllBaseColorsSelected_NoExclusions()
    {
        var boxes = EmptyBoxes();
        // Only base colors matter for only-mode exclusions
        Set(boxes, ColorBitFlags.C);
        Set(boxes, ColorBitFlags.W);
        Set(boxes, ColorBitFlags.U);
        Set(boxes, ColorBitFlags.B);
        Set(boxes, ColorBitFlags.R);
        Set(boxes, ColorBitFlags.G);

        var filter = ColorFilterHelper.GetFilter(boxes, onlyMode: true)!;
        Assert.That(filter.ExcludedMask, Is.Zero); // all base colors selected → nothing to exclude
    }

    // ── AND + ONLY combined ──

    [Test]
    public void GetFilter_AndAndOnly_BothActive()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        Set(boxes, ColorBitFlags.U);

        var filter = ColorFilterHelper.GetFilter(boxes, andMode: true, onlyMode: true)!;
        Assert.That(filter.AndMode, Is.True);
        Assert.That(HasAll(filter.RequiredMask, ColorBitFlags.W, ColorBitFlags.U), Is.True);
        Assert.That(filter.ExcludedMask, Is.Not.Zero);
    }

    // ── IDENTITY mode ──

    [Test]
    public void GetFilter_IdentityMode_ImplicitlyEnablesOnly()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);

        var filter = ColorFilterHelper.GetFilter(boxes, identityMode: true)!;
        Assert.That(filter.IdentityMode, Is.True);
        Assert.That(HasAll(filter.ExcludedMask, ColorBitFlags.U, ColorBitFlags.B,
            ColorBitFlags.R, ColorBitFlags.G), Is.True);
    }

    [Test]
    public void GetFilter_IdentityMode_IgnoresColorless()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        Set(boxes, ColorBitFlags.C);

        var filter = ColorFilterHelper.GetFilter(boxes, identityMode: true)!;
        Assert.That(Has(filter.RequiredMask, ColorBitFlags.W), Is.True);
        Assert.That(Missing(filter.RequiredMask, ColorBitFlags.C), Is.True);
        Assert.That(Missing(filter.ExcludedMask, ColorBitFlags.C), Is.True);
    }

    [Test]
    public void GetFilter_IdentityMode_ColorlessOnly_ExcludesAllNonColorless()
    {
        // Identity mode ignores colorless → required=0, but onlyMode excludes all other flags
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.C);

        var filter = ColorFilterHelper.GetFilter(boxes, identityMode: true)!;
        Assert.That(filter.RequiredMask, Is.Zero); // C is ignored in identity mode
        // All non-C flags excluded (identity implies only)
        Assert.That(Missing(filter.ExcludedMask, ColorBitFlags.C), Is.True);
        Assert.That(HasAll(filter.ExcludedMask, ColorBitFlags.W, ColorBitFlags.U,
            ColorBitFlags.B, ColorBitFlags.R, ColorBitFlags.G), Is.True);
    }

    // ── Color types (MULTI, MONO, HYBRID) ──

    [Test]
    public void GetFilter_MultiColorType()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.MULTI);

        var filter = ColorFilterHelper.GetFilter(boxes)!;
        // MULTI is a computed flag — not in RequiredMask, sets RequiresMultiColor instead
        Assert.That(Missing(filter.RequiredMask, ColorBitFlags.MULTI), Is.True);
        Assert.That(filter.RequiresMultiColor, Is.True);
        // RequiredMask must be 0 — no base colors selected
        Assert.That(filter.RequiredMask, Is.Zero);
        // ToSqlWhere must NOT generate "(ColorBits & 0) != 0" (always FALSE)
        var sql = filter.ToSqlWhere();
        Assert.That(sql, Does.Not.Contain("& 0"), "RequiredMask=0 must not generate useless condition");
    }

    [Test]
    public void GetFilter_MonoAndColorless()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.MONO);
        Set(boxes, ColorBitFlags.C);

        var filter = ColorFilterHelper.GetFilter(boxes)!;
        // MONO is a computed flag — not in RequiredMask, sets RequiresMonoColor instead
        Assert.That(Missing(filter.RequiredMask, ColorBitFlags.MONO), Is.True);
        Assert.That(filter.RequiresMonoColor, Is.True);
        // C is a real bit — stays in RequiredMask
        Assert.That(Has(filter.RequiredMask, ColorBitFlags.C), Is.True);
    }

    [Test]
    public void GetFilter_HybridWithWhite()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.HYBRID);
        Set(boxes, ColorBitFlags.W);

        var filter = ColorFilterHelper.GetFilter(boxes)!;
        // W is in RequiredMask, HYBRID is separate (RequiresHybrid)
        Assert.That(Has(filter.RequiredMask, ColorBitFlags.W), Is.True);
        Assert.That(Missing(filter.RequiredMask, ColorBitFlags.HYBRID), Is.True);
        Assert.That(filter.RequiresHybrid, Is.True);
    }

    // ── SQL generation via ToSqlWhere() ──

    [Test]
    public void ToSqlWhere_OR_Mode_GeneratesCorrectClause()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        Set(boxes, ColorBitFlags.U);

        var filter = ColorFilterHelper.GetFilter(boxes)!;
        var sql = filter.ToSqlWhere();
        Assert.That(sql, Does.Contain("ColorBits"));
        Assert.That(sql, Does.Contain("!= 0"));
    }

    [Test]
    public void ToSqlWhere_MultiColor_HasCorrectParentheses()
    {
        // Regression: '-' has higher precedence than '&' in SQLite.
        // Must generate "((col & 62) - 1)", NOT "(col & 62 - 1)" which parses as "col & 61".
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.MULTI);
        var filter = ColorFilterHelper.GetFilter(boxes)!;
        var sql = filter.ToSqlWhere()!;

        // Must contain the double-parenthesized subtraction pattern
        Assert.That(sql, Does.Contain("((ColorBits & 62) - 1)"),
            "MULTI SQL must parenthesize (col & mask) before subtracting 1");
        // Must NOT contain the broken pattern (col & 62 - 1) without inner parens
        Assert.That(sql, Does.Not.Contain("& 62 - 1"),
            "Must not have unparenthesized 'mask - 1' (SQLite precedence bug)");
    }

    [Test]
    public void ToSqlWhere_MonoColor_HasCorrectParentheses()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.MONO);
        var filter = ColorFilterHelper.GetFilter(boxes)!;
        var sql = filter.ToSqlWhere()!;

        Assert.That(sql, Does.Contain("((ColorBits & 62) - 1)"),
            "MONO SQL must parenthesize (col & mask) before subtracting 1");
        Assert.That(sql, Does.Not.Contain("& 62 - 1"),
            "Must not have unparenthesized 'mask - 1'");
    }

    [Test]
    public void ToSqlWhere_MultiColor_RequiredMaskZero_NoUselessCondition()
    {
        // MULTI-only → RequiredMask = 0. Must NOT generate "(ColorBits & 0) != 0".
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.MULTI);
        var filter = ColorFilterHelper.GetFilter(boxes)!;
        var sql = filter.ToSqlWhere()!;

        Assert.That(sql, Does.Not.Contain("& 0)"),
            "RequiredMask=0 must not generate useless '(col & 0)' condition");
    }

    [Test]
    public void ToSqlWhere_AND_Mode_GeneratesEquality()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        Set(boxes, ColorBitFlags.U);

        var filter = ColorFilterHelper.GetFilter(boxes, andMode: true)!;
        var sql = filter.ToSqlWhere();
        Assert.That(sql, Does.Contain("= 6")); // W|U = 2|4 = 6
    }

    [Test]
    public void ToSqlWhere_ExcludedColors_GeneratesTwoConditions()
    {
        // No optimization: always generate explicit conditions
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);

        var filter = ColorFilterHelper.GetFilter(boxes, onlyMode: true)!;
        var sql = filter.ToSqlWhere();
        // Two conditions: required OR + excluded NOT
        Assert.That(sql, Does.Contain("!= 0"));
        Assert.That(sql, Does.Contain("== 0"));
    }

    [Test]
    public void ToSqlWhere_OnlyRequired_NoExcluded_GeneratesOrClause()
    {
        // No onlyMode → no exclusions → single OR condition
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        Set(boxes, ColorBitFlags.U);

        var filter = ColorFilterHelper.GetFilter(boxes)!;
        var sql = filter.ToSqlWhere();
        Assert.That(sql, Is.EqualTo("(ColorBits & 6) != 0"));
    }

    [Test]
    public void ToSqlWhere_NoFullMaskOptimization()
    {
        // Full mask optimization was removed — it breaks with computed bits (MULTI/MONO/HYBRID).
        // A card {W} has ColorBits = W|MONO = 6, not W=2. Exact equality "ColorBits = 2" would reject it.
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);

        var filter = ColorFilterHelper.GetFilter(boxes, onlyMode: true)!;
        var sql = filter.ToSqlWhere();
        // Must NOT contain exact equality
        Assert.That(sql, Does.Not.Contain("ColorBits = "));
    }

    [Test]
    public void ToSqlWhere_IdentityMode_UsesIdentityBits()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);

        var filter = ColorFilterHelper.GetFilter(boxes, identityMode: true)!;
        var sql = filter.ToSqlWhere();
        Assert.That(sql, Does.Contain("IdentityBits"));
    }

    [Test]
    public void ToSqlWhere_EmptyFilter_ReturnsNull()
    {
        var filter = new MagicAssistant.Data.Model.ColorFilter();
        Assert.That(filter.IsEmpty, Is.True);
        Assert.That(filter.ToSqlWhere(), Is.Null);
    }

    // ── Bit mask arithmetic correctness ──

    [Test]
    public void SqlMask_OR_WhiteBlue_MatchesExpectedValue()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        Set(boxes, ColorBitFlags.U);

        var filter = ColorFilterHelper.GetFilter(boxes)!;
        Assert.That(filter.RequiredMask, Is.EqualTo(6)); // W=2 | U=4
    }

    [Test]
    public void SqlMask_AllColors_MatchesFullMask()
    {
        var boxes = EmptyBoxes();
        foreach (var flag in ColorFilterFlags.All)
            boxes[flag] = true;

        var filter = ColorFilterHelper.GetFilter(boxes)!;
        // MULTI, MONO, HYBRID are separate Requires* flags — excluded from RequiredMask.
        // C=1,W=2,U=4,B=8,R=16,G=32 → 63
        Assert.That(filter.RequiredMask, Is.EqualTo(63));
        Assert.That(filter.RequiresMultiColor, Is.True);
        Assert.That(filter.RequiresMonoColor, Is.True);
        Assert.That(filter.RequiresHybrid, Is.True);
    }

    // ── Edge cases ──

    [Test]
    public void GetFilter_ColorlessOnly_NoModifiers()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.C);

        var filter = ColorFilterHelper.GetFilter(boxes)!;
        Assert.That(Has(filter.RequiredMask, ColorBitFlags.C), Is.True);
        Assert.That(filter.ExcludedMask, Is.Zero);
    }

    [Test]
    public void GetFilter_ColorlessWithOnly_ExcludesOtherBaseColors()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.C);

        var filter = ColorFilterHelper.GetFilter(boxes, onlyMode: true)!;
        Assert.That(Has(filter.RequiredMask, ColorBitFlags.C), Is.True);
        // Only other BASE colors excluded (W,U,B,R,G). Computed flags NOT excluded.
        Assert.That(HasAll(filter.ExcludedMask, ColorBitFlags.W, ColorBitFlags.U, ColorBitFlags.B,
            ColorBitFlags.R, ColorBitFlags.G), Is.True);
        Assert.That(Missing(filter.ExcludedMask, ColorBitFlags.MULTI), Is.True);
        Assert.That(Missing(filter.ExcludedMask, ColorBitFlags.MONO), Is.True);
        Assert.That(Missing(filter.ExcludedMask, ColorBitFlags.HYBRID), Is.True);
    }

    [Test]
    public void GetFilter_FiveColorsAndMode()
    {
        // W AND U AND B AND R AND G (all 5 colors must be present)
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        Set(boxes, ColorBitFlags.U);
        Set(boxes, ColorBitFlags.B);
        Set(boxes, ColorBitFlags.R);
        Set(boxes, ColorBitFlags.G);

        var filter = ColorFilterHelper.GetFilter(boxes, andMode: true)!;
        Assert.That(filter.AndMode, Is.True);
        // Required mask = W|U|B|R|G = 2+4+8+16+32 = 62
        Assert.That(filter.RequiredMask, Is.EqualTo(62));
    }
}
