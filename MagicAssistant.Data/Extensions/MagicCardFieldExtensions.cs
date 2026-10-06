

using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model.Enums;
using System.Xml;

namespace MagicAssistant.Data.Model;

public static class MagicCardFieldExtensions
{
    /// <summary>
    /// Gets the value of this field from a MagicCard.
    /// Mirrors Java MagicCardField.get(IMagicCard).
    /// </summary>
    public static object? Get(this MagicCardField field, IMagicCard card)
    {
        return field switch
        {
            // Base card fields — direct property access
            MagicCardField.Id => card.Id,
            MagicCardField.Name => card.Name,
            MagicCardField.ManaCost => card.ManaCost,
            MagicCardField.Layout => (int)card.Layout,
            MagicCardField.Type => card.Type,
            MagicCardField.Power => card.Power,
            MagicCardField.Toughness => card.Toughness,
            MagicCardField.Set => card.SetCode,
            MagicCardField.Rarity => card.Rarity,
            MagicCardField.OracleText => card.OracleText,
            MagicCardField.Artist => card.Artist,
            MagicCardField.Language => card.Language,
            MagicCardField.CollectorNumber => card.GetBase().CollectorNumber,
            MagicCardField.Cmc => card.CmcNumber,
            MagicCardField.ColorType => card.ColorType,
            MagicCardField.Text => card.Text,
            MagicCardField.Side => card.Side,
            MagicCardField.CommunityRating => card.CommunityRating,
            MagicCardField.EnglishName => card.EnglishName,
            MagicCardField.Edition => card.CardSet?.SetCode,

            // Extended fields — from properties
            MagicCardField.Rulings => card.GetBase().Rulings,
            MagicCardField.Legality => card.LegalityMap,
            MagicCardField.Colors => card.GetBase().Colors,
            MagicCardField.ColorIdentity => card.GetBase().ColorIdentity,

            // Computed fields
            MagicCardField.SetBlock => null, // Block no longer tracked (not in Scryfall set data)
            MagicCardField.SetCore => card.CardSet?.SetType,
            MagicCardField.SetRelease => card.CardSet?.ReleaseDate,
            MagicCardField.UniqueCount => card.UniqueCount,
            MagicCardField.Size => GetSize(card),
            MagicCardField.Sideboard => card.IsSideboard,
            MagicCardField.EnglishType => card.Type,

            // Physical fields — delegate to physical methods
            MagicCardField.MyPrice => GetMyPrice(card),
            MagicCardField.Count => GetPhysicalCount(card),
            MagicCardField.IsOwn => GetIsOwn(card),
            MagicCardField.Location => card.Location,
            MagicCardField.Comment => GetComment(card),
            MagicCardField.Special => GetSpecial(card),
            MagicCardField.DateAdded => GetDateAdded(card),
            MagicCardField.Custom => GetCustom(card),
            MagicCardField.ForTradeCount => GetForTradeCount(card),
            MagicCardField.Grade => GetGrade(card),

            // Derived/computed
            MagicCardField.OwnCount => card.OwnCount,
            MagicCardField.OwnTotal => card.OwnTotal,
            MagicCardField.OwnTotalAll => card.OwnTotalAll,
            MagicCardField.OwnUnique => card.OwnUnique,
            MagicCardField.CreatureCount => GetCreatureCount(card),
            MagicCardField.Count4 => Math.Min(GetPhysicalCount(card), 4),
            MagicCardField.PercentComplete => ComputePercentComplete(card),
            MagicCardField.Percent4Complete => ComputePercent4Complete(card),

            _ => null,
        };
    }

    /// <summary>
    /// Sets the value of this field on a MagicCard.
    /// Mirrors Java MagicCardField.setM(MagicCard, Object).
    /// </summary>
    public static void SetM(this MagicCardField field, MagicCard card, object? value)
    {
        switch (field)
        {
            case MagicCardField.Id:
                card.Id = value is not null ? (Guid)value : throw new ArgumentNullException(nameof(value));
                break;
            case MagicCardField.Name:
                card.Name = value?.ToString() ?? string.Empty;
                break;
            case MagicCardField.ManaCost:
                card.ManaCost = value?.ToString() ?? string.Empty;
                break;
            case MagicCardField.Layout:
                if (value is int layoutInt)
                    card.Layout = (LayoutEnum)layoutInt;
                break;
            case MagicCardField.Type:
                card.Type = value?.ToString();
                break;
            case MagicCardField.Power:
                var power = value?.ToString();
                card.Power = string.IsNullOrWhiteSpace(power) ? "" : power;
                break;
            case MagicCardField.Toughness:
                var toughness = value?.ToString();
                card.Toughness = string.IsNullOrWhiteSpace(toughness) ? "" : toughness;
                break;
            case MagicCardField.Set:
                card.SetCode = value?.ToString();
                break;
            case MagicCardField.Rarity:
                card.Rarity = value?.ToString();
                break;
            case MagicCardField.OracleText:
                card.OracleText = value?.ToString();
                break;
            case MagicCardField.Artist:
                card.Artist = value?.ToString();
                break;
            case MagicCardField.Language:
                card.Language = value?.ToString();
                break;
            case MagicCardField.CollectorNumber:
                card.CollectorNumber = value?.ToString();
                break;
            case MagicCardField.CommunityRating:
                card.CommunityRating = CastToFloat(value);
                break;
            case MagicCardField.Rulings:
                card.Rulings = value?.ToString();
                break;

            // Extended fields — direct property assignment
            case MagicCardField.Side:
                card.Side = CastToInt(value);
                break;
            case MagicCardField.Legality:
                if (value is Legality.LegalityMap lm)
                    card.LegalityMap = lm;
                break;

            // Physical fields — stored via properties on physical cards
            case MagicCardField.Count:
            case MagicCardField.IsOwn:
            case MagicCardField.Location:
            case MagicCardField.Comment:
            case MagicCardField.Special:
            case MagicCardField.DateAdded:
            case MagicCardField.Custom:
            case MagicCardField.ForTradeCount:
                // These are set on MagicCardPhysical, not MagicCard
                break;

            default:
                throw new ArgumentException($"Field {field} cannot be set on MagicCard");
        }
    }

    /// <summary>
    /// Sets the value of this field on a MagicCardPhysical.
    /// Mirrors Java MagicCardField.setM(MagicCardPhysical, Object).
    /// </summary>
    public static void SetM(this MagicCardField field, MagicCardPhysical physical, object? value)
    {
        if (!field.IsPhysical())
        {
            // Delegate to base card
            field.SetM(physical.Card, value);
            return;
        }

        switch (field)
        {
            case MagicCardField.Count:
                physical.Count = CastToInt(value);
                break;
            case MagicCardField.IsOwn:
                physical.IsOwn = value is bool b ? b : bool.Parse(value?.ToString() ?? "false");
                break;
            case MagicCardField.Location:
                if (value is Location loc)
                    physical.Location = loc;
                else
                    physical.Location = Location.FromPath(value?.ToString() ?? string.Empty);
                break;
            case MagicCardField.Comment:
                physical.Comment = value?.ToString();
                break;
            case MagicCardField.Special:
                physical.SpecialTags = value?.ToString();
                break;
            case MagicCardField.DateAdded:
                if (value is DateTimeOffset dto)
                    physical.DateAdded = dto;
                else if (value is DateTime dt)
                    physical.DateAdded = dt;
                else if (value is string s && DateTimeOffset.TryParse(s, out var parsed))
                    physical.DateAdded = parsed;
                break;
            case MagicCardField.Custom:
                physical.Custom = value?.ToString();
                break;
            case MagicCardField.ForTradeCount:
                physical.SetForTradeCount(CastToInt(value));
                break;
            case MagicCardField.Grade:
                physical.Grade = value switch
                {
                    null => null,
                    CardGrade g => g,
                    int i => (CardGrade)i,
                    _ => Enum.TryParse<CardGrade>(value.ToString(), out var parsed) ? parsed : null
                };
                break;
            case MagicCardField.MyPrice:
                physical.MyPrice = CastToFloat(value);
                break;
        }
    }

    /// <summary>
    /// Gets the label (display name) for a field.
    /// Mirrors Java MagicCardField.getLabel().
    /// </summary>
    public static string GetLabel(this MagicCardField field)
    {
        var name = field.ToString();
        // Convert UPPER_CASE to Title Case
        var parts = name.Split('_');
        return string.Join(" ", parts.Select(p => p.Length > 0 ? char.ToLowerInvariant(p[0]) + p.Substring(1) : ""));
    }

    /// <summary>
    /// Checks if this field is a physical card field.
    /// Mirrors Java MagicCardField.isPhysical().
    /// </summary>
    public static bool IsPhysical(this MagicCardField field)
    {
        return field is >= MagicCardField.Count and <= MagicCardField.Grade;
    }

    /// <summary>
    /// Checks if this field is transient (not persisted).
    /// Mirrors Java MagicCardField.isTransient().
    /// </summary>
    public static bool IsTransient(this MagicCardField field)
    {
        return field is MagicCardField.CreatureCount
            or MagicCardField.Count4
            or MagicCardField.PercentComplete
            or MagicCardField.Percent4Complete
            or MagicCardField.Error
            or MagicCardField.OwnCount
            or MagicCardField.OwnTotal
            or MagicCardField.OwnTotalAll
            or MagicCardField.OwnUnique
            or MagicCardField.Sideboard;
    }

    /// <summary>
    /// Returns all non-transient fields, optionally filtered by physical/non-physical.
    /// Mirrors Java MagicCardField.allNonTransientFields().
    /// </summary>
    public static ICollection<MagicCardField> AllNonTransientFields(bool physical)
    {
        return Enum.GetValues<MagicCardField>()
            .Where(f => !f.IsTransient())
            .Where(f => physical || !f.IsPhysical())
            .ToList();
    }

    /// <summary>
    /// Looks up a field by name (case-insensitive).
    /// Mirrors Java MagicCardField.fieldByName().
    /// </summary>
    public static MagicCardField? FieldByName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        // Direct match
        if (Enum.TryParse<MagicCardField>(name, ignoreCase: true, out var field))
            return field;

        // Aliases
        return name.ToLowerInvariant() switch
        {
            "edition" => MagicCardField.Set,
            "qty" => MagicCardField.Count,
            "price" => MagicCardField.MyPrice, // legacy alias for the per-instance price (Deck.MyPrice)
            _ => null,
        };
    }

    // === Helper methods for physical card fields ===
    private static float GetMyPrice(IMagicCard card) => card is MagicCardPhysical p ? p.MyPrice : 0f;
    private static int GetPhysicalCount(IMagicCard card) => card.IsPhysical ? card.Count : 1;
    private static bool GetIsOwn(IMagicCard card) => card is MagicCardPhysical pcp ? pcp.IsOwn : false;
    private static string? GetComment(IMagicCard card) => card is MagicCardPhysical pcp ? pcp.Comment : null;
    private static string? GetSpecial(IMagicCard card) => card is MagicCardPhysical pcp ? pcp.SpecialTags : null;
    private static DateTimeOffset? GetDateAdded(IMagicCard card) => card is MagicCardPhysical pcp ? pcp.DateAdded : null;
    private static string? GetCustom(IMagicCard card) => card is MagicCardPhysical pcp ? pcp.Custom : null;
    private static int GetForTradeCount(IMagicCard card) => card is MagicCardPhysical pcp ? pcp.GetForTradeCount() : 0;
    private static object? GetGrade(IMagicCard card) => card is MagicCardPhysical p ? (object?)p.Grade : null;
    private static int GetCreatureCount(IMagicCard card)
    {
        if (card.Power == null || string.IsNullOrEmpty(card.Power)) return 0;
        return card.IsPhysical ? Math.Max(1, card.Count) : 1;
    }

    /// <summary>
    /// Returns the number of parts this card has (1 for normal, 2+ for split/flip/adventure).
    /// Mirrors Java MagicCard.getSize().
    /// </summary>
    private static int GetSize(IMagicCard card)
    {
        // Split/flip cards have multiple faces via CardFaces array.
        // Single-faced cards return 1.
        if (card.GetBase().CardFaces is { Length: > 0 })
            return card.GetBase().CardFaces.Length;
        return 1;
    }

    /// <summary>
    /// Computes collector completion percentage for this card.
    /// Based on owned copies vs. total available unique printings in the set.
    /// Mirrors Java MagicCard.getPercentComplete().
    /// Returns 0-100 float.
    /// </summary>
    private static float ComputePercentComplete(IMagicCard card)
    {
        var ownCount = card.OwnCount;
        if (ownCount == 0)
            return 0f;

        // If we have at least 1 copy, we "own" this card → 100% for this printing
        return 100f;
    }

    /// <summary>
    /// Computes "4-of" completion percentage for this card.
    /// Each copy contributes 25%, capped at 100% (4 copies).
    /// Mirrors Java MagicCard.getPercent4Complete().
    /// </summary>
    private static float ComputePercent4Complete(IMagicCard card)
    {
        var ownCount = card.OwnCount;
        return Math.Min(ownCount, 4) * 25f;
    }

    private static float CastToFloat(object? value)
    {
        if (value == null) return 0f;
        if (value is float f) return f;
        if (value is string s) return float.Parse(s);
        throw new InvalidCastException(value.GetType().ToString());
    }

    private static int CastToInt(object? value)
    {
        if (value == null) return 0;
        if (value is int i) return i;
        if (value is string s) return int.Parse(s);
        throw new InvalidCastException(value.GetType().ToString());
    }
}

