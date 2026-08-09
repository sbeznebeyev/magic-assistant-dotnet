using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.DbModel;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Create a new folder node (NodeType=0) in the navigation tree.
/// Unlike decks, folders don't get a sideboard child.
/// </summary>
public record CreateFolderCommand(
    string Name,
    int? ParentId = null);

public class CreateFolderCommandHandler
{
    private readonly ICollectionStore _collectionStore;

    public CreateFolderCommandHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    /// <summary>
    /// Creates a new folder node (NodeType=0). Returns the created folder node.
    /// </summary>
    public async Task<Collection> Handle(CreateFolderCommand command, CancellationToken ct = default)
    {
        return await _collectionStore.CreateFolderAsync(command.Name, command.ParentId, ct);
    }
}
