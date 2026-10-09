using MagicAssistant.Core.Commands;
using MagicAssistant.Core.Services.CardResolve;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Core.Services.DeckCard;

/// <summary>
/// Vertical slice: card operations within decks.
/// Handles adding, removing, moving, updating, and splitting cards in deck contexts.
/// 
/// Uses CardResolveService for batch resolution of KB cards.
/// </summary>
public class DeckCardService : IDeckCardService
{
    private readonly ICollectionStore _collectionStore;
    private readonly ICardStore<MagicCardPhysical> _cardStore;
    private readonly ICardResolveService _resolveService;
    private readonly RemoveCardsFromDeckCommandHandler _removeCardsHandler;

    public DeckCardService(
        ICollectionStore collectionStore,
        ICardStore<MagicCardPhysical> cardStore,
        ICardResolveService resolveService,
        RemoveCardsFromDeckCommandHandler removeCardsHandler)
    {
        _collectionStore = collectionStore;
        _cardStore = cardStore;
        _resolveService = resolveService;
        _removeCardsHandler = removeCardsHandler;
    }

    // ═══════════════════════════════════════════════════════════
    //  CRUD operations
    // ═══════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<bool> CopyCardsAsync(
        int deckId, ICollection<IMagicCard> cards, CancellationToken ct = default)
    {
        if (deckId <= 0)
            throw new ArgumentException("Invalid deck ID.", nameof(deckId));
        if (cards == null || !cards.Any())
            return true;

        var collection = new Collection(deckId, string.Empty, null, NodeType.Deck, 0);
        var physicals = new List<MagicCardPhysical>();

        foreach (var card in cards)
        {
            var baseCard = card.GetBase();
            bool isOwn = card is MagicCardPhysical mcp ? mcp.IsOwn : true;

            physicals.Add(new MagicCardPhysical(baseCard, collection, isOwn)
            {
                Count = card.Count,
            });
        }

        return await _cardStore.AddAllAsync(physicals, ct);
    }

    /// <inheritdoc />
    public async Task<ICollection<MagicCardPhysical>> MaterializeFromIdsAsync(int deckId, ICollection<Guid> cardIds, CancellationToken ct = default)
    {
        if (deckId <= 0)
            throw new ArgumentException("Invalid deck ID.", nameof(deckId));
        if (cardIds == null || !cardIds.Any())
            return Array.Empty<MagicCardPhysical>();

        // Batch resolve all card IDs in one query, then persist the physical instances
        // into the deck (merge semantics: ON CONFLICT increments Count).
        var physicals = await _resolveService.MaterializeFromIdsAsync(deckId, cardIds, ct);
        if (physicals.Count > 0)
            await _cardStore.AddAllAsync(physicals, ct);

        return physicals;
    }

    /// <inheritdoc />
    public async Task<int> RemoveCardsAsync(int deckId, ICollection<Guid> cardIds, CancellationToken ct = default)
    {
        if (deckId <= 0)
            throw new ArgumentException("Invalid deck ID.", nameof(deckId));
        if (cardIds == null || !cardIds.Any())
            return 0;

        // CQRS: full-row removal of every listed card from the node.
        return await _removeCardsHandler.Handle(
            new RemoveCardsFromDeckCommand(deckId, cardIds), ct);
    }

    /// <inheritdoc />
    public async Task<int> ClearNodeAsync(int nodeId, CancellationToken ct = default)
    {
        if (nodeId <= 0)
            throw new ArgumentException("Invalid node ID.", nameof(nodeId));

        // CQRS: empty CardIds = clear — wipe every card row of the node; the node itself is kept.
        return await _removeCardsHandler.Handle(
            new RemoveCardsFromDeckCommand(nodeId, Array.Empty<Guid>()), ct);
    }

    /// <inheritdoc />
    public async Task<int> MoveCardsAsync(int sourceDeckId, ICollection<Guid> cardIds, int targetDeckId, CancellationToken ct = default)
    {
        if (sourceDeckId <= 0)
            throw new ArgumentException("Invalid source deck ID.", nameof(sourceDeckId));
        if (targetDeckId <= 0)
            throw new ArgumentException("Invalid target deck ID.", nameof(targetDeckId));
        if (sourceDeckId == targetDeckId)
            throw new ArgumentException("Source and target must differ.", nameof(targetDeckId));
        if (cardIds == null || !cardIds.Any())
            return 0;

        // Get existing cards from source deck
        var sourceCards = await _collectionStore.GetDeckCardsAsync(sourceDeckId, ct);
        var toMove = sourceCards
            .Where(c => cardIds.Contains(c.Id))
            .ToList();

        if (!toMove.Any())
            return 0;

        var targetCollection = new Collection(targetDeckId, string.Empty, null, NodeType.Deck, 0);
        int moved = 0;

        foreach (var card in toMove)
        {
            // Remove from source
            await _cardStore.RemoveAsync(card, ct);

            // Add to target with new collection
            card.SetCollection(targetCollection);
            await _cardStore.AddAsync(card, ct);
            moved++;
        }

        return moved;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateCardAsync(int deckId, Guid cardId, Dictionary<MagicCardField, object?> updates, CancellationToken ct = default)
    {
        if (deckId <= 0)
            throw new ArgumentException("Invalid deck ID.", nameof(deckId));
        if (updates == null || !updates.Any())
            return false;

        var existing = await _collectionStore.GetCardsAsync(cardId, ct);
        var matching = existing.FirstOrDefault(e => e.Collection?.Id == deckId && e.Card.Id == cardId);
        if (matching == null)
            return false;

        var fieldsToUpdate = new List<MagicCardField>();

        foreach (var (field, value) in updates)
        {
            switch (field)
            {
                case MagicCardField.Count when value is int count:
                    matching.Count = count;
                    fieldsToUpdate.Add(field); break;
                case MagicCardField.IsOwn when value is bool isOwn:
                    matching.IsOwn = isOwn;
                    fieldsToUpdate.Add(field); break;
                case MagicCardField.MyPrice when value is float price:
                    matching.MyPrice = price;
                    fieldsToUpdate.Add(field); break;
                case MagicCardField.Comment when value is string comment:
                    matching.Comment = comment;
                    fieldsToUpdate.Add(field); break;
                case MagicCardField.Special when value is string special:
                    matching.SpecialTags = special;
                    fieldsToUpdate.Add(field); break;
                case MagicCardField.Custom when value is string custom:
                    matching.Custom = custom;
                    fieldsToUpdate.Add(field); break;
                case MagicCardField.ForTradeCount when value is int forTrade:
                    // 0 clears the count and any legacy "fortrade" tag fallback
                    if (forTrade == 0)
                        matching.RemoveSpecialTag("fortrade");
                    matching.SetForTradeCount(forTrade);
                    fieldsToUpdate.Add(field); break;
                // Nullable value types are illegal in patterns (CS8116) — split into two cases.
                case MagicCardField.Grade when value is CardGrade g:
                    matching.Grade = g;
                    fieldsToUpdate.Add(field); break;
                case MagicCardField.Grade: // null — clears the column
                    matching.Grade = null;
                    fieldsToUpdate.Add(field); break;
            }
        }

        if (!fieldsToUpdate.Any())
            return true;

        return await _cardStore.UpdateAsync(matching, fieldsToUpdate, ct);
    }

    // ═══════════════════════════════════════════════════════════
    //  Split operations
    // ═══════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<MagicCardPhysical?> SplitCardAsync(int deckId, Guid cardId, int rightCount, CancellationToken ct = default)
    {
        if (rightCount <= 0)
            return null;

        var existing = await _collectionStore.GetCardsAsync(cardId, ct);
        var matching = existing.FirstOrDefault(e => e.Collection?.Id == deckId && e.Card.Id == cardId);
        if (matching == null)
            return null;

        var total = matching.Count;
        if (rightCount >= total)
            return null; // nothing to split

        var leftCount = total - rightCount;
        var sideboard = await _collectionStore.GetByParentIdAsync(deckId);

        if (sideboard is null)
            throw new InvalidOperationException("sideboard is not exists");

        // Create right part FIRST (before modifying original count)
        var rightPart = new MagicCardPhysical(matching.Card, sideboard, matching.IsOwn)
        {
            Count = rightCount,
            MyPrice = matching.MyPrice,
            Comment = matching.Comment,
            SpecialTags = matching.SpecialTags,
            Custom = matching.Custom,
        };

        // Update original to left count
        matching.Count = leftCount;
        await _cardStore.UpdateAsync(matching, new[] { MagicCardField.Count }, ct);

        // Add right part
        await _cardStore.AddAsync(rightPart, ct);

        return rightPart;
    }

    /// <inheritdoc />
    public async Task<ICollection<MagicCardPhysical>> SplitCardsAsync(
        ICollection<IMagicCard> cards, int rightCount, CancellationToken ct = default)
    {
        var expanded = _resolveService.ExpandGroups(cards);
        var result = new List<MagicCardPhysical>();

        foreach (var card in expanded)
        {
            if (card is MagicCardPhysical mcp && mcp.Collection is { Id: > 0 } c)
            {
                var locId = c.Id;
                var split = await SplitCardAsync(locId, mcp.Id, rightCount, ct);
                if (split != null)
                    result.Add(split);
            }
            else if (card is MagicCard mc)
            {
                for (var i = 0; i < rightCount; i++)
                {
                    var clone = (MagicCard)mc.Clone();
                    result.Add(new MagicCardPhysical(clone, null, true));
                }
            }
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════
    //  Read operations
    // ═══════════════════════════════════════════════════════════

    /// <inheritdoc />
    public Task<ICollection<MagicCardPhysical>> GetDeckCardsAsync(int deckId, CancellationToken ct = default)
    {
        if (deckId <= 0)
            throw new ArgumentException("Invalid deck ID.", nameof(deckId));

        return _collectionStore.GetDeckCardsAsync(deckId, ct);
    }

    /// <inheritdoc />
    public Task<ICollection<MagicCardPhysical>> GetDeckWithSideboardAsync(int deckId, CancellationToken ct = default)
    {
        if (deckId <= 0)
            throw new ArgumentException("Invalid deck ID.", nameof(deckId));

        return _collectionStore.GetDeckWithSideboardAsync(deckId, ct);
    }

    /// <inheritdoc />
    public Task<Dictionary<(int CollectionId, Guid CardId), int>> GetDeckCardCountsAsync(ICollection<int> collectionIds, ICollection<Guid> cardIds, CancellationToken ct = default)
    {
        if (collectionIds == null || !collectionIds.Any())
            throw new ArgumentException("At least one collection ID is required.", nameof(collectionIds));

        return _collectionStore.GetCardCountsAsync(collectionIds, cardIds, ct);
    }

    /// <inheritdoc />
    public async Task<int> GetOwnedCountAsync(int deckId, Guid cardId, CancellationToken ct = default)
    {
        if (deckId <= 0)
            throw new ArgumentException("Invalid deck ID.", nameof(deckId));

        // All physical rows of this card across the DB — filter to the given deck and owned copies only.
        var rows = await _collectionStore.GetCardsAsync(cardId, ct);
        return rows.Where(r => r.Collection?.Id == deckId && r.IsOwn).Sum(r => r.Count);
    }
}
