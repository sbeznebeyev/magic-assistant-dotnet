using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Atomic commit of a fully resolved import.
/// Invariant: called only after 100% of cards are resolved — before this call nothing
/// is created or modified in the database.
/// New mode (TargetCollectionId == null): creates the deck node + sideboard and adds
/// the cards in ONE transaction (ICollectionStore.CreateDeckWithCardsAsync);
/// <see cref="SideboardCards"/> land in the freshly created sideboard child.
/// Existing mode (TargetCollectionId != null): stamps location and adds cards to the
/// existing node with merge semantics; <see cref="SideboardCards"/> merge into the
/// target deck's sideboard child. A non-deck target (Collection) has no sideboard home —
/// the file's sideboard is silently ignored there, only the main cards are imported.
/// CardOverrides — user selections from the unresolved-cards table, keyed by
/// "OriginalName|SetCode"; applied before insertion for the matching cards.
/// </summary>
public record ImportResolvedCardsCommand(
    string DeckName,
    int? ParentId,
    int? TargetCollectionId,
    IReadOnlyList<MyCard> Cards,
    IReadOnlyDictionary<string, Guid>? CardOverrides = null,
    IReadOnlyList<MyCard>? SideboardCards = null);

/// <summary>Result of an atomic import commit.</summary>
public class ImportCommitResult
{
    public int DeckId { get; set; }
    public string DeckName { get; set; } = string.Empty;
    public int CardsImported { get; set; }

    /// <summary>Sideboard cards merged into the sideboard node (0 when the file had none).</summary>
    public int SideboardCardsImported { get; set; }
}

public class ImportResolvedCardsCommandHandler
{
    private readonly ICollectionStore _collectionStore;
    private readonly ICardStore<MyCard> _cardStore;

    public ImportResolvedCardsCommandHandler(
        ICollectionStore collectionStore,
        ICardStore<MyCard> cardStore)
    {
        _collectionStore = collectionStore;
        _cardStore = cardStore;
    }

    public async Task<ImportCommitResult> Handle(
        ImportResolvedCardsCommand command, CancellationToken ct = default)
    {
        if (command.Cards.Count == 0)
            throw new ArgumentException("No cards to import.", nameof(command));

        var sideboard = (command.SideboardCards ?? Enumerable.Empty<MyCard>()).ToList();

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

            // Pre-check the sideboard home BEFORE any insert: merge mode commits main and
            // sideboard as separate transactions, so a late failure would leave a partial import.
            // A non-deck target (Collection) has no sideboard child — the file's sideboard is
            // silently ignored there; only the main cards are imported.
            Collection? sbNode = null;
            if (sideboard.Count > 0 && deck.NodeType == NodeType.Deck)
            {
                var sbId = await _collectionStore.GetSideboardIdAsync(targetId, ct);
                if (sbId is not { } foundSbId)
                    throw new InvalidOperationException($"Target deck '{deck.Name}' has no sideboard node.");

                sbNode = await _collectionStore.GetByIdAsync(foundSbId, ct);
            }

            foreach (var card in command.Cards)
                card.SetCollection(deck);

            await _cardStore.AddAllAsync(command.Cards, ct);

            if (sideboard.Count > 0 && sbNode is not null)
            {
                foreach (var card in sideboard)
                    card.SetCollection(sbNode);

                await _cardStore.AddAllAsync(sideboard, ct);
            }

            return new ImportCommitResult
            {
                DeckId = deck.Id,
                DeckName = deck.Name,
                CardsImported = command.Cards.Count,
                SideboardCardsImported = sideboard.Count
            };
        }

        // New node: single transaction (node + sideboard + cards)
        var created = await _collectionStore.CreateDeckWithCardsAsync(
            command.DeckName, command.Cards, command.ParentId,
            sideboard.Count > 0 ? sideboard : null, ct);

        return new ImportCommitResult
        {
            DeckId = created.Id,
            DeckName = created.Name,
            CardsImported = command.Cards.Count,
            SideboardCardsImported = sideboard.Count
        };
    }
}
