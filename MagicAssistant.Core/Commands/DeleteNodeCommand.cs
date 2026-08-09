using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Delete a collection node (deck, folder, or collection) from the navigation tree.
/// For decks: also deletes sideboard child and all cards via ON DELETE CASCADE.
/// </summary>
public record DeleteNodeCommand(int Id);

public class DeleteNodeCommandHandler
{
    private readonly ICollectionStore _collectionStore;

    public DeleteNodeCommandHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    /// <summary>
    /// Deletes the node and its sideboard child (if any). Returns true if a row was deleted.
    /// </summary>
    public async Task<bool> Handle(DeleteNodeCommand command, CancellationToken ct = default)
    {
        return await _collectionStore.DeleteAsync(command.Id, ct);
    }
}
