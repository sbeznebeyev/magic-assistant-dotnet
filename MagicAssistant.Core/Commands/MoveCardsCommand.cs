using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Move cards from one collection/deck to another.
/// Mirrors Java: DataManager.moveCards() + MoveToActiveDeckHandler
/// </summary>
public record MoveCardsCommand(
    ICollection<ICard> Cards,
    int SourceDeckId,
    int TargetDeckId);

public class MoveCardsCommandHandler
{
    private readonly ICardStore<MyCard> _cardStore;
    private readonly ICollectionStore _collectionStore;

    public MoveCardsCommandHandler(
        ICardStore<MyCard> cardStore,
        ICollectionStore collectionStore)
    {
        _cardStore = cardStore;
        _collectionStore = collectionStore;
    }

    /// <summary>
    /// Moves cards from source deck to target deck.
    /// Cards are removed from source and added to target (with merge semantics).
    /// Returns the number of cards successfully moved.
    /// </summary>
    public async Task<int> Handle(MoveCardsCommand command, CancellationToken ct = default)
    {
        var sourceDeck = await _collectionStore.GetByIdAsync(command.SourceDeckId, ct);
        var targetDeck = await _collectionStore.GetByIdAsync(command.TargetDeckId, ct);

        if (sourceDeck == null)
            throw new InvalidOperationException($"Source deck '{command.SourceDeckId}' not found");
        if (targetDeck == null)
            throw new InvalidOperationException($"Target deck '{command.TargetDeckId}' not found");

        // Targeted query — only the moved rows of the source deck (physical columns only;
        // Card carries Id, which is all RemoveAsync/AddAsync read from it).
        var sourceCards = await _collectionStore.GetDeckCardsForMoveAsync(sourceDeck.Id, command.Cards.Select(c => c.Id), ct);
        var sourceCardMap = sourceCards.ToDictionary(c => c.Id);

        int totalMoved = 0;

        foreach (var card in command.Cards)
        {
            if (!sourceCardMap.TryGetValue(card.Id, out var instance))
                continue;

            var moveCount = Math.Min(instance.Count, card.Count);
            if (moveCount <= 0) continue;

            // Remove from source
            await _cardStore.RemoveAsync(new MyCard(instance.Card, sourceDeck, instance.IsOwn)
            {
                Count = moveCount,
            }, ct);

            // Add to target with updated location
            var targetInstance = new MyCard(instance.Card, targetDeck, instance.IsOwn)
            {
                Count = moveCount,
                MyPrice = instance.MyPrice,
                Comment = instance.Comment,
                DateAdded = DateTimeOffset.Now,
            };

            await _cardStore.AddAsync(targetInstance, ct);
            totalMoved += moveCount;
        }

        return totalMoved;
    }
}
