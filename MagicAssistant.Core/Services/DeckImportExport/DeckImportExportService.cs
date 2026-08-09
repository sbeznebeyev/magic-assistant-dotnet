using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.DbModel;
using MagicAssistant.Data.IO.xml;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Services.DeckImportExport;

/// <summary>
/// Vertical slice: deck import/export operations.
/// Handles XML file parsing and serialization for Magic Assistant format.
/// </summary>
public class DeckImportExportService : IDeckImportExportService
{
    private readonly ICollectionStore _collectionStore;
    private readonly ICardStore<MagicCardPhysical> _cardStore;
    private readonly IDbCardStore? _kbStore;

    public DeckImportExportService(
        ICollectionStore collectionStore,
        ICardStore<MagicCardPhysical> cardStore,
        IDbCardStore? kbStore = null)
    {
        _collectionStore = collectionStore;
        _cardStore = cardStore;
        _kbStore = kbStore;
    }

    /// <inheritdoc />
    public async Task<ImportDeckResult> ImportFromFileAsync(
        string filePath, int? parentId = null, CancellationToken ct = default)
    {
        var result = new ImportDeckResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.ErrorMessage = $"File not found: {filePath}";
                return result;
            }

            // Parse XML
            var importResult = await XmlImport.ImportFromFileAsync(filePath, ct);

            if (!importResult.Cards.Any())
            {
                result.ErrorMessage = "No cards found in the file.";
                return result;
            }

            // Create deck node
            var deck = await _collectionStore.CreateDeckAsync(importResult.Name, parentId, ct);

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

    /// <inheritdoc />
    public async Task<int> ImportToCollectionAsync(
        int collectionId, string xmlContent, CancellationToken ct = default)
    {
        if (collectionId <= 0)
            throw new ArgumentNullException(nameof(collectionId));
        if (string.IsNullOrWhiteSpace(xmlContent))
            throw new ArgumentException("XML content is required.", nameof(xmlContent));

        var importResult = XmlImport.ImportFromXml(xmlContent);
        var location = Location.FromId(collectionId, collectionId.ToString(), false);

        var physicals = new List<MagicCardPhysical>();
        foreach (var card in importResult.Cards)
        {
            if (card is MagicCardPhysical mcp)
            {
                mcp.SetLocation(location);
                physicals.Add(mcp);
            }
            else if (card is MagicCard mc)
            {
                physicals.Add(new MagicCardPhysical(mc, location, true));
            }
        }

        if (physicals.Any())
            await _cardStore.AddAllAsync(physicals, ct);

        return physicals.Count;
    }

    /// <inheritdoc />
    public async Task<string> ExportDeckToXmlAsync(int deckId, CancellationToken ct = default)
    {
        if (deckId <= 0)
            throw new ArgumentException("Invalid deck ID.", nameof(deckId));

        var cards = await _collectionStore.GetDeckCardsAsync(deckId, ct);
        var deckNode = await _collectionStore.GetByIdAsync(deckId, ct);
        
        return XmlExport.ExportToXml(
            name: deckNode?.Name ?? "Unknown Deck",
            key: null,
            comment: null,
            type: "deck",
            cards);
    }

    /// <inheritdoc />
    public async Task<ExportDeckResult> ExportToFileAsync(
        int deckId, string filePath, CancellationToken ct = default)
    {
        var result = new ExportDeckResult();

        try
        {
            var node = await _collectionStore.GetByIdAsync(deckId, ct);
            if (node == null)
            {
                result.ErrorMessage = $"Deck '{deckId}' not found.";
                return result;
            }

            // Targeted query — get only this deck's cards
            var deckCards = await _collectionStore.GetDeckCardsAsync(deckId, ct);

            // Export to XML file
            await XmlExport.ExportToFileAsync(
                filePath,
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

    // ── Legacy import (Gatherer ID + full set name) ──

    /// <inheritdoc />
    public async Task<ImportDeckResult> ImportLegacyFromFileAsync(
        string filePath, int? parentId = null, CancellationToken ct = default)
    {
        var result = new ImportDeckResult();

        try
        {
            if (!File.Exists(filePath))
            {
                result.ErrorMessage = $"File not found: {filePath}";
                return result;
            }

            if (_kbStore == null)
            {
                result.ErrorMessage = "Knowledge base store is not available. Legacy import requires KB for resolving Gatherer IDs and set names.";
                return result;
            }

            // Parse XML
            var importResult = await XmlImport.ImportFromFileAsync(filePath, ct);

            if (!importResult.Cards.Any())
            {
                result.ErrorMessage = "No cards found in the file.";
                return result;
            }

            var baseCards = importResult.Cards.Select(c => c.GetBase()).ToList();

            // Step 1: Batch-resolve full set names → abbreviations
            var distinctSetNames = baseCards
                .Select(c => c.SetCode!)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Dictionary<string, string> setMap = new(StringComparer.OrdinalIgnoreCase);
            if (distinctSetNames.Count > 0)
                setMap = await _kbStore.ResolveSetNamesAsync(distinctSetNames, ct);

            foreach (var card in baseCards)
            {
                if (!string.IsNullOrEmpty(card.SetCode) && setMap.TryGetValue(card.SetCode!, out var abbr))
                    card.SetCode = abbr;
            }

            // Step 2: Batch-resolve (Name, Set) → Scryfall UUID
            var nameSetPairs = baseCards.Select(c => (c.Name, c.SetCode)).Distinct().ToList();

            Dictionary<string, MagicCard> cardMap = new(StringComparer.Ordinal);
            if (nameSetPairs.Count > 0)
                cardMap = await _kbStore.ResolveCardsByNameAndSetAsync(nameSetPairs, ct);

            foreach (var card in baseCards)
            {
                var key = $"{card.Name}|{card.SetCode}";
                if (cardMap.TryGetValue(key, out var kbCard))
                    card.Id = kbCard.Id;
            }

            // Step 3: Create deck and add cards
            var deck = await _collectionStore.CreateDeckAsync(importResult.Name, parentId, ct);

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

    /// <inheritdoc />
    public async Task<int> ImportLegacyToCollectionAsync(
        int collectionId, string xmlContent, CancellationToken ct = default)
    {
        if (collectionId <= 0)
            throw new ArgumentNullException(nameof(collectionId));
        if (string.IsNullOrWhiteSpace(xmlContent))
            throw new ArgumentException("XML content is required.", nameof(xmlContent));

        if (_kbStore == null)
            throw new InvalidOperationException("Legacy import requires KB for resolving Gatherer IDs and set names.");

        var importResult = XmlImport.ImportFromXml(xmlContent);
        var baseCards = importResult.Cards.Select(c => c.GetBase()).ToList();

        // Resolve full set names → abbreviations
        var distinctSetNames = baseCards
            .Select(c => c.SetCode!)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, string> setMap = new(StringComparer.OrdinalIgnoreCase);
        if (distinctSetNames.Count > 0)
            setMap = await _kbStore.ResolveSetNamesAsync(distinctSetNames, ct);

        foreach (var card in baseCards)
        {
            if (!string.IsNullOrEmpty(card.SetCode) && setMap.TryGetValue(card.SetCode!, out var abbr))
                card.SetCode = abbr;
        }

        // Resolve (Name, Set) → Scryfall UUID
        var nameSetPairs = baseCards.Select(c => (c.Name, c.SetCode)).Distinct().ToList();

        Dictionary<string, MagicCard> cardMap = new(StringComparer.Ordinal);
        if (nameSetPairs.Count > 0)
            cardMap = await _kbStore.ResolveCardsByNameAndSetAsync(nameSetPairs, ct);

        foreach (var card in baseCards)
        {
            var key = $"{card.Name}|{card.SetCode}";
            if (cardMap.TryGetValue(key, out var kbCard))
                card.Id = kbCard.Id;
        }

        // Add to collection
        var location = Location.FromId(collectionId, collectionId.ToString(), false);
        var physicals = new List<MagicCardPhysical>();

        foreach (var card in importResult.Cards)
        {
            if (card is MagicCardPhysical mcp)
            {
                mcp.SetLocation(location);
                physicals.Add(mcp);
            }
            else if (card is MagicCard mc)
            {
                physicals.Add(new MagicCardPhysical(mc, location, true));
            }
        }

        if (physicals.Any())
            await _cardStore.AddAllAsync(physicals, ct);

        return physicals.Count;
    }
}

// ═══════════════════════════════════════════════════════════
//  Result types
// ═══════════════════════════════════════════════════════════

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

/// <summary>
/// Result of an export operation.
/// </summary>
public class ExportDeckResult
{
    public bool Success { get; set; }
    public int CardsExported { get; set; }
    public string? ErrorMessage { get; set; }
}
