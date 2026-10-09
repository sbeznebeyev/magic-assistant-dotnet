using MagicAssistant.Data.Model;

namespace MagicAssistant.Data.Contracts;

/// <summary>
/// Interface for physical card instances in collections/decks.
/// Mirrors Java IMagicCardPhysical.
/// </summary>
public interface IMagicCardPhysical : ICardModifiable
{
    MagicCard Card { get; }
    void SetCollection(Collection? collection);
    bool HasSpecialTag(string tag);
    int GetForTradeCount();
}
