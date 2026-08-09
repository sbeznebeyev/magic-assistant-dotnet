using Dapper;
using Microsoft.Data.Sqlite;
using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Data.Db;

/// <summary>
/// SQLite-backed key-value store for user settings.
/// Table UserSettings (Key TEXT PK, Value TEXT) is created by InitializeDb.EnsureCreatedAsync().
/// Uses per-request connections with ADO.NET pooling — safe for Blazor Server circuits.
/// </summary>
public class SqliteUserSettingsStore : IUserSettingsStore
{
    private readonly string _dbPath;

    public SqliteUserSettingsStore(string dbPath)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath};Default Timeout=30");
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var sql = "SELECT Value FROM UserSettings WHERE Key = @Key";
        return await conn.QuerySingleOrDefaultAsync<string?>(sql, new { Key = key });
    }

    public async Task<bool> SetValueAsync(string key, string value, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var sql = @"
            INSERT INTO UserSettings (Key, Value) VALUES (@Key, @Value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value";

        var rows = await conn.ExecuteAsync(sql, new { Key = key, Value = value });
        return rows > 0;
    }

    public async Task<bool> DeleteValueAsync(string key, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var sql = "DELETE FROM UserSettings WHERE Key = @Key";
        var rows = await conn.ExecuteAsync(sql, new { Key = key });
        return rows > 0;
    }
}
