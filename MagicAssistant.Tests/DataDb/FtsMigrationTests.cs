using Dapper;
using Microsoft.Data.Sqlite;

namespace MagicAssistant.Tests.DataDb;

/// <summary>
/// The one test that exercises the LEGACY FTS index shape (CardId UNINDEXED, duplicated text).
/// Standing rule: the app NEVER migrates — it must refuse to start against the legacy index.
/// Tests do NOT execute migrations either; applying the migration file is a manual action
/// (or Kira's, on explicit request). This fixture only proves the refusal works.
/// </summary>
[TestFixture]
public class FtsMigrationTests
{
    private string _dbPath = "";

    [SetUp]
    public async Task SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test-ftsmig-{Guid.NewGuid()}.db");

        // Create with the current initializer, insert a card through the store (schema-safe),
        // then DOWNGRADE the FTS index to the legacy shape — CardId column, duplicated text,
        // old triggers — with its contents backfilled, exactly like a pre-migration DB.
        await InitializeDb.EnsureCreatedAsync(_dbPath);
        var store = new SqliteCardStore(_dbPath);
        await store.AddAllAsync(new[]
        {
            new Card { Id = Guid.NewGuid(), Name = "Lightning Bolt", ManaCost = "{X}{R}", Type = "Instant", SetCode = "m19", Rarity = "Common" },
        });
        await store.DisposeAsync();

        using var conn = new SqliteConnection($"Data Source={_dbPath};Default Timeout=30");
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            DROP TRIGGER IF EXISTS cards_fts_ai;
            DROP TRIGGER IF EXISTS cards_fts_ad;
            DROP TRIGGER IF EXISTS cards_fts_ud;
            DROP TABLE CardsFts;
            CREATE VIRTUAL TABLE CardsFts USING fts5(
                CardId UNINDEXED, Name, Type, OracleText, Artist
            );
            INSERT INTO CardsFts(CardId, Name, Type, OracleText, Artist)
            SELECT Id, Name, Type, OracleText, Artist FROM Cards;
            CREATE TRIGGER cards_fts_ai AFTER INSERT ON Cards
            BEGIN
                INSERT INTO CardsFts(CardId, Name, Type, OracleText, Artist)
                VALUES (new.Id, new.Name, new.Type, new.OracleText, new.Artist);
            END;");
    }

    [TearDown]
    public async Task TearDown()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            var file = _dbPath + suffix;
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Test]
    public async Task LegacyIndex_AppRefusesToStart()
    {
        // Standing rule: no automatic migrations — by the app or by anyone else. The initializer
        // must abort with a pointer to the migration file rather than silently touching data.
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await InitializeDb.EnsureCreatedAsync(_dbPath));

        Assert.That(ex!.Message, Does.Contain("004_Fts_External_Content_Index.sql"),
            "error must name the migration file to apply");
    }
}
}
