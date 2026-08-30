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
/// Payload for recording an Add/Sub card operation in the LastActions history:
/// the target collection and the card with its count and direction.
/// </summary>
public record CardOperationRecord(
    int TargetCollectionId,
    MagicCard Card,
    int Count,
    LastActionOperationEnum Operation);

/// <summary>
/// A single entry in the LastActions queue: which card, how many copies, in which direction,
/// and in which collection (Location) the operation was performed.
/// The queue is a rolling history that is never reset, so each entry carries its own location.
/// </summary>
public record LastActionItem(
    int LocationId,
    string Location,
    MagicCard Card,
    int Count,
    LastActionOperationEnum Operation);
