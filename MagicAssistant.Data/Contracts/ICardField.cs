using System;
using System.Collections.Generic;
using System.Text;

namespace MagicAssistant.Data.Contracts;

public interface ICardField
{
    string Name { get; }
    Type ValueType { get; }
    object? Get(ICardSimple card);
    void Set(ICardSimple card, object? value);
}