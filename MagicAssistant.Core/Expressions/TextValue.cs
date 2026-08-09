using System;
using System.Text.RegularExpressions;

namespace MagicAssistant.Core.Expressions;

public class TextValue : Value
{
    public bool WordBoundary { get; set; } = true;
    public bool CaseSensitive { get; set; } = false;
    public bool IsRegex { get; set; } = false;

    private Regex? _cachedPattern;

    public TextValue(string value, bool wordBoundary, bool caseSensitive, bool isRegex)
        : base(value)
    {
        WordBoundary = wordBoundary;
        CaseSensitive = caseSensitive;
        IsRegex = isRegex;
    }

    public Regex GetPattern()
    {
        if (_cachedPattern != null) return _cachedPattern;

        var options = new RegexOptions();
        if (!CaseSensitive)
            options |= RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        string patternString = ValueString;

        if (IsRegex)
        {
            _cachedPattern = new Regex(patternString, options);
        }
        else
        {
            // Escape literal text for regex usage
            var escaped = Regex.Escape(patternString);
            
            if (WordBoundary)
            {
                patternString = $@"\b{escaped}\b";
            }
            else
            {
                patternString = escaped;
            }
            
            _cachedPattern = new Regex(patternString, options);
        }

        return _cachedPattern;
    }

    public override bool Equals(Expr? other)
    {
        if (other is not TextValue textOther) return false;
        return base.Equals(textOther) 
            && WordBoundary == textOther.WordBoundary
            && CaseSensitive == textOther.CaseSensitive
            && IsRegex == textOther.IsRegex;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(base.GetHashCode());
        hash.Add(WordBoundary);
        hash.Add(CaseSensitive);
        hash.Add(IsRegex);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Gets the raw string value of this text pattern.
    /// </summary>
    public string GetString() => ValueString;
}
