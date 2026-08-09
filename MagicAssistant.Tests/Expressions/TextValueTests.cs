using MagicAssistant.Core.Expressions;

namespace MagicAssistant.Tests.Expressions;

[TestFixture]
public class TextValueTests
{
    [Test]
    public void GetPattern_WithWordBoundary_MatchesWholeWord()
    {
        var pattern = new TextValue("test", true, false, false).GetPattern();
        Assert.That(pattern.IsMatch("test"), Is.True);
        Assert.That(pattern.IsMatch("testing"), Is.False);  // word boundary blocks
        Assert.That(pattern.IsMatch("a test b"), Is.True);

        // Case insensitive by default
        Assert.That(pattern.IsMatch("TEST"), Is.True);
    }

    [Test]
    public void GetPattern_WithoutWordBoundary_MatchesSubstring()
    {
        var pattern = new TextValue("test", false, false, false).GetPattern();
        Assert.That(pattern.IsMatch("testing"), Is.True);
    }

    [Test]
    public void GetPattern_CaseSensitive_RespectsCase()
    {
        var pattern = new TextValue("test", true, true, false).GetPattern();
        Assert.That(pattern.IsMatch("test"), Is.True);
        Assert.That(pattern.IsMatch("TEST"), Is.False);
    }

    [Test]
    public void GetPattern_RegexMode_ParsesAsRegex()
    {
        var pattern = new TextValue("t.st", true, false, true).GetPattern();
        Assert.That(pattern.IsMatch("test"), Is.True);
        Assert.That(pattern.IsMatch("tast"), Is.True);
    }

    [Test]
    public void GetPattern_EscapesSpecialChars()
    {
        var pattern = new TextValue("hello.world", true, false, false).GetPattern();
        Assert.That(pattern.IsMatch("hello.world"), Is.True);
        Assert.That(pattern.IsMatch("helloXworld"), Is.False); // dot is literal, not wildcard
    }

    [Test]
    public void Equals_CompareAllOptions()
    {
        var tv1 = new TextValue("test", true, false, false);
        var tv2 = new TextValue("test", true, false, false);
        var tv3 = new TextValue("test", false, false, false); // different wordBoundary

        Assert.That(tv1.Equals(tv2), Is.True);
        Assert.That(tv1.Equals(tv3), Is.False);
        Assert.That(tv1.GetHashCode(), Is.EqualTo(tv2.GetHashCode()));
    }
}
