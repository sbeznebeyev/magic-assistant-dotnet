using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Atomic commit of a fully resolved import.
/// Invariant: called only after 100% of cards are resolved — before this call nothing
/// is created or modified in the database.
/// New mode (TargetCollectionId == null): creates the deck node + sideboard and adds
/// the cards in ONE transaction (ICollectionStore.CreateDeckWithCardsAsync).
/// Existing mode (TargetCollectionId != null): stamps location and adds cards to the
/// existing node with merge semantics.
/// CardOverrides — user selections from the unresolved-cards table, keyed by
/// "OriginalName|SetCode"; applied before insertion for the matching cards.
/// </summary>
public record ImportResolvedCardsCommand(
    string DeckName,
    int? ParentId,
    int? TargetCollectionId,
    IReadOnlyList<MagicCardPhysical> Cards,
    IReadOnlyDictionary<string, Guid>? CardOverrides = null);

/// <summary>Result of an atomic import commit.</summary>
public class ImportCommitResult
{
    public int DeckId { get; set; }
    public string DeckName { get; set; } = string.Empty;
    public int CardsImported { get; set; }
}

public class ImportResolvedCardsCommandHandler
{
    private readonly ICollectionStore _collectionStore;
    private readonly ICardStore<MagicCardPhysical> _cardStore;

    public ImportResolvedCardsCommandHandler(
        ICollectionStore collectionStore,
        ICardStore<MagicCardPhysical> cardStore)
    {
        _collectionStore = collectionStore;
        _cardStore = cardStore;
    }

    public async Task<ImportCommitResult> Handle(
        ImportResolvedCardsCommand command, CancellationToken ct = default)
    {
        if (command.Cards.Count == 0)
            throw new ArgumentException("No cards to import.", nameof(command));

        // Apply user overrides from the unresolved-cards table: key = "OriginalName|SetCode"
        if (command.CardOverrides is { Count: > 0 } overrides)
        {
            foreach (var card in command.Cards)
            {
                var key = $"{card.Name}|{card.SetCode}";
                if (overrides.TryGetValue(key, out var id))
                    card.Card.Id = id;
            }
        }

        if (command.TargetCollectionId is int targetId)
        {
            // Existing node: stamp location and add (merge semantics on conflict)
            var deck = await _collectionStore.GetByIdAsync(targetId, ct);
            if (deck == null)
                throw new InvalidOperationException($"Target collection '{targetId}' not found.");

            var location = Location.FromId(deck.Id, deck.Name, false);
            foreach (var card in command.Cards)
                card.SetLocation(location);

            await _cardStore.AddAllAsync(command.Cards, ct);

            return new ImportCommitResult { DeckId = deck.Id, DeckName = deck.Name, CardsImported = command.Cards.Count };
        }

        // New node: single transaction (node + sideboard + cards)
        var created = await _collectionStore.CreateDeckWithCardsAsync(command.DeckName, command.Cards, command.ParentId, ct);

        return new ImportCommitResult { DeckId = created.Id, DeckName = created.Name, CardsImported = command.Cards.Count };
    }
}
