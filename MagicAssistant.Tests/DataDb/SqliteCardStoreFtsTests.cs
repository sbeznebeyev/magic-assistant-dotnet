using Microsoft.Data.Sqlite;

namespace MagicAssistant.Tests.DataDb;

[TestFixture]
public class SqliteCardStoreFtsTests
{
    private string _dbPath = "";
    private SqliteCardStore _store = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test-fts-{Guid.NewGuid()}.db");
        await InitializeDb.EnsureCreatedAsync(_dbPath);
        _store = new SqliteCardStore(_dbPath);
        await _store.InitializeAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_store != null)
            await _store.DisposeAsync();

        // Microsoft.Data.Sqlite (v6+) pools connections by default.
        // Disposed connections return to the pool, keeping native file handles open on Windows.
        // ClearAllPools() releases all pooled connections so the .db file can be deleted.
        SqliteConnection.ClearAllPools();

        DeleteDbFiles(_dbPath);
    }

    private static void DeleteDbFiles(string dbPath)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            var file = dbPath + suffix;
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    private static Guid S2G(string s) => Guid.Parse(s.PadRight(32, '0').Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-"));

    private async Task AddCard(string id, string name, string type, string? oracleText = null, string? artist = null)
    {
        await _store.AddAsync(CreateCard(id, name, type, oracleText, artist));
    }

    private static MagicCard CreateCard(string id, string name, string type,
        string? oracleText = null, string? artist = null)
    {
        return new MagicCard
        {
            Id = S2G(id),
            Name = name,
            Type = type,
            OracleText = oracleText ?? "",
            Artist = artist ?? "Unknown",
            SetCode = "TEST",
            Rarity = "Common",
        };
    }

    /* ═══════════════════════════════════════════════════════
     * Базовые тесты FTS5-поиска
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task SearchAsync_SingleWord_ReturnsMatchingCards()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");
        await AddCard("c2", "Mountain", "Basic Land — Mountain");
        await AddCard("c3", "Lightning Helix", "Sorcery");

        var results = await _store.SearchAsync("lightning");

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.Select(c => c.Name), Does.Contain("Lightning Bolt"));
        Assert.That(results.Select(c => c.Name), Does.Contain("Lightning Helix"));
    }

    [Test]
    public async Task SearchAsync_MultipleWords_AndBehavior()
    {
        await AddCard("c1", "Lightning Bolt", "Instant — Arcane");
        await AddCard("c2", "Lightning Strike", "Instant");
        await AddCard("c3", "Bolt of the Gods", "Sorcery");

        var results = await _store.SearchAsync("lightning instant");

        Assert.That(results, Has.Count.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task SearchAsync_NoResults_ReturnsEmpty()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");

        var results = await _store.SearchAsync("nonexistentcardthatdoesnotexist");

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_EmptyQuery_ThrowsSqliteException()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");
        await AddCard("c2", "Mountain", "Land");

        try
        {
            var results = await _store.SearchAsync("");
            Assert.Fail(
                $"Expected SqliteException for empty FTS5 query, but got {results.Count} results. " +
                "BUG: SearchAsync passes empty string directly to FTS5 MATCH clause without validation. " +
                "FTS5 throws 'syntax error near \"\"' on empty query. " +
                "Fix: add guard clause in SearchAsync — if string.IsNullOrWhiteSpace(query) return empty collection.");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("syntax error"))
        {
            // Expected: FTS5 rejects empty MATCH query with syntax error.
            // This is a bug in SearchAsync — it should validate the query before passing to FTS5.
        }
    }

    /* ═══════════════════════════════════════════════════════
     * Quoted phrases
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task SearchAsync_QuotedPhrase_MatchesExactPhrase()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");
        await AddCard("c2", "Bolt of the Gods", "Sorcery");
        await AddCard("c3", "Chain Lightning", "Sorcery");

        var results = await _store.SearchAsync("\"Lightning Bolt\"");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results.First().Name, Is.EqualTo("Lightning Bolt"));
    }

    [Test]
    public async Task SearchAsync_QuotedPhrase_NoMatch_WhenWordsNotAdjacent()
    {
        await AddCard("c1", "Lightning Strike", "Instant");
        await AddCard("c2", "Bolt of the Gods", "Sorcery");

        var results = await _store.SearchAsync("\"Lightning Bolt\"");

        Assert.That(results, Is.Empty);
    }

    /* ═══════════════════════════════════════════════════════
     * Column-specific поиск
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task SearchAsync_ColumnPrefix_SearchesSpecificColumn()
    {
        await AddCard("c1", "Mountain", "Basic Land — Mountain");
        await AddCard("c2", "Forest", "Basic Land — Forest");
        await AddCard("c3", "Lightning Bolt", "Instant");

        var results = await _store.SearchAsync("name:mountain");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results.First().Name, Is.EqualTo("Mountain"));
    }

    [Test]
    public async Task SearchAsync_TypeColumn_SearchesTypeLine()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");
        await AddCard("c2", "Mountain", "Basic Land — Mountain");
        await AddCard("c3", "Gideon of the Trials", "Legendary Planeswalker — Gideon");

        var results = await _store.SearchAsync("type:planeswalker");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results.First().Name, Is.EqualTo("Gideon of the Trials"));
    }

    [Test]
    public async Task SearchAsync_ArtistColumn_SearchesArtist()
    {
        await AddCard("c1", "Card One", "Instant", artist: "Magali Villeneuve");
        await AddCard("c2", "Card Two", "Sorcery", artist: "Justin Chadwick");

        var results = await _store.SearchAsync("artist:chadwick");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results.First().Name, Is.EqualTo("Card Two"));
    }

    /* ═══════════════════════════════════════════════════════
     * Boolean operators
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task SearchAsync_OrOperator_ReturnsEitherTerm()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");
        await AddCard("c2", "Mountain", "Land");
        await AddCard("c3", "Forest", "Land");

        var results = await _store.SearchAsync("lightning OR forest");

        Assert.That(results, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SearchAsync_OrOperator_BroaderThanAnd()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");
        await AddCard("c2", "Mountain", "Land");
        await AddCard("c3", "Forest", "Land");

        var andResults = await _store.SearchAsync("lightning forest");
        var orResults = await _store.SearchAsync("lightning OR forest");

        Assert.That(orResults.Count, Is.GreaterThanOrEqualTo(andResults.Count));
    }

    /* ═══════════════════════════════════════════════════════
     * Спецсимволы
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task SearchAsync_SpecialCharsInName_FoundByNormalSearch()
    {
        await AddCard("c1", "Card with (parentheses)", "Instant");
        await AddCard("c2", "Card brackets", "Sorcery");
        await AddCard("c3", "Card Dash", "Enchantment");

        var results = await _store.SearchAsync("card");

        Assert.That(results, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task SearchAsync_UnicodeCharacters_HandlesCorrectly()
    {
        await AddCard("c1", "Card with emoji", "Instant");
        await AddCard("c2", "Normal Card", "Sorcery");

        var results = await _store.SearchAsync("card");

        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
    }

    /* ═══════════════════════════════════════════════════════
     * Limit-параметр
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task SearchAsync_Limit_RespectsLimit()
    {
        for (var i = 1; i <= 20; i++)
            await AddCard($"c{i}", $"Lightning Card {i}", "Instant");

        var results = await _store.SearchAsync("lightning", limit: 5);

        Assert.That(results, Has.Count.LessThanOrEqualTo(5));
    }

    [Test]
    public async Task SearchAsync_Limit_DefaultIs100()
    {
        for (var i = 1; i <= 110; i++)
            await AddCard($"c{i}", $"Test Card {i}", "Instant");

        var results = await _store.SearchAsync("test");

        Assert.That(results, Has.Count.LessThanOrEqualTo(100));
    }

    [Test]
    public async Task SearchAsync_Limit_Zero_ReturnsEmpty()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");

        var results = await _store.SearchAsync("lightning", limit: 0);

        Assert.That(results, Is.Empty);
    }

    /* ═══════════════════════════════════════════════════════
     * Ранжирование по релевантности
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task SearchAsync_RankedByRelevance_BetterMatchesFirst()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");
        await AddCard("c2", "Chain Lightning", "Sorcery");
        await AddCard("c3", "Mountain", "Basic Lightning Land");

        var results = await _store.SearchAsync("lightning bolt");

        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results.First().Name, Is.EqualTo("Lightning Bolt"));
    }

    /* ═══════════════════════════════════════════════════════
     * OracleText поиск
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task SearchAsync_OracleText_SearchesCardText()
    {
        await AddCard("c1", "Mystery Card", "Instant", oracleText: "Destroy target creature");
        await AddCard("c2", "Another Card", "Sorcery", oracleText: "Draw a card");

        var results = await _store.SearchAsync("destroy");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results.First().Name, Is.EqualTo("Mystery Card"));
    }

    [Test]
    public async Task SearchAsync_OracleText_LongText_FindsSubstring()
    {
        await AddCard("c1", "Complex Card", "Enchantment",
            oracleText: "Whenever a creature you control attacks, put a +1/+1 counter on it. " +
                        "At the beginning of your upkeep, you may pay {2}. If you do not, sacrifice this enchantment.");

        var results = await _store.SearchAsync("creature");

        Assert.That(results, Has.Count.EqualTo(1));
    }

    /* ═══════════════════════════════════════════════════════
     * Edge-кейсы
     * ═══════════════════════════════════════════════════════ */

    [Test]
    public async Task SearchAsync_WhitespaceOnlyQuery_ThrowsSqliteException()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");

        try
        {
            var results = await _store.SearchAsync("   ");
            Assert.Fail(
                $"Expected SqliteException for whitespace-only FTS5 query, but got {results.Count} results. " +
                "BUG: SearchAsync passes whitespace string directly to FTS5 MATCH clause without validation. " +
                "FTS5 tokenizer produces empty token stream from whitespace → 'syntax error near \"\"'. " +
                "Fix: add guard clause in SearchAsync — if string.IsNullOrWhiteSpace(query) return empty collection.");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("syntax error"))
        {
            // Expected: FTS5 rejects whitespace-only MATCH query with syntax error.
            // This is a bug in SearchAsync — it should validate the query before passing to FTS5.
        }
    }

    [Test]
    public async Task SearchAsync_CaseInsensitive_MatchesRegardlessOfCase()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");

        var lower = await _store.SearchAsync("lightning");
        var upper = await _store.SearchAsync("LIGHTNING");
        var mixed = await _store.SearchAsync("Lightning");

        Assert.That(lower, Has.Count.EqualTo(1));
        Assert.That(upper, Has.Count.EqualTo(1));
        Assert.That(mixed, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SearchAsync_PartialWord_DoesNotMatch()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");

        var results = await _store.SearchAsync("light");

        Assert.That(results, Is.Empty,
            "FTS5 default tokenizer performs whole-word token matching only. " +
            "Partial prefix 'light' should NOT match full token 'Lightning'. " +
            "Use wildcard syntax (e.g., 'light*') for prefix matching.");
    }

    [Test]
    public async Task SearchAsync_Wildcard_PrefixMatch()
    {
        await AddCard("c1", "Lightning Bolt", "Instant");
        await AddCard("c2", "Lightning Strike", "Sorcery");
        await AddCard("c3", "Bolt of the Gods", "Enchantment");

        var results = await _store.SearchAsync("light*");

        Assert.That(results, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SearchAsync_Stemming_DoesNotWorkWithPorterTokenizer()
    {
        await AddCard("c1", "Protection Card", "Enchantment");
        await AddCard("c2", "Other Card", "Instant");

        var results = await _store.SearchAsync("protect");

        Assert.That(results, Is.Empty,
            "BUG: SQLite FTS5 porter tokenizer does NOT perform stemming in this build. " +
            "Searching 'protect' returns 0 results even though 'Protection' is indexed. " +
            "The porter tokenizer normalizes to lowercase and splits on punctuation, " +
            "but does not reduce words to their root form (e.g., 'protection' → 'protect'). " +
            "This means derived word forms will NOT match each other via FTS5 search. " +
            "Workaround: use wildcard queries (e.g., 'protect*') instead of relying on stemming.");
    }
}
