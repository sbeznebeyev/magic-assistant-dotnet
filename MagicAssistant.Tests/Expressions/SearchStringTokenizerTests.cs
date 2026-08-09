using MagicAssistant.Core.Expressions;

namespace MagicAssistant.Tests.Expressions;

[TestFixture]
public class SearchStringTokenizerTests
{
    private readonly SearchStringTokenizer _t = new();

    [Test]
    public void TextTokens_SplitByWhitespace()
    {
        _t.Init("lightning bolt");
        Assert.That(_t.NextToken()!.Value, Is.EqualTo("lightning"));
        Assert.That(_t.NextToken()!.Value, Is.EqualTo("bolt"));
        Assert.That(_t.NextToken(), Is.Null);
    }

    [Test]
    public void QuotedTokens_PreserveSpaces()
    {
        _t.Init("\"lightning bolt\" remaining");
        var quoted = _t.NextToken();
        Assert.That(quoted!.Type, Is.EqualTo(SearchStringTokenizer.TokenType.Quoted));
        Assert.That(quoted.Value, Is.EqualTo("lightning bolt"));
        Assert.That(_t.NextToken()!.Value, Is.EqualTo("remaining"));
    }

    [Test]
    public void RegexTokens_ExtractPattern()
    {
        _t.Init("\\regex:hello\\sworld rest");
        var t1 = _t.NextToken();
        Assert.That(t1!.Type, Is.EqualTo(SearchStringTokenizer.TokenType.Regex));
        Assert.That(t1.Value, Is.EqualTo("hello\\sworld"));
    }

    [Test]
    public void NotTokens_PrecedeNextWord()
    {
        _t.Init("!lightning bolt");
        Assert.That(_t.NextToken()!.Type, Is.EqualTo(SearchStringTokenizer.TokenType.Not));
        Assert.That(_t.NextToken()!.Value, Is.EqualTo("lightning"));
    }

    [Test]
    public void MixedTokens_ReturnInOrder()
    {
        _t.Init("!word1 \"quoted\" \\regex:pat word2");
        var types = new List<SearchStringTokenizer.TokenType>();
        SearchStringTokenizer.SearchToken? token;
        while ((token = _t.NextToken()) != null)
            types.Add(token!.Type);

        Assert.That(types, Is.EqualTo(new[]
        {
            SearchStringTokenizer.TokenType.Not,
            SearchStringTokenizer.TokenType.Text,
            SearchStringTokenizer.TokenType.Quoted,
            SearchStringTokenizer.TokenType.Regex,
            SearchStringTokenizer.TokenType.Text,
        }));
    }

    [Test]
    public void NullAndEmpty_ReturnNoTokens()
    {
        _t.Init(null!);
        Assert.That(_t.NextToken(), Is.Null);
        _t.Init("");
        Assert.That(_t.NextToken(), Is.Null);
    }
}
