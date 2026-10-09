using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Core.Services.CardResolve;

/// <summary>
/// Vertical slice: resolves stub cards to real KB cards, materializes borrowed cards,
/// and provides synchronous utility operations.
/// 
/// Key optimization: batch resolve via GetCardsByIdsAsync — one SQL query instead of N.
/// </summary>
public class CardResolveService : ICardResolveService
{
    private readonly IDbCardStore _kb;
    private readonly ICollectionStore _coll;

    public CardResolveService(
        IDbCardStore kb,
        ICollectionStore coll)
    {
        _kb = kb;
        _coll = coll;
    }

    /// <summary>
    /// Resolve card references to KB cards. Expands CardGroup hierarchies first.
    /// Uses batch SQL query (WHERE Id IN @ids) for efficiency — one round-trip instead of N.
    /// </summary>
    public async Task<ICollection<ICard>> ResolveCardsAsync(ICollection<ICard> cards, CancellationToken ct = default)
    {
        if (cards == null || !cards.Any())
            return Array.Empty<ICard>();

        var expanded = ExpandGroups(cards);

        // Collect all unique card IDs for batch lookup
        var ids = expanded
            .Select(c => c is Card mc ? mc.Id : c.Id)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return Array.Empty<ICard>();

        // Single batch query instead of N individual queries
        var kbMap = await _kb.GetCardsByIdsAsync(ids, ct);

        var output = new List<ICard>(expanded.Count);
        foreach (var card in expanded)
        {
            var resolved = ResolveSingle(card, kbMap);
            if (resolved != null)
                output.Add(resolved);
        }

        return output;
    }

    private static ICard? ResolveSingle(ICard card, Dictionary<Guid, Card> kbMap)
    {
        if (card is Card mc)
        {
            return kbMap.TryGetValue(mc.Id, out var kb) ? kb : mc; // fall back to stub
        }

        if (card is MyCard mcp)
        {
            if (kbMap.TryGetValue(mcp.Id, out var kb2))
                mcp.SetBaseCard(kb2);
            return mcp;
        }

        return card; // CardGroup or other — pass through
    }

    /// <summary>
    /// Materialize borrowed cards to real physical instances.
    /// Owned cards → copy with null location.
    /// Borrowed cards → resolve from collection store, filling remaining count.
    /// Single query for all physical cards + O(1) dictionary lookup.
    /// </summary>
    public async Task<ICollection<MyCard>> MaterializeCardsAsync(ICollection<ICard> cards, CancellationToken ct = default)
    {
        var expanded = ExpandGroups(cards);
        var result = new List<MyCard>();

        // Single query: get ALL physical cards once, group by CardId for O(1) lookup
        var allCards = (await _coll.GetAllDeckCardsAsync(ct)).Cast<MyCard>().ToList();
        var byCardId = allCards
            .Where(c => c.IsOwn)
            .GroupBy(c => c.Id)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var card in expanded)
        {
            if (card is not MyCard physical)
                continue;

            if (physical.IsOwn)
            {
                // Owned → copy with null collection
                result.Add(new MyCard(physical.Card, null, true)
                {
                    Count = physical.Count,
                });
            }
            else
            {
                // Borrowed → resolve from owned cards by CardId
                var remaining = physical.Count;

                if (byCardId.TryGetValue(physical.Id, out var candidates))
                {
                    foreach (var candidate in candidates)
                    {
                        if (remaining <= 0) break;

                        var grabCount = Math.Min(candidate.Count, remaining);
                        result.Add(new MyCard(candidate.Card, candidate.Collection, true)
                        {
                            Count = grabCount,
                        });
                        remaining -= grabCount;
                    }
                }

                // Unresolved → placeholder (borrowed, no collection)
                if (remaining > 0)
                {
                    result.Add(new MyCard(physical.Card, null, false)
                    {
                        Count = remaining,
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Expand CardGroup hierarchies — flatten all nested groups into a flat list.
    /// Synchronous — no I/O involved.
    /// </summary>
    public ICollection<ICard> ExpandGroups(ICollection<ICard> cards)
    {
        var result = new List<ICard>(cards.Count);
        ExpandRecursive(cards, result);
        return result;
    }

    private static void ExpandRecursive(IEnumerable<ICard> cards, List<ICard> result)
    {
        foreach (var card in cards)
        {
            result.Add(card);
            if (card is CardGroup group)
                ExpandRecursive(group.Children, result);
        }
    }

    /// <summary>
    /// Find common location from a collection of cards (all must share the same location).
    /// Returns null if cards have different or no locations.
    /// Synchronous — no I/O involved.
    /// </summary>
    public Collection? GetCollection(ICollection<ICard> cards)
    {
        if (cards == null || !cards.Any())
            return null;

        Collection? common = null;
        foreach (var card in cards)
        {
            if (card is MyCard mcp && mcp.Collection is { Id: > 0 } c)
            {
                if (common == null)
                    common = c;
                else if (common.Id != c.Id)
                    return null; // different collections
            }
            else
            {
                return null; // non-physical breaks the chain
            }
        }

        return common;
    }

    /// <summary>
    /// Resolve card IDs from KB into MagicCardPhysical instances with a specific deck location.
    /// Mirrors the old DataManager.MaterializeCardsAsync(deckId, cardIds).
    /// Uses batch resolve for efficiency.
    /// </summary>
    public async Task<ICollection<MyCard>> MaterializeFromIdsAsync(int deckId, ICollection<Guid> cardIds, CancellationToken ct = default)
    {
        if (deckId <= 0)
            throw new ArgumentException("Invalid deck ID.", nameof(deckId));
        if (cardIds == null || !cardIds.Any())
            return Array.Empty<MyCard>();

        // Batch resolve all card IDs in one query
        var kbMap = await _kb.GetCardsByIdsAsync(cardIds.ToList(), ct);
        var collection = new Collection(deckId, string.Empty, null, NodeType.Deck, 0);
        var physicals = new List<MyCard>(kbMap.Count);

        foreach (var cardId in cardIds)
        {
            if (kbMap.TryGetValue(cardId, out var card))
                physicals.Add(new MyCard(card, collection, true));
        }

        return physicals;
    }
}
