using MagicAssistant.Core.Services.KnowledgeBaseSync;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Orchestrates full synchronization of a set from Scryfall into the knowledge base.
/// 
/// Steps:
/// 1. Fetch set card list from Scryfall API
/// 2. Map DTOs → MagicCard
/// 3. Download card images to local cache
/// 4. Persist cards to knowledge base (SQLite)
/// </summary>
public record SyncKBCommand(string SetCode, string[] Languages, bool DownloadImages = true);


/// <summary>
/// Final result of synchronization.
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public string SetCode { get; set; } = string.Empty;
    public string? SetName { get; set; }
    public int CardsDownloaded { get; set; }
    public int CardsPersisted { get; set; }
    public int ImagesDownloaded { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Thin handler that delegates sync orchestration to KnowledgeBaseSyncService.
/// Keeps the CQRS pattern intact while the heavy 6-phase logic lives in its own vertical slice.
/// </summary>
public class SyncKBCommandHandler
{
    private readonly IKnowledgeBaseSyncService _syncService;

    public SyncKBCommandHandler(IKnowledgeBaseSyncService syncService)
    {
        _syncService = syncService;
    }

    public Task<SyncResult> Handle(
        SyncKBCommand command,
        CancellationToken ct = default)
    {
        return _syncService.SyncSetAsync(command.SetCode, command.Languages, command.DownloadImages, ct);
    }
}
