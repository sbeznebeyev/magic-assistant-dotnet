using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Services.DeckImportExport;

/// <summary>
/// Vertical slice: deck import/export operations.
/// Handles XML file parsing and serialization for Magic Assistant format.
/// 
/// Replaces DataManager.ImportCardsToCollectionAsync, ExportDeckToXmlAsync.
/// Wraps existing ImportDeckCommandHandler and ExportDeckCommandHandler logic.
/// </summary>
public interface IDeckImportExportService
{
    /// <summary>
    /// Import a deck from a Magic Assistant XML file.
    /// Creates a new deck node and adds imported cards to it.
    /// </summary>
    Task<ImportDeckResult> ImportFromFileAsync(
        string filePath, int? parentId = null, CancellationToken ct = default);

    /// <summary>
    /// Import cards from Magic Assistant XML content into an existing collection.
    /// Does not create a new deck — adds to existing collection.
    /// </summary>
    Task<int> ImportToCollectionAsync(
        int collectionId, string xmlContent, CancellationToken ct = default);

    /// <summary>
    /// Export a deck to Magic Assistant XML format.
    /// Returns the XML string.
    /// </summary>
    Task<string> ExportDeckToXmlAsync(int deckId, CancellationToken ct = default);

    /// <summary>
    /// Export a deck to Magic Assistant XML and save to file.
    /// </summary>
    Task<ExportDeckResult> ExportToFileAsync(
        int deckId, string filePath, CancellationToken ct = default);

    /// <summary>
    /// Import a legacy Magic Assistant XML file where card IDs are Gatherer IDs
    /// (integers) and set names are full names ("Core Set 2019") instead of abbreviations.
    /// Resolves IDs and set names against the knowledge base before importing.
    /// </summary>
    Task<ImportDeckResult> ImportLegacyFromFileAsync(
        string filePath, int? parentId = null, CancellationToken ct = default);

    /// <summary>
    /// Import legacy Magic Assistant XML content into an existing collection.
    /// Resolves Gatherer IDs and full set names against the knowledge base.
    /// </summary>
    Task<int> ImportLegacyToCollectionAsync(
        int collectionId, string xmlContent, CancellationToken ct = default);
}
