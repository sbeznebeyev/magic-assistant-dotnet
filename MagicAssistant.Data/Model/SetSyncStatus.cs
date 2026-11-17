namespace MagicAssistant.Data.Model;

/// <summary>
/// Sync status of a set in the knowledge base.
/// Name — the set name as it was requested (full name from a legacy file).
/// SetCode — resolved abbreviation, null when the set is not present in CardSet at all.
/// CardCount — how many cards of this set are stored in Cards (0 = only metadata synced).
/// </summary>
public record SetSyncStatus(string Name, string? SetCode, string? SetName, int CardCount);
