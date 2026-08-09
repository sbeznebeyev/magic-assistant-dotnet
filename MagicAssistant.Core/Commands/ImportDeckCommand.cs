using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using MagicAssistant.Data.IO.xml;
using MagicAssistant.Data.DbModel;

namespace MagicAssistant.Core.Commands;

// ═══════════════════════════════════════════════════════════
//  Import
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Import a deck from a Magic Assistant XML file.
/// Creates a new deck node and adds imported cards to it.
/// </summary>
public record ImportDeckCommand(
    string FilePath,
    int? ParentId = null
);

/// <summary>
/// Result of an import operation.
/// </summary>
public class ImportDeckResult
{
    public bool Success { get; set; }
    public Collection? Deck { get; set; }
    public int CardsImported { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ImportDeckCommandHandler
{
    private readonly ICollectionStore _collectionStore;
    private readonly ICardStore<MagicCardPhysical> _cardStore;

    public ImportDeckCommandHandler(
        ICollectionStore collectionStore,
        ICardStore<MagicCardPhysical> cardStore)
    {
        _collectionStore = collectionStore;
        _cardStore = cardStore;
    }

    public async Task<ImportDeckResult> Handle(ImportDeckCommand command, CancellationToken ct = default)
    {
        var result = new ImportDeckResult();

        try
        {
            // Parse XML
            var importResult = await XmlImport.ImportFromFileAsync(command.FilePath, ct);

            if (!importResult.Cards.Any())
            {
                result.ErrorMessage = "No cards found in the file.";
                return result;
            }

            // Create deck node
            var deck = await _collectionStore.CreateDeckAsync(importResult.Name, command.ParentId, ct);

            // Add imported cards to the deck
            var location = Location.FromId(deck.Id, deck.Name, false);
            var physicals = new List<MagicCardPhysical>();

            foreach (var card in importResult.Cards)
            {
                MagicCardPhysical mcp;
                if (card is MagicCardPhysical existingPhysical)
                {
                    existingPhysical.SetLocation(location);
                    mcp = existingPhysical;
                }
                else
                {
                    mcp = new MagicCardPhysical(card.GetBase(), location, true);
                }
                physicals.Add(mcp);
            }

            await _cardStore.AddAllAsync(physicals, ct);

            result.Success = true;
            result.Deck = deck;
            result.CardsImported = physicals.Count;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}
