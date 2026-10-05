namespace MagicAssistant.Data.Model;

/// <summary>Parsing boundaries between wire strings and <see cref="CardSetType"/>.</summary>
public static class CardSetTypes
{
    /// <summary>
    /// Strict parse for inbound Scryfall data. Throws on unknown values instead of letting
    /// a sentinel be persisted into the TEXT column.
    /// </summary>
    public static CardSetType Parse(string? value)
        => Enum.TryParse<CardSetType>(value, ignoreCase: true, out var parsed) && parsed != CardSetType.unknown
            ? parsed
            : throw new ArgumentException($"Unknown set type '{value}'. Extend CardSetType before syncing.", nameof(value));

    /// <summary>
    /// Lenient parse for DB reads. Unknown or null values map to
    /// <see cref="CardSetType.unknown"/> instead of throwing.
    /// </summary>
    public static CardSetType FromDb(string? value)
        => Enum.TryParse<CardSetType>(value, ignoreCase: true, out var parsed) ? parsed : CardSetType.unknown;
}
