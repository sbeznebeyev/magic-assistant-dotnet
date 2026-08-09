namespace MagicAssistant.Core.Expressions;

public class Value : Expr
{
    private readonly string _value;

    public Value(string value)
    {
        _value = value ?? "";
    }

    public override string ToString() => $"'{_value}'";

    public override object? GetFieldValue(object? o) => _value;

    protected string ValueString => _value;

    public override bool Equals(Expr? other) => other is Value otherValue && _value == otherValue._value;
}
