using MagicAssistant.Core.Commands;
using MagicAssistant.Core.Queries;
using MagicAssistant.Data.DbModel;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Core.Services.DeckManagement;

/// <summary>
/// Vertical slice: deck and collection management.
/// Delegates all operations to CQRS command/query handlers — never touches ICollectionStore directly.
/// </summary>
public class DeckManagementService : IDeckManagementService
{
    private readonly CreateNodeCommandHandler _createNodeHandler;
    private readonly DeleteNodeCommandHandler _deleteNodeHandler;
    private readonly RenameNodeCommandHandler _renameNodeHandler;
    private readonly LockNodeCommandHandler _lockNodeHandler;
    private readonly UnlockNodeCommandHandler _unlockNodeHandler;
    private readonly GetNodesTreeQueryHandler _navigationTreeHandler;
    private readonly GetNodeByIdQueryHandler _getNodeByIdHandler;
    private readonly GetNodesBranchQueryHandler _getNodesBranchHandler;

    public DeckManagementService(
        CreateNodeCommandHandler createNodeHandler,
        DeleteNodeCommandHandler deleteNodeHandler,
        RenameNodeCommandHandler renameNodeHandler,
        LockNodeCommandHandler lockNodeHandler,
        UnlockNodeCommandHandler unlockNodeHandler,
        GetNodesTreeQueryHandler navigationTreeHandler,
        GetNodeByIdQueryHandler getNodeByIdHandler,
        GetNodesBranchQueryHandler getNodesBranchHandler)
    {
        _createNodeHandler = createNodeHandler;
        _deleteNodeHandler = deleteNodeHandler;
        _renameNodeHandler = renameNodeHandler;
        _lockNodeHandler = lockNodeHandler;
        _unlockNodeHandler = unlockNodeHandler;
        _navigationTreeHandler = navigationTreeHandler;
        _getNodeByIdHandler = getNodeByIdHandler;
        _getNodesBranchHandler = getNodesBranchHandler;
    }

    /// <inheritdoc />
    public async Task<int> CreateNodeAsync(int? parentId, string name, NodeType nodeType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        return await _createNodeHandler.Handle(new CreateNodeCommand(parentId, name.Trim(), nodeType), ct);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDeckAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            throw new ArgumentException("Invalid ID.", nameof(id));

        return await _deleteNodeHandler.Handle(new DeleteNodeCommand(id), ct);
    }

    /// <inheritdoc />
    public async Task<bool> RenameAsync(int id, string newName, CancellationToken ct = default)
    {
        if (id <= 0)
            throw new ArgumentException("Invalid ID.", nameof(id));
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("New name is required.", nameof(newName));

        return await _renameNodeHandler.Handle(new RenameNodeCommand(id, newName.Trim()), ct);
    }

    /// <inheritdoc />
    public Task<Dictionary<int, NavigatorNodeBase>> GetAllNodesAsync(CancellationToken ct = default)
    {
        return _navigationTreeHandler.HandleAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Collection?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            throw new ArgumentException("Invalid ID.", nameof(id));

        return await _getNodeByIdHandler.Handle(new GetNodeByIdQuery(id), ct);
    }

    /// <inheritdoc />
    public async Task<bool> LockAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            throw new ArgumentException("Invalid ID.", nameof(id));

        return await _lockNodeHandler.Handle(new LockNodeCommand(id), ct);
    }

    /// <inheritdoc />
    public async Task<bool> UnlockAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            throw new ArgumentException("Invalid ID.", nameof(id));

        return await _unlockNodeHandler.Handle(new UnlockNodeCommand(id), ct);
    }

    /// <inheritdoc />
    public async Task<NavigatorNodeBase?> GetNodesBranchAsync(int collectionId, CancellationToken ct = default)
    {
        if (collectionId <= 0)
            throw new ArgumentException("Invalid ID.", nameof(collectionId));

        return await _getNodesBranchHandler.HandleAsync(new GetNodesBranchQuery(collectionId), ct);
    }
}
