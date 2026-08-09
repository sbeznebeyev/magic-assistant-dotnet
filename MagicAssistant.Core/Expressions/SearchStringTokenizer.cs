using System;
using System.Collections.Generic;
using System.Text;

namespace MagicAssistant.Core.Expressions;

public class SearchStringTokenizer
{
    public enum TokenType { Text, Quoted, Regex, Abi, Not }
    public record SearchToken(string Value, TokenType Type);

    private string _text = "";
    private int _pos = 0;

    public void Init(string text)
    {
        _text = text ?? "";
        _pos = 0;
    }

    public SearchToken? NextToken()
    {
        SkipWhitespace();
        if (_pos >= _text.Length) return null;

        if (_text[_pos] == '!')
        {
            _pos++;
            return new SearchToken("", TokenType.Not);
        }

        if (_text[_pos] == '"')
        {
            var sb1 = new StringBuilder();
            _pos++;
            while (_pos < _text.Length && _text[_pos] != '"')
                sb1.Append(_text[_pos++]);
            _pos++; // consume closing quote
            return new SearchToken(sb1.ToString(), TokenType.Quoted);
        }

        if (_text.AsSpan(_pos).StartsWith("\\regex:"))
        {
            _pos += 7;
            var sb2 = new StringBuilder();
            while (_pos < _text.Length && !char.IsWhiteSpace(_text[_pos]) && _text[_pos] != '"')
                sb2.Append(_text[_pos++]);
            return new SearchToken(sb2.ToString(), TokenType.Regex);
        }

        var sb3 = new StringBuilder();
        while (_pos < _text.Length && !char.IsWhiteSpace(_text[_pos]) && _text[_pos] != '"')
            sb3.Append(_text[_pos++]);
        return new SearchToken(sb3.ToString(), TokenType.Text);
    }

    private void SkipWhitespace()
    {
        while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            _pos++;
    }
}
