using MagicAssistant.Data.Model;

namespace MagicAssistant.Data.Contracts;

public interface ICard
{
    string Name { get; }
    int Count { get; }
    Collection? Collection { get; }
    bool IsSideboard { get; }
    int UniqueCount { get; }
    bool IsPhysical { get; }
}