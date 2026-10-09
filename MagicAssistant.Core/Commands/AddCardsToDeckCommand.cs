using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Commands;

public record AddCardsToDeckCommand(int DeckId, ICollection<ICard> Cards);

public class AddCardsToDeckCommandHandler
{
    private readonly ICardStore<MyCard> _cardStore;
    private readonly ICollectionStore _collectionStore;

    public AddCardsToDeckCommandHandler(
        ICardStore<MyCard> cardStore,
        ICollectionStore collectionStore)
    {
        _cardStore = cardStore;
        _collectionStore = collectionStore;
    }

    public async Task<bool> Handle(AddCardsToDeckCommand command, CancellationToken ct = default)
    {
        var deck = await _collectionStore.GetByIdAsync(command.DeckId, ct);
        if (deck == null)
            throw new InvalidOperationException($"Deck '{command.DeckId}' not found");

        var physicals = command.Cards.Select(c => new MyCard(c.GetBase(), deck, true)).ToList();

        return await _cardStore.AddAllAsync(physicals, ct);
    }
}
