using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Remove cards from a deck. Supports partial removal (by count) and full removal.
/// Mirrors Java: DeleteHandler.remove() + DataManager.remove()
/// </summary>
public record RemoveCardsFromDeckCommand(int DeckId, ICollection<IMagicCard> Cards, bool RemoveSideboard = false);

public class RemoveCardsFromDeckCommandHandler
{
    private readonly ICardStore<MagicCardPhysical> _cardStore;
    private readonly ICollectionStore _collectionStore;

    public RemoveCardsFromDeckCommandHandler(
        ICardStore<MagicCardPhysical> cardStore,
        ICollectionStore collectionStore)
    {
        _cardStore = cardStore;
        _collectionStore = collectionStore;
    }

    /// <summary>
    /// Removes cards from a deck. If count exceeds available, removes all copies.
    /// Returns the number of cards actually removed.
    /// </summary>
    public async Task<int> Handle(RemoveCardsFromDeckCommand command, CancellationToken ct = default)
    {
        var deck = await _collectionStore.GetByIdAsync(command.DeckId, ct);
        if (deck == null)
            throw new InvalidOperationException($"Deck '{command.DeckId}' not found");

        // Targeted query — get only this deck's cards (WHERE DeckId = @DeckId)
        var deckCards = await _collectionStore.GetDeckCardsAsync(deck.Id, ct);
        var deckCardMap = deckCards.ToDictionary(c => c.Id);

        int totalRemoved = 0;

        foreach (var card in command.Cards)
        {
            if (!deckCardMap.TryGetValue(card.Id, out var instance))
                continue;

            var removeCount = Math.Min(instance.Count, card.Count);
            if (removeCount <= 0) continue;

            await _cardStore.RemoveAsync(new MagicCardPhysical(instance.Card, instance.Location, instance.IsOwn)
            {
                Count = removeCount,
            }, ct);
            totalRemoved += removeCount;
        }

        // Also handle sideboard if requested
        if (command.RemoveSideboard && deck != null)
        {
            // Find sideboard child of this deck
            var sbChildren = await _collectionStore.GetChildrenAsync(deck.Id, ct);
            var sbNode = sbChildren.FirstOrDefault(n => n.NodeType == NodeType.Sideboard);
            if (sbNode != null)
            {
                var sbCards = await _collectionStore.GetDeckCardsAsync(sbNode.Id, ct);
            var sbCardMap = sbCards.ToDictionary(c => c.Id);

            foreach (var card in command.Cards)
            {
                if (!sbCardMap.TryGetValue(card.Id, out var instance))
                    continue;

                var removeCount = Math.Min(instance.Count, card.Count);
                if (removeCount <= 0) continue;

                await _cardStore.RemoveAsync(new MagicCardPhysical(instance.Card, instance.Location, instance.IsOwn)
                {
                    Count = removeCount,
                }, ct);
                totalRemoved += removeCount;
            }
            }
        }

        return totalRemoved;
    }
}
