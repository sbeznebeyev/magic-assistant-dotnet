
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.IO.xml;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Commands;

// ═══════════════════════════════════════════════════════════
//  Import old Magic Assistant XML file with Gatherer ID and Set full name.
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Import a deck from a legacy Magic Assistant XML file where card IDs are Gatherer IDs
/// (integers) instead of Scryfall UUIDs, and set names are full names ("Core Set 2019") instead of set code ("m19").
/// 
/// Resolution strategy:
///   1. Batch-resolve full set names → abbreviations via Editions table
///   2. Update card.Set with resolved abbreviation
///   3. Batch-resolve (Name, Set) pairs → Scryfall UUID via Cards table
///   4. Replace Gatherer ID with Scryfall UUID
/// </summary>
public class LegacyImportDeckCommandHandler
{
    private readonly ICollectionStore _collectionStore;
    private readonly ICardStore<MagicCardPhysical> _cardStore;
    private readonly IDbCardStore _kbStore;

    public LegacyImportDeckCommandHandler(
        ICollectionStore collectionStore,
        ICardStore<MagicCardPhysical> cardStore,
        IDbCardStore kbStore)
    {
        _collectionStore = collectionStore;
        _cardStore = cardStore;
        _kbStore = kbStore;
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

            // Extract base cards for resolution (both MCP and raw MagicCard)
            var baseCards = importResult.Cards.Select(c => c.GetBase()).ToList();

            // ── Step 1: Batch-resolve full set names → abbreviations ──
            var distinctSetNames = baseCards
                .Select(c => c.SetCode!)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Dictionary<string, string> setMap = new(StringComparer.OrdinalIgnoreCase);
            if (distinctSetNames.Count > 0)
            {
                setMap = await _kbStore.ResolveSetNamesAsync(distinctSetNames, ct);
            }

            // Apply resolved abbreviations to cards
            foreach (var card in baseCards)
            {
                if (!string.IsNullOrEmpty(card.SetCode) && setMap.TryGetValue(card.SetCode!, out var abbr))
                    card.SetCode = abbr;
            }

            // ── Step 2: Batch-resolve (Name, Set) → Scryfall UUID ──
            var nameSetPairs = baseCards
                .Select(c => (c.Name, c.SetCode))
                .Distinct()
                .ToList();

            Dictionary<string, MagicCard> cardMap = new(StringComparer.Ordinal);
            if (nameSetPairs.Count > 0)
            {
                cardMap = await _kbStore.ResolveCardsByNameAndSetAsync(nameSetPairs, ct);
            }

            // Replace Gatherer IDs with Scryfall UUIDs
            foreach (var card in baseCards)
            {
                var key = $"{card.Name}|{card.SetCode}";
                if (cardMap.TryGetValue(key, out var kbCard))
                    card.Id = kbCard.Id;
            }

            // ── Step 3: Create deck and add cards ──
            var deck = await _collectionStore.CreateDeckAsync(importResult.Name, command.ParentId, ct);

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
