using System.Text.RegularExpressions;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Expressions;

/// <summary>
/// Static utility for matching card fields against text patterns.
/// Mirrors Java MagicCard.matches(ICardField, TextValue).
///
/// Lives in Core layer because it depends on TextValue (expression system).
/// The Data layer has no knowledge of regex/matching semantics.
/// </summary>
public static class CardMatcher
{
    /// <summary>
    /// Matches a card's field value against a text pattern with full flag support.
    /// 
    /// - TYPE field: uses CardTypes.HasType() for primary-type matching (non-regex mode)
    /// - Other fields: uses TextValue.GetPattern() regex with wordBoundary/caseSensitive/isRegex flags
    /// </summary>
    public static bool Matches(IMagicCard card, MagicCardField field, TextValue textValue)
    {
        var value = GetFieldStringValue(card, field);

        // Special handling for TYPE field in non-regex mode:
        // Java uses CardTypes.hasType() which checks primary type (word before " — ")
        if (field == MagicCardField.Type && !textValue.IsRegex)
        {
            return TypeMatches(card, textValue.GetString());
        }

        // All other fields: use TextValue's compiled regex pattern
        // This properly handles wordBoundary, caseSensitive, and isRegex flags
        return textValue.GetPattern().IsMatch(value);
    }

    /// <summary>
    /// Legacy overload for simple string matching (no flags).
    /// Used when TextValue is not available. Falls back to case-insensitive contains.
    /// </summary>
    public static bool MatchesSimple(IMagicCard card, MagicCardField field, string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return true;

        var value = GetFieldStringValue(card, field);
        if (string.IsNullOrEmpty(value)) return false;

        // Special handling for TYPE field: check primary type
        if (field == MagicCardField.Type)
            return TypeMatches(card, pattern);

        // Default: case-insensitive substring match
        return value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Gets the string representation of a card field value.
    /// </summary>
    private static string GetFieldStringValue(IMagicCard card, MagicCardField field)
    {
        var obj = field.Get(card);
        return obj?.ToString() ?? "";
    }

    /// <summary>
    /// Checks if the card's primary type matches the given pattern.
    /// Mirrors Java CardTypes.hasType() — checks the primary type (word before " — ").
    /// For example: "Creature — Human Warrior" → primary type is "Creature".
    /// </summary>
    private static bool TypeMatches(IMagicCard card, string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return true;

        var fullType = card.Type ?? "";
        if (string.IsNullOrEmpty(fullType)) return false;

        // Extract primary type (first word before " — ")
        var dashIdx = fullType.IndexOf(" — ");
        var primaryType = dashIdx >= 0 ? fullType[..dashIdx] : fullType;

        // Case-insensitive check: does the primary type contain the pattern?
        return primaryType.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0
            || fullType.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
