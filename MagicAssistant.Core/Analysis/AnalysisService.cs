using MagicAssistant.Core.Expressions;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Analysis;

/// <summary>
/// Single-pass deck analysis service.
/// Mirrors Java CardStoreUtils + aggr classes, but uses C# LINQ for clarity.
/// 
/// Computed in one pass:
/// - Mana curve (CMC distribution 0-7+)
/// - Color distribution (W/U/B/R/G/Colorless)
/// - Type distribution (Land/Creature/Instant/Sorcery/Enchantment/Artifact/Planeswalker)
/// - Creature subtypes
/// - Keyword abilities count
/// - Basic stats (total cards, total mana cost, max repeats)
/// </summary>
public class AnalysisService : IAnalysisService
{
    private static readonly HashSet<string> _landTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "land",
        "basic land",
        "basic"
    };

    private static readonly HashSet<string> _creatureTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "creature"
    };

    private static readonly HashSet<string> _instantTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "instant"
    };

    private static readonly HashSet<string> _sorceryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "sorcery"
    };

    private static readonly HashSet<string> _enchantmentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "enchantment"
    };

    private static readonly HashSet<string> _artifactTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "artifact"
    };

    private static readonly HashSet<string> _planeswalkerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "planeswalker"
    };

    public DeckAnalysisResult Analyze(IEnumerable<MyCard> cards)
    {
        var result = new DeckAnalysisResult();

        // Accumulators for creature subtypes and keyword abilities (counts, not just presence)
        var creatureSubtypeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var abilityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in cards)
        {
            var baseCard = card.Card;
            var count = card.Count;
            var type = baseCard.Type;
            var cost = baseCard.ManaCost;

            // --- Mana curve (non-land only, by CMC) ---
            if (!IsLand(baseCard) && !string.IsNullOrEmpty(cost) && baseCard.CmcNumber.HasValue)
            {
                var cmc = baseCard.CmcNumber.Value;
                if (cmc >= 0 && cmc <= 6)
                    result.ManaCurve[cmc] += count;
                else if (cmc >= 7)
                    result.ManaCurve[7] += count;

                result.TotalManaCost += cmc * count;
            }

            // --- Color distribution (non-land only) ---
            // Mirrors Java: Colors.getColorPresense(cost) parses { } symbols directly,
            // not via ColorType. Handles mono, multi, hybrid, pitch correctly.
            if (!IsLand(baseCard))
            {
                var colors = ColorTypes.GetColorPresence(cost);
                if (colors.Any())
                {
                    foreach (var c in colors)
                    {
                        result.ColorDistribution.TryGetValue(c, out var current);
                        result.ColorDistribution[c] = current + count;
                    }
                }
                else
                {
                    // Colorless card (no colored mana symbols in cost)
                    result.ColorDistribution.TryGetValue("Colorless", out var current);
                    result.ColorDistribution["Colorless"] = current + count;
                }
            }

            // --- Type distribution ---
            if (type != null)
            {
                if (HasType(type, _landTypes))
                {
                    result.LandCount += count;
                }
                else if (HasType(type, _creatureTypes))
                {
                    result.CreatureCount += count;

                    // Extract creature subtypes with accumulated counts
                    var subTypes = ExtractCreatureSubtypes(type);
                    foreach (var st in subTypes)
                    {
                        creatureSubtypeCounts.TryGetValue(st, out var subtypeCurrent);
                        creatureSubtypeCounts[st] = subtypeCurrent + count;
                    }
                }
                else
                {
                    result.NonCreatureCount += count;
                }
            }

            // --- Keyword abilities (accumulated counts) ---
            var oracleText = baseCard.OracleText;
            if (!string.IsNullOrEmpty(oracleText))
            {
                foreach (var ability in Abilities.GetAbilities())
                {
                    if (ability.IsKeyword && ability.Match(oracleText))
                    {
                        abilityCounts.TryGetValue(ability.DisplayName, out var abilityCurrent);
                        abilityCounts[ability.DisplayName] = abilityCurrent + count;
                    }
                }
            }

            // --- Max repeats tracking ---
            // Track unique card names and their max count
        }

        // Compute max repeats from cards (count per unique card name)
        var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in cards)
        {
            if (!card.Card.IsBasicLand())
            {
                var name = card.Card.Name;
                if (nameCounts.TryGetValue(name, out var existing))
                    nameCounts[name] = existing + card.Count;
                else
                    nameCounts[name] = card.Count;
            }
        }

        result.MaxRepeats = nameCounts.Any() ? nameCounts.Values.Max() : 0;
        result.TotalCards = cards.Sum(c => c.Count);
        result.CreatureSubtypes = creatureSubtypeCounts;
        result.AbilityStats = abilityCounts;

        return result;
    }

    public int[] ComputeManaCurve(IEnumerable<MyCard> cards)
    {
        var curve = new int[8];
        foreach (var card in cards)
        {
            if (IsLand(card.Card) || string.IsNullOrEmpty(card.Card.ManaCost) || !card.Card.CmcNumber.HasValue)
                continue;

            var cmc = card.Card.CmcNumber.Value;
            if (cmc >= 0 && cmc <= 6)
                curve[cmc] += card.Count;
            else if (cmc >= 7)
                curve[7] += card.Count;
        }
        return curve;
    }

    // === Helpers ===

    /// <summary>True when the card's type line carries a land supertype token ("land" / "basic").</summary>
    public static bool IsLand(Card card)
    {
        var type = card.Type;
        if (type == null) return false;
        return HasType(type, _landTypes);
    }

    private static bool HasType(string typeText, HashSet<string> knownTypes)
    {
        foreach (var kt in knownTypes)
        {
            // Check if the card's type line contains this type
            // MTG types are separated by spaces or dashes (e.g., "Creature - Human Warrior")
            var parts = typeText.Split(new[] { ' ', '-', ':' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Equals(kt, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Extracts creature subtypes from a type line.
    /// E.g., "Creature - Human Warrior" → ["Human", "Warrior"]
    /// E.g., "Legendary Creature — Vampire Noble" → ["Vampire", "Noble"]
    /// Double-faced cards join faces with "//" ("Creature — Cat // Creature — Rat"): each
    /// face is parsed independently, only faces whose supertype part names "Creature"
    /// contribute subtypes (the "Instant — Trap" half of a DFC must not leak "Trap" into
    /// the creature chart), and the face separator itself never reaches the result.
    /// </summary>
    private static readonly (string Token, int Length)[] SubtypeSeparators =
    {
        (" - ", 3),
        (" :", 2),
        (" \u2014 ", 3), // em dash
        ("\u2014", 1),   // em dash without spaces
    };

    private static IEnumerable<string> ExtractCreatureSubtypes(string typeLine)
    {
        foreach (var face in typeLine.Split(new[] { "//" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Earliest separator wins; skip its FULL length. The old code advanced one char
            // past the match start, leaking the dash itself into the list as a bare "—" token.
            var sepPos = -1;
            var sepLen = 0;
            foreach (var (token, length) in SubtypeSeparators)
            {
                var idx = face.IndexOf(token, StringComparison.Ordinal);
                if (idx >= 0 && (sepPos < 0 || idx < sepPos))
                {
                    sepPos = idx;
                    sepLen = length;
                }
            }
            if (sepPos < 0) continue; // no subtype section on this face ("Creature", "Instant", …)

            // Gate on THIS face's supertype part — a DFC contributes only its creature half.
            if (!HasType(face[..sepPos], _creatureTypes)) continue;

            var subTypePart = face[(sepPos + sepLen)..].Trim();
            foreach (var part in subTypePart.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                // Defensive: a stray separator can never become a subtype name again.
                if (part is "-" or "\u2014" or ":") continue;
                yield return part;
            }
        }
    }
}
