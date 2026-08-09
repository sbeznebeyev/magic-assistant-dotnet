namespace MagicAssistant.Data.Contracts;

/// <summary>
/// Key-value store for persistent user settings.
/// Backed by a single SQLite table (UserSettings) with TEXT Key + TEXT Value columns.
/// </summary>
public interface IUserSettingsStore
{
    /// <summary>
    /// Get the value for a given key, or null if not found.
    /// </summary>
    Task<string?> GetValueAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Save or update a setting value. Inserts if key does not exist, updates if it does.
    /// </summary>
    Task<bool> SetValueAsync(string key, string value, CancellationToken ct = default);

    /// <summary>
    /// Delete a setting by key. Returns true if the row existed and was removed.
    /// </summary>
    Task<bool> DeleteValueAsync(string key, CancellationToken ct = default);
}
