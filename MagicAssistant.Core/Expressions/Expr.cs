using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MagicAssistant.Core.Expressions;

public enum Operation
{
    AND,
    OR,
    EQUALS,
    MATCHES,
    NOT,
    GE,   // >=
    LE,   // <=
    EQ    // ==
}

public abstract class Expr : IEquatable<Expr>
{
    // --- Constants ---

    public static readonly Expr True = new TrueExpr();
    public static readonly Expr False = new FalseExpr();
    public static readonly Expr Empty = new EmptyExpr();

    private sealed class TrueExpr : Expr
    {
        public override bool Evaluate(object? o) => true;
        public override object? GetFieldValue(object? o) => true;
        public override string ToString() => "true";
        public override Expr And(Expr b) => b;
        public override Expr Or(Expr b) => this;
        public override Expr Not() => False;
    }

    private sealed class FalseExpr : Expr
    {
        public override bool Evaluate(object? o) => false;
        public override object? GetFieldValue(object? o) => false;
        public override string ToString() => "false";
        public override Expr And(Expr b) => this;
        public override Expr Or(Expr b) => b;
        public override Expr Not() => True;
    }

    private sealed class EmptyExpr : Expr
    {
        public override bool Evaluate(object? o) => true;
        public override object? GetFieldValue(object? o) => true;
        public override string ToString() => "true";
        public override Expr And(Expr b) => b;
        public override Expr Or(Expr b) => b;
        public override Expr Not() => this;
    }

    public static Expr ValueOf(bool b) => b ? True : False;

    // Static helper to combine AND without virtual dispatch issues
    internal static Expr CombineAnd(Expr a, Expr b)
    {
        if (a == Empty || a == True) return b;
        if (a == False) return False;
        if (b == Empty || b == True) return a;
        if (b == False) return False;
        return new BinaryExpr(a, Operation.AND, b);
    }

    // --- Abstract methods ---

    public virtual bool Evaluate(object? o) => false;
    public virtual object? GetFieldValue(object? o) => null;

    public virtual Expr And(Expr b)
    {
        if (b == Empty || b == True) return this;
        if (b == False) return False;
        return new BinaryExpr(this, Operation.AND, b);
    }

    public virtual Expr Or(Expr b)
    {
        if (b == Empty) return this;
        if (b == True) return True;
        if (b == False) return this;
        return new BinaryExpr(this, Operation.OR, b);
    }

    public virtual Expr Not() => new NotExpr(this);

    public override string ToString() => "expr";

    // --- Equality ---

    public override bool Equals(object? obj) => Equals(obj as Expr);

    public virtual bool Equals(Expr? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other == null) return false;
        return GetType() == other.GetType();
    }

    public override int GetHashCode() => GetType().GetHashCode();
}
