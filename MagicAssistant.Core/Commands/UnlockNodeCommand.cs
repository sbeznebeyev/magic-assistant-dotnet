using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Unlock a collection node. Removes all restrictions.
/// </summary>
public record UnlockNodeCommand(int Id);

public class UnlockNodeCommandHandler
{
    private readonly ICollectionStore _collectionStore;

    public UnlockNodeCommandHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    /// <summary>
    /// Unlocks the node. Returns true if a row was updated.
    /// </summary>
    public async Task<bool> Handle(UnlockNodeCommand command, CancellationToken ct = default)
    {
        return await _collectionStore.UnlockAsync(command.Id, ct);
    }
}
