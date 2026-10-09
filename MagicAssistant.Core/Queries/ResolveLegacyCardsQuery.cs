using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// A card from a legacy file that could not be resolved in the knowledge base.
/// Name is the original name as it appears in the file (face suffix included).
/// </summary>
public record UnresolvedLegacyCard(string Name, string? SetCode);

/// <summary>
/// Result of resolving legacy cards against the knowledge base.
/// Resolved — physicals with real Scryfall UUIDs stamped, ready for commit.
/// Unresolved — distinct (Name, SetCode) rows that need manual mapping or set sync.
/// </summary>
public class LegacyCardResolution
{
    public List<MagicCardPhysical> Resolved { get; } = new();
    public List<UnresolvedLegacyCard> Unresolved { get; } = new();
}

/// <summary>
/// Batch-resolves cards from a legacy Magic Assistant XML (integer Gatherer IDs, full set names)
/// against the knowledge base — read-only, no database writes:
///   1. strips face suffixes from split card names ("Trial // Error (Error)" → "Trial // Error")
///   2. resolves full set names → abbreviations via CardSet (case-insensitive)
///   3. resolves (Name, SetCode) pairs → Scryfall UUIDs — exact match against all name
///      fields (Cards.Name/PrintedName, CardFaces.Name/PrintedName), so localized names work
/// Unresolved cards are reported in <see cref="LegacyCardResolution.Unresolved"/>.
/// </summary>
public record ResolveLegacyCardsQuery(IReadOnlyList<IMagicCard> Cards);

public class ResolveLegacyCardsQueryHandler
{
    private readonly IDbCardStore _kbStore;

    public ResolveLegacyCardsQueryHandler(IDbCardStore kbStore)
    {
        _kbStore = kbStore;
    }

    public async Task<LegacyCardResolution> Handle(
        ResolveLegacyCardsQuery query, CancellationToken ct = default)
    {
        var result = new LegacyCardResolution();
        var cards = query.Cards;
        if (cards.Count == 0) return result;

        // Step 1: matching names — split face suffix stripped for KB lookup
        var matchNames = cards.Select(c => StripFaceSuffix(c.Name)).ToList();

        // Step 2: full set names → abbreviations
        var distinctSetNames = cards
            .Select(c => c.SetCode!)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, string> setMap = new(StringComparer.OrdinalIgnoreCase);
        if (distinctSetNames.Count > 0)
            setMap = await _kbStore.ResolveSetNamesAsync(distinctSetNames, ct);

        var matchSets = cards
            .Select(c =>
            {
                var s = c.SetCode;
                if (string.IsNullOrEmpty(s)) return null;
                return setMap.TryGetValue(s!, out var abbr) ? abbr : s;
            })
            .ToList();

        // Step 3: batch (Name, SetCode) → Scryfall UUID
        var pairs = matchNames.Zip(matchSets, (name, set) => (Name: name, Set: set)).Distinct().ToList();

        Dictionary<string, MagicCard> cardMap = new(StringComparer.Ordinal);
        if (pairs.Count > 0)
            cardMap = await _kbStore.ResolveCardsByNameAndSetAsync(pairs, ct);

        // Step 4: stamp resolved IDs, collect unresolved rows (distinct by original name + set)
        var seenUnresolved = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var key = $"{matchNames[i]}|{matchSets[i]}";

            if (cardMap.TryGetValue(key, out var kbCard))
            {
                if (card is MagicCardPhysical physical)
                {
                    physical.Card.Id = kbCard.Id;
                    result.Resolved.Add(physical);
                }
                else
                {
                    // Raw (non-physical) cards are not produced by XmlImport, but stay supported:
                    // wrap into a physical with the resolved ID; collection is stamped at commit.
                    var baseCopy = new MagicCard { Id = kbCard.Id, Name = card.Name, SetCode = card.SetCode };
                    result.Resolved.Add(new MagicCardPhysical(baseCopy, null, true));
                }
            }
            else if (seenUnresolved.Add($"{card.Name}|{card.SetCode}"))
            {
                result.Unresolved.Add(new UnresolvedLegacyCard(card.Name, card.SetCode));
            }
        }

        return result;
    }

    /// <summary>
    /// Strips the legacy face suffix from a split card name:
    /// "Trial // Error (Error)" → "Trial // Error". Names without "//" are returned as-is.
    /// </summary>
    public static string StripFaceSuffix(string name)
    {
        if (!name.Contains("//")) return name;

        var idx = name.LastIndexOf(" (", StringComparison.Ordinal);
        if (idx > 0 && name.EndsWith(")", StringComparison.Ordinal))
            return name[..idx];

        return name;
    }
}
