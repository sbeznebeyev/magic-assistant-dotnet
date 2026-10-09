using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Unified command to create a navigation node (Folder, Collection, or Deck).
/// If NodeType == Deck, a sideboard child is also created automatically.
/// Returns the ID of the created node only (sideboard ID is not returned).
/// </summary>
public record CreateNodeCommand(int? ParentId, string Name, NodeType NodeType);

public class CreateNodeCommandHandler
{
    private readonly ICollectionStore _collectionStore;

    public CreateNodeCommandHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    /// <summary>
    /// Creates the node and returns its ID.
    /// For Deck: also creates a sideboard child (not returned).
    /// </summary>
    public async Task<int> Handle(CreateNodeCommand command, CancellationToken ct = default)
    {
        return command.NodeType switch
        {
            NodeType.Folder =>
                await CreateFolderAsync(command.ParentId, command.Name, ct),
            NodeType.Collection =>
                await CreateCollectionAsync(command.ParentId, command.Name, ct),
            NodeType.Deck =>
                await CreateDeckAsync(command.ParentId, command.Name, ct),
            _ => throw new NotSupportedException($"Node type {command.NodeType} is not supported for creation.")
        };
    }

    private async Task<int> CreateFolderAsync(int? parentId, string name, CancellationToken ct)
    {
        var created = await _collectionStore.CreateFolderAsync(name, parentId, ct);
        return created.Id;
    }

    private async Task<int> CreateCollectionAsync(int? parentId, string name, CancellationToken ct)
    {
        var created = await _collectionStore.CreateCollectionAsync(name, parentId, ct);
        return created.Id;
    }

    private async Task<int> CreateDeckAsync(int? parentId, string name, CancellationToken ct)
    {
        var created = await _collectionStore.CreateDeckAsync(name, parentId, ct);
        return created.Id;
    }
}
