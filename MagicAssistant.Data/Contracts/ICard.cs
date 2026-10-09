using MagicAssistant.Data.Model;

namespace MagicAssistant.Data.Contracts;

public interface ICard
{
    string Name { get; }
    int Count { get; }
    Collection? Collection { get; }
    bool IsSideboard { get; }
    bool IsPhysical { get; }
}