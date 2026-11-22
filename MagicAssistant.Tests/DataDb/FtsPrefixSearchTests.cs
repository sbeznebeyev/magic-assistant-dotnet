using Dapper;
using Microsoft.Data.Sqlite;

namespace MagicAssistant.Tests.DataDb;

/// <summary>
/// FTS prefix semantics: bare words match by prefix (fly → flying), quoted phrases stay exact.
/// </summary>
[TestFixture]
public class FtsPrefixSearchTests
{
    private string _dbPath = "";
    private SqliteCardStore _store = null!;
    private Guid _fly, _flying;

    [SetUp]
    public async Task SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test-fts-{Guid.NewGuid()}.db");
        await InitializeDb.EnsureCreatedAsync(_dbPath);
        _store = new SqliteCardStore(_dbPath);

        _fly = Guid.NewGuid();
        _flying = Guid.NewGuid();
        await _store.AddAllAsync(new[]
        {
            new Card { Id = _fly, Name = "Fly Swarm", ManaCost = "{1}", Type = "Instant", SetCode = "m19", Rarity = "Common" },
            new Card { Id = _flying, Name = "Flying Carpet", ManaCost = "{1}", Type = "Artifact", SetCode = "m19", Rarity = "Common" },
        });
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
    public async Task BareWord_MatchesByPrefix()
    {
        var result = await Search("fly");

        Assert.That(result.Cards.Select(c => c.Id), Is.EquivalentTo(new[] { _fly, _flying }),
            "\"fly\" must find both Fly and Flying");
    }

    [Test]
    public async Task QuotedWord_StaysExact()
    {
        var result = await Search("\"fly\"");

        Assert.That(result.Cards.Select(c => c.Id), Is.EquivalentTo(new[] { _fly }),
            "quoted \"fly\" matches only the exact word");
    }

    [Test]
    public async Task ColumnFilter_GetsPrefixToo()
    {
        var result = await Search("name:fly");

        Assert.That(result.Cards.Select(c => c.Id), Is.EquivalentTo(new[] { _fly, _flying }),
            "name:fly keeps prefix semantics inside the column filter");
    }

    [Test]
    public async Task BooleanOperators_StillWork()
    {
        var result = await Search("fly OR carpet");

        Assert.That(result.Cards.Select(c => c.Id), Is.EquivalentTo(new[] { _fly, _flying }));
    }
}
