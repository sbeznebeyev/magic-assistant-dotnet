using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Commands;

public record CreateDeckCommand(string Name, int? ParentId = null);

public class CreateDeckCommandHandler
{
    private readonly ICollectionStore _collectionStore;

    public CreateDeckCommandHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    public async Task<Collection> Handle(CreateDeckCommand command, CancellationToken ct = default)
    {
        return await _collectionStore.CreateDeckAsync(command.Name, command.ParentId, ct);
    }
}
