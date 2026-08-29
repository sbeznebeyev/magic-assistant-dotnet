using MagicAssistant.Data.Model;

namespace MagicAssistant.App.Shared;

/// <summary>
/// Type of a card action recorded in the LastActions queue.
/// </summary>
public enum LastActionOperationEnum
{
    /// <summary>Cards were added to the active location.</summary>
    Add,

    /// <summary>Cards were removed from the active location.</summary>
    Sub,
}

/// <summary>
/// A single entry in the LastActions queue: which card, how many copies, and in which direction.
/// </summary>
public record LastActionItem(
    MagicCard Card,
    int Count,
    LastActionOperationEnum Operation);
