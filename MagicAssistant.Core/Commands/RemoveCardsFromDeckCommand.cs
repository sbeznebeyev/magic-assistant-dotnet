using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Remove cards from a collection node — DeckId is the node's own ID (deck, collection or sideboard).
/// Non-empty CardIds: removes ALL copies of each listed card (full-row removal).
/// Empty CardIds: clears EVERY card out of the node — the "clear" operation. Used by
/// DeckCardService.ClearNodeAsync with the node's own ID (deck, collection or sideboard); the node itself is kept.
/// The actual work is a single SQL round-trip in ICollectionStore.RemoveCardsAsync;
/// the returned count is SUM(Count) over the deleted rows — total card copies, not row count.
/// Mirrors Java: DeleteHandler.remove() + DataManager.remove()
/// </summary>
public record RemoveCardsFromDeckCommand(int DeckId, ICollection<Guid> CardIds);

public class RemoveCardsFromDeckCommandHandler
{
    private readonly ICollectionStore _collectionStore;

    public RemoveCardsFromDeckCommandHandler(ICollectionStore collectionStore)
    {
        _collectionStore = collectionStore;
    }

    /// <summary>
    /// Delegates to the store: one SQL round-trip removes all copies of each listed card
    /// (or every card row when CardIds is empty). Returns the total number of card copies removed.
    /// </summary>
    public async Task<int> Handle(RemoveCardsFromDeckCommand command, CancellationToken ct = default)
        => await _collectionStore.RemoveCardsAsync(command.DeckId, command.CardIds ?? Array.Empty<Guid>(), ct);
}
