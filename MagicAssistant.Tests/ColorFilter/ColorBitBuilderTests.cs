using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.ColorFilter;

[TestFixture]
public class ColorBitBuilderTests
{
    private static bool Has(int bits, ColorBitFlags flag) => (bits & (int)flag) != 0;
    private static bool HasAll(int bits, params ColorBitFlags[] flags)
        => flags.All(f => (bits & (int)f) != 0);
    private static bool Missing(int bits, ColorBitFlags flag) => (bits & (int)flag) == 0;

    // ── Basic color bits ──

    [Test]
    public void Build_SingleColor_White()
    {
        var bits = ColorBitBuilder.Build(["W"], "{W}");
        Assert.That(Has(bits, ColorBitFlags.W), Is.True);
        Assert.That(Has(bits, ColorBitFlags.MONO), Is.True);
        Assert.That(Missing(bits, ColorBitFlags.MULTI), Is.True);
        Assert.That(Missing(bits, ColorBitFlags.C), Is.True);
    }

    [Test]
    public void Build_SingleColor_EachBasicColor()
    {
        var tests = new (string[] colors, string cost, ColorBitFlags flag)[]
        {
            (["W"], "{W}", ColorBitFlags.W),
            (["U"], "{U}", ColorBitFlags.U),
            (["B"], "{B}", ColorBitFlags.B),
            (["R"], "{R}", ColorBitFlags.R),
            (["G"], "{G}", ColorBitFlags.G),
        };

        foreach (var (colors, cost, expected) in tests)
        {
            var bits = ColorBitBuilder.Build(colors, cost);
            Assert.That(Has(bits, expected), Is.True, $"Missing flag {expected}");
            Assert.That(Has(bits, ColorBitFlags.MONO), Is.True, $"Missing MONO for {expected}");
        }
    }

    [Test]
    public void Build_MultiColor_TwoColors()
    {
        var bits = ColorBitBuilder.Build(["W", "U"], "{W}{U}");
        Assert.That(Has(bits, ColorBitFlags.W), Is.True);
        Assert.That(Has(bits, ColorBitFlags.U), Is.True);
        Assert.That(Has(bits, ColorBitFlags.MULTI), Is.True);
        Assert.That(Missing(bits, ColorBitFlags.MONO), Is.True);
        Assert.That(Missing(bits, ColorBitFlags.C), Is.True);
    }

    [Test]
    public void Build_MultiColor_FiveColors()
    {
        var bits = ColorBitBuilder.Build(["W", "U", "B", "R", "G"], "{W}{U}{B}{R}{G}");
        Assert.That(HasAll(bits, ColorBitFlags.W, ColorBitFlags.U, ColorBitFlags.B,
            ColorBitFlags.R, ColorBitFlags.G, ColorBitFlags.MULTI), Is.True);
    }

    [Test]
    public void Build_Colorless_NoColors()
    {
        var bits = ColorBitBuilder.Build(Array.Empty<string>(), "");
        Assert.That(Has(bits, ColorBitFlags.C), Is.True);
        Assert.That(Missing(bits, ColorBitFlags.MONO), Is.True);
        Assert.That(Missing(bits, ColorBitFlags.MULTI), Is.True);
    }

    [Test]
    public void Build_Colorless_NullColors()
    {
        var bits = ColorBitBuilder.Build(null, null);
        Assert.That(Has(bits, ColorBitFlags.C), Is.True);
    }

    // ── Hybrid detection ──
    // HYBRID is mutually exclusive with MONO/MULTI/C (matching Java Colors.getColorType()).

    [Test]
    public void Build_Hybrid_WithSlashInManaCost()
    {
        var bits = ColorBitBuilder.Build(["W", "U"], "{W/U}");
        // Base color bits still set from colors array
        Assert.That(HasAll(bits, ColorBitFlags.W, ColorBitFlags.U), Is.True);
        // HYBRID replaces MULTI (mutually exclusive)
        Assert.That(Has(bits, ColorBitFlags.HYBRID), Is.True);
        Assert.That(Missing(bits, ColorBitFlags.MULTI), Is.True);
        Assert.That(Missing(bits, ColorBitFlags.MONO), Is.True);
    }

    [Test]
    public void Build_Hybrid_MonoHybrid()
    {
        var bits = ColorBitBuilder.Build(["W"], "{W/2}");
        // Base color bit still set from colors array
        Assert.That(Has(bits, ColorBitFlags.W), Is.True);
        // HYBRID replaces MONO (mutually exclusive)
        Assert.That(Has(bits, ColorBitFlags.HYBRID), Is.True);
        Assert.That(Missing(bits, ColorBitFlags.MONO), Is.True);
        Assert.That(Missing(bits, ColorBitFlags.MULTI), Is.True);
    }

    [Test]
    public void Build_NoHybrid_RegularCost()
    {
        var bits = ColorBitBuilder.Build(["W"], "{W}{2}");
        Assert.That(Missing(bits, ColorBitFlags.HYBRID), Is.True);
    }

    // ── Identity bits ──

    [Test]
    public void Build_Identity_IncludesOracleTextColors()
    {
        var bits = ColorBitBuilder.Build(null, "{W}", ["W", "R"]);
        Assert.That(HasAll(bits, ColorBitFlags.W, ColorBitFlags.R, ColorBitFlags.MULTI), Is.True);
    }

    [Test]
    public void Build_Identity_ColorlessIgnored()
    {
        var bits = ColorBitBuilder.Build(null, null, Array.Empty<string>());
        Assert.That(Missing(bits, ColorBitFlags.C), Is.True);
    }

    [Test]
    public void Build_Identity_SingleColor()
    {
        var bits = ColorBitBuilder.Build(null, "{W}", ["W"]);
        Assert.That(HasAll(bits, ColorBitFlags.W, ColorBitFlags.MONO), Is.True);
        Assert.That(Missing(bits, ColorBitFlags.C), Is.True);
    }

    // ── Mutual exclusivity ──

    [Test]
    public void Build_MONO_and_MULTI_MutuallyExclusive()
    {
        var mono = ColorBitBuilder.Build(["W"], "{W}");
        Assert.That(Has(mono, ColorBitFlags.MONO), Is.True);
        Assert.That(Missing(mono, ColorBitFlags.MULTI), Is.True);

        var multi = ColorBitBuilder.Build(["W", "U"], "{W}{U}");
        Assert.That(Has(multi, ColorBitFlags.MULTI), Is.True);
        Assert.That(Missing(multi, ColorBitFlags.MONO), Is.True);
    }

    [Test]
    public void Build_C_and_MONO_MutuallyExclusive()
    {
        var colorless = ColorBitBuilder.Build(Array.Empty<string>(), "");
        Assert.That(Has(colorless, ColorBitFlags.C), Is.True);
        Assert.That(Missing(colorless, ColorBitFlags.MONO), Is.True);

        var mono = ColorBitBuilder.Build(["W"], "{W}");
        Assert.That(Has(mono, ColorBitFlags.MONO), Is.True);
        Assert.That(Missing(mono, ColorBitFlags.C), Is.True);
    }
}
