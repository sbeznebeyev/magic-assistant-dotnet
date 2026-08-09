using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Lock a collection node (deck, folder, collection, sideboard).
/// Locked nodes cannot be deleted or renamed.
/// For Collection/Deck/Sideboard: card operations (add/remove/update) are also blocked.
/// </summary>
public record LockNodeCommand(int Id);

public class LockNodeCommandHandler
{
    private readonly ICollectionStore _collectionStore;

    public LockNodeCommandHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    /// <summary>
    /// Locks the node. Returns true if a row was updated.
    /// </summary>
    public async Task<bool> Handle(LockNodeCommand command, CancellationToken ct = default)
    {
        return await _collectionStore.LockAsync(command.Id, ct);
    }
}
