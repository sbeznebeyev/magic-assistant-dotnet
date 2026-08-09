using MagicAssistant.Core.Commands;

namespace MagicAssistant.Core.Services.KnowledgeBaseSync;

/// <summary>
/// Vertical slice: synchronizes the knowledge base from Scryfall.
/// Handles set resolution, card download, DTO→Model mapping, image caching, and persistence.
/// 
/// Replaces DataManager.SyncSetAsync().
/// </summary>
public interface IKnowledgeBaseSyncService
{
    /// <summary>
    /// Synchronize a single set from Scryfall into the knowledge base.
    /// 6-phase orchestration: resolve → download → map → images → save → done.
    /// Progress is tracked via SyncSetStateRegistry for UI polling.
    /// </summary>
    Task<SyncResult> SyncSetAsync(string setAbbreviation, string[] languages, bool downloadImages = true, CancellationToken ct = default);
}
