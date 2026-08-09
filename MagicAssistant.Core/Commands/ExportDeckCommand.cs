using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.IO.xml;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Commands;

// ═══════════════════════════════════════════════════════════
//  Export
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Export a deck to a Magic Assistant XML file.
/// </summary>
public record ExportDeckCommand(
    int DeckId,
    string FilePath
);

/// <summary>
/// Result of an export operation.
/// </summary>
public class ExportDeckResult
{
    public bool Success { get; set; }
    public int CardsExported { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ExportDeckCommandHandler
{
    private readonly ICollectionStore _collectionStore;
    private readonly ICardStore<MagicCardPhysical> _cardStore;

    public ExportDeckCommandHandler(
        ICollectionStore collectionStore,
        ICardStore<MagicCardPhysical> cardStore)
    {
        _collectionStore = collectionStore;
        _cardStore = cardStore;
    }

    public async Task<ExportDeckResult> Handle(ExportDeckCommand command, CancellationToken ct = default)
    {
        var result = new ExportDeckResult();

        try
        {
            // Resolve deck node
            var node = await _collectionStore.GetByIdAsync(command.DeckId, ct);
            if (node == null)
            {
                result.ErrorMessage = $"Deck '{command.DeckId}' not found.";
                return result;
            }

            // Targeted query — get only this deck's cards (WHERE DeckId = @DeckId)
            var deckCards = await _collectionStore.GetDeckCardsAsync(command.DeckId, ct);

            // Export to XML
            await XmlExport.ExportToFileAsync(
                command.FilePath,
                node.Name,
                node.Id.ToString(),
                null, // comment
                "deck",
                deckCards,
                ct);

            result.Success = true;
            result.CardsExported = deckCards.Count;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}