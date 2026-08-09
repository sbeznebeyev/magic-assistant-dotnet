namespace MagicAssistant.Data.Model;

/// <summary>
/// Raw collector progress row from a single SQL query.
/// </summary>
public record CollectorProgressRaw(
    string SetAbbreviation,
    string? SetName,
    long TotalCards,           // unique card names in KB for this set
    long CollectedCards,       // unique card names in any deck (owned + borrowed)
    long OwnedCards);          // unique card names the user owns across all decks
