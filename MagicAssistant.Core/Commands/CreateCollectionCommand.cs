using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.DbModel;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Create a new collection or folder in the navigation tree.
/// Mirrors Java: NewCardCollectionWizard + CardOrganizer creation
/// </summary>
public record CreateCollectionCommand(
    string Name,
    int? ParentId = null);

public class CreateCollectionCommandHandler
{
    private readonly ICollectionStore _collectionStore;

    public CreateCollectionCommandHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    /// <summary>
    /// Creates a new collection node (NodeType=1). Unlike decks, collections don't get sideboard.
    /// Returns the created collection node.
    /// </summary>
    public async Task<Collection> Handle(CreateCollectionCommand command, CancellationToken ct = default)
    {
        return await _collectionStore.CreateCollectionAsync(command.Name, command.ParentId, ct);
    }
}
