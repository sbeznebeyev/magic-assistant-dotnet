namespace MagicAssistant.Data.Model;

/// <summary>
/// Per-set progress details for the CollectorView side panel: set identity
/// (name / release date / type) plus owned-vs-total breakdowns by card rarity
/// and by mana color. Ownership semantics match GetCollectorProgressRawAsync:
/// distinct CollectorNumber within the set, IsOwn = 1 across all decks.
/// </summary>
public sealed record SetProgressDetails(
    string SetCode,
    string? SetName,
    DateTime? ReleaseDate,
    CardSetType SetType,
    IReadOnlyList<SetGroupProgress> RarityGroups,
    IReadOnlyList<SetGroupProgress> ColorGroups);

/// <summary>One progress group (rarity or color): owned vs total distinct cards.</summary>
public sealed record SetGroupProgress(string Key, long Owned, long Total)
{
    /// <summary>Owned share of the group in percent (0 when the group is empty).</summary>
    public double Percent => Total > 0 ? Owned / (double)Total * 100 : 0;
}
