using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Services.CardResolve;

/// <summary>
/// Vertical slice: resolves stub cards to real KB cards, materializes borrowed cards,
/// and provides synchronous utility operations (ExpandGroups, GetCollection).
/// 
/// Replaces DataManager.ResolveAsync, MaterializeAsync, ExpandGroups, GetCollection.
/// </summary>
public interface ICardResolveService
{
    /// <summary>
    /// Resolve card references to KB cards. Expands CardGroup hierarchies first.
    /// For MagicCard: replaces stub with real KB card.
    /// For MagicCardPhysical: updates base card reference via SetBaseCard().
    /// Uses batch SQL query (WHERE Id IN @ids) for efficiency.
    /// </summary>
    Task<ICollection<ICard>> ResolveCardsAsync(ICollection<ICard> cards, CancellationToken ct = default);

    /// <summary>
    /// Materialize borrowed cards to real physical instances.
    /// Owned cards → copy with null location.
    /// Borrowed cards → resolve from collection store, filling remaining count.
    /// Single query for all physical cards + O(1) dictionary lookup.
    /// </summary>
    Task<ICollection<MyCard>> MaterializeCardsAsync(ICollection<ICard> cards, CancellationToken ct = default);

    /// <summary>
    /// Expand CardGroup hierarchies — flatten all nested groups into a flat list.
    /// Synchronous — no I/O involved.
    /// </summary>
    ICollection<ICard> ExpandGroups(ICollection<ICard> cards);

    /// <summary>
    /// Find common collection from a set of cards (all must share the same collection).
    /// Returns null if cards have different or no collections.
    /// Synchronous — no I/O involved.
    /// </summary>
    Collection? GetCollection(ICollection<ICard> cards);

    /// <summary>
    /// Resolve card IDs from KB into MagicCardPhysical instances with a specific deck location.
    /// Mirrors the old DataManager.MaterializeCardsAsync(deckId, cardIds).
    /// </summary>
    Task<ICollection<MyCard>> MaterializeFromIdsAsync(int deckId, ICollection<Guid> cardIds, CancellationToken ct = default);
}
