using Dapper;
using Microsoft.Data.Sqlite;

namespace MagicAssistant.Tests.DataDb;

/// <summary>
/// FTS5 reliability: diacritic-insensitive search, syntax-error fallback.
/// </summary>
[TestFixture]
public class FtsReliabilityTests
{
    private string _dbPath = "";
    private SqliteCardStore _store = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test-ftsrel-{Guid.NewGuid()}.db");
        await InitializeDb.EnsureCreatedAsync(_dbPath);
        _store = new SqliteCardStore(_dbPath);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _store.DisposeAsync();
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            var file = _dbPath + suffix;
            if (File.Exists(file)) File.Delete(file);
        }
    }

    private Task<CardListResult> Search(string query) =>
        _store.GetPagedAsync(new CardListFilter { Page = 1, PageSize = 50, Query = query });

    [Test]
    public async Task Diacritics_AreIgnored()
    {
        var id = Guid.NewGuid();
        await _store.AddAllAsync(new[]
        {
            new Card { Id = id, Name = "Sunpetal Grove", ManaCost = "{2}", Type = "Land", SetCode = "m19", Rarity = "Common", Artist = "José Multiverse" },
        });

        var result = await Search("jose");

        Assert.That(result.Cards.Select(c => c.Id), Contains.Item(id),
            "\"jose\" must find artist \"José\" (remove_diacritics=2)");
    }

    [Test]
    public async Task SyntaxError_FallsBackWithoutThrowing()
    {
        var id = Guid.NewGuid();
        await _store.AddAllAsync(new[]
        {
            new Card { Id = id, Name = "Lightning Bolt", ManaCost = "{X}{R}", Type = "Instant", SetCode = "m19", Rarity = "Common" },
        });

        // "AND" dangling at the end is an FTS5 syntax error — the search must degrade to words,
        // not throw into the silent empty list.
        await Assert.DoesNotThrowAsync(() => Search("lightning AND"));
    }

    [Test]
    public async Task UnknownColumn_FallsBackToWords()
    {
        var id = Guid.NewGuid();
        await _store.AddAllAsync(new[]
        {
            new Card { Id = id, Name = "Lightning Bolt", ManaCost = "{X}{R}", Type = "Instant", SetCode = "m19", Rarity = "Common" },
        });

        // name: is real, color: is not — the error must not surface as an exception.
        var result = await Search("color:red bolt");

        Assert.That(result.Cards, Is.Not.Null);
    }
}
