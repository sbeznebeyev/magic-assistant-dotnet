using MagicAssistant.Data.Model;

namespace MagicAssistant.Data.Contracts;

/// <summary>
/// Interface for modifiable cards (physical instances that can be edited).
/// Mirrors Java ICardModifiable.
/// </summary>
public interface ICardModifiable : IMagicCard
{
    bool IsOwn { get; set; }
    new Collection? Collection { get; set; }
    string? Comment { get; set; }
}