using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Rename a collection node (deck, folder, or collection) in the navigation tree.
/// For decks: also renames the sideboard child to "{newName} (sb)".
/// </summary>
public record RenameNodeCommand(int Id, string NewName);

public class RenameNodeCommandHandler
{
    private readonly ICollectionStore _collectionStore;

    public RenameNodeCommandHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    /// <summary>
    /// Renames the node and its sideboard child (if any). Returns true if a row was updated.
    /// </summary>
    public async Task<bool> Handle(RenameNodeCommand command, CancellationToken ct = default)
    {
        return await _collectionStore.RenameAsync(command.Id, command.NewName, ct);
    }
}
