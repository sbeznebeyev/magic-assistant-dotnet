using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Expressions;

public class BinaryExpr : Expr
{
    public Expr Left { get; }
    public Operation Op { get; }
    public Expr Right { get; }

    public BinaryExpr(Expr left, Operation op, Expr right)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Op = op;
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public override string ToString()
    {
        if (Op == Operation.NOT)
            return $"NOT ({Left})";
        return $"{Left} {Op} {Right}";
    }

    public override bool Evaluate(object? o)
    {
        switch (Op)
        {
            case Operation.AND:
                return Left.Evaluate(o) && Right.Evaluate(o);
            
            case Operation.OR:
                return Left.Evaluate(o) || Right.Evaluate(o);
            
            case Operation.EQUALS:
            {
                var x = Left.GetFieldValue(o);
                var y = Right.GetFieldValue(o);
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                return Equals(x, y);
            }
            
            case Operation.MATCHES:
                return EvaluateMatches(o);
            
            case Operation.EQ:
            case Operation.LE:
            case Operation.GE:
            {
                var x = Left.GetFieldValue(o);
                var y = Right.GetFieldValue(o);
                if (x == null && y == null) return true;
                
                string sx = x?.ToString() ?? "0";
                string sy = y?.ToString() ?? "0";

                if (sx == sy) return true;

                if (double.TryParse(sx, out double dx) && double.TryParse(sy, out double dy))
                {
                    return Op switch
                    {
                        Operation.EQ => Math.Abs(dx - dy) < 0.001,
                        Operation.GE => dx >= dy,
                        Operation.LE => dx <= dy,
                        _ => false
                    };
                }

                throw new InvalidCastException($"Cannot perform numeric comparison on {sx} and {sy}");
            }
            
            default:
                throw new NotSupportedException($"Operation {Op} not implemented in evaluate");
        }
    }

    private bool EvaluateMatches(object? o)
    {
        var x = Left.GetFieldValue(o);
        var y = Right.GetFieldValue(o);
        if (x == null && y == null) return true;
        if (x == null || y == null) return false;

        // Card field matching: use CardMatcher with full TextValue flags
        if (Left is CardFieldExpr { Field: var field } && o is IMagicCard card && Right is TextValue textValue)
        {
            return CardMatcher.Matches(card, field, textValue);
        }

        // Fallback: regex on string representations
        string tx = x.ToString() ?? "";
        if (y is TextValue tv)
        {
            return tv.GetPattern().IsMatch(tx);
        }

        string ty = y?.ToString() ?? "";
        return Regex.IsMatch(tx, ty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    // Static factories mirror Java convenience methods
    public static Expr FieldEquals(MagicCardField field, string value)
        => new BinaryExpr(new CardFieldExpr(field), Operation.EQUALS, new Value(value));

    public static Expr FieldMatches(MagicCardField field, string? value)
    {
        if (string.IsNullOrEmpty(value)) return Expr.Empty;
        return new BinaryExpr(new CardFieldExpr(field), Operation.MATCHES, new TextValue(value!, false, false, false));
    }

    public static Expr FieldMatches(MagicCardField? field, string? value)
    {
        if (field == null || string.IsNullOrEmpty(value)) return Expr.Empty;
        return new BinaryExpr(new CardFieldExpr(field.Value), Operation.MATCHES, new TextValue(value, false, false, false));
    }

    public static Expr FieldOp(MagicCardField field, Operation op, string value)
        => new BinaryExpr(new CardFieldExpr(field), op, new Value(value));

    public static Expr FieldInt(MagicCardField field, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Expr.True;

        if (value == ">= 0") return Expr.True;

        var trimmed = value.Trim();
        if (trimmed.StartsWith(">="))
            return FieldOp(field, Operation.GE, trimmed[2..].Trim());
        if (trimmed.StartsWith("<="))
            return FieldOp(field, Operation.LE, trimmed[2..].Trim());
        if (trimmed.StartsWith("=="))
            return FieldOp(field, Operation.EQ, trimmed[2..].Trim());
        if (trimmed.StartsWith("="))
            return FieldOp(field, Operation.EQ, trimmed[1..].Trim());
        if (trimmed == "0") return Expr.True;

        return FieldOp(field, Operation.EQ, trimmed);
    }

    public static Expr TextSearch(MagicCardField? field, string text)
    {
        if (field == null || string.IsNullOrWhiteSpace(text)) return Expr.Empty;

        var tokenizer = new SearchStringTokenizer();
        tokenizer.Init(text);
        SearchStringTokenizer.SearchToken? token;
        Expr result = Expr.Empty;

        while ((token = tokenizer.NextToken()) != null)
        {
            Expr current;
            if (token.Type == SearchStringTokenizer.TokenType.Not)
            {
                var next = tokenizer.NextToken();
                if (next == null) break;
                current = MagicCardFilter.TokenSearch(field.Value, next).Not();
            }
            else
            {
                current = MagicCardFilter.TokenSearch(field.Value, token);
            }

            result = Expr.CombineAnd(result, current);
        }
        return result;
    }

    /// <summary>
    /// Gets the ICardField from a CardFieldExpr (for EvaluateMatches delegation).
    /// </summary>
    public static MagicCardField? GetFieldFromExpr(Expr expr)
    {
        return expr is CardFieldExpr cfe ? cfe.Field : null;
    }

    public override bool Equals(Expr? other)
    {
        if (other is not BinaryExpr b) return false;
        return Op == b.Op && Equals(Left, b.Left) && Equals(Right, b.Right);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Op);
        hash.Add(Left);
        hash.Add(Right);
        return hash.ToHashCode();
    }
}
