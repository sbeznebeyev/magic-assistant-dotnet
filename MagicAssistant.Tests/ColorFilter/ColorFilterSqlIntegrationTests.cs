using Dapper;
using MagicAssistant.App.Shared;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Tests.ColorFilter;

/// <summary>
/// Integration tests that execute generated SQL against an in-memory SQLite database.
/// Catches bugs that unit tests miss: operator precedence, parenthesization, SQLite-specific behavior.
/// </summary>
[TestFixture]
public class ColorFilterSqlIntegrationTests
{
    private static Dictionary<ColorBitFlags, bool> EmptyBoxes()
    {
        var boxes = new Dictionary<ColorBitFlags, bool>();
        foreach (var flag in ColorFilterFlags.All)
            boxes[flag] = false;
        return boxes;
    }

    private static void Set(Dictionary<ColorBitFlags, bool> boxes, ColorBitFlags flag)
        => boxes[flag] = true;

    /// <summary>
    /// Runs the generated SQL WHERE clause against real SQLite and returns matching card IDs.
    /// </summary>
    private static List<int> EvaluateSqlAgainstSQLite(string? sqlWhere, Dictionary<int, int> testCards)
    {
        var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        conn.Open();

        try
        {
            // Create table and insert test data
            conn.Execute("CREATE TABLE Cards (Id INTEGER PRIMARY KEY, ColorBits INTEGER)");
            foreach (var kvp in testCards)
                conn.Execute("INSERT INTO Cards (Id, ColorBits) VALUES (@id, @bits)",
                    new { id = kvp.Key, bits = kvp.Value });

            var whereClause = !string.IsNullOrEmpty(sqlWhere) ? $"WHERE {sqlWhere}" : "";
            var sql = $"SELECT Id FROM Cards {whereClause}";

            return conn.Query<int>(sql).ToList();
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Test]
    public void SqlIntegration_MultiColorFilter_ExcludesMonoCards()
    {
        // Regression: SQLite operator precedence bug where "col & 62 - 1" parsed as "col & (62-1)"
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        Set(boxes, ColorBitFlags.U);
        Set(boxes, ColorBitFlags.MULTI);
        var filter = ColorFilterHelper.GetFilter(boxes, onlyMode: true)!;
        var sql = filter.ToSqlWhere()!;

        // Test cards: ID → ColorBits (int values)
        var testCards = new Dictionary<int, int>
        {
            { 1, 0x084 },  // Mono-blue (U|MONO) — should be EXCLUDED
            { 2, 0x082 },  // Mono-white (W|MONO) — should be EXCLUDED
            { 3, 0x046 },  // WU multi-color — should PASS
            { 4, 0x0CE },  // WUB multi-color — should be EXCLUDED (B in excludedMask)
            { 5, 0x001 },  // Colorless — should be EXCLUDED
        };

        var matching = EvaluateSqlAgainstSQLite(sql, testCards);

        Assert.That(matching, Does.Contain(3), "WU multi-color card must pass");
        Assert.That(matching, Does.Not.Contain(1), "Mono-blue card must NOT pass (regression)");
        Assert.That(matching, Does.Not.Contain(2), "Mono-white card must NOT pass");
        Assert.That(matching, Does.Not.Contain(4), "WUB card must NOT pass (B excluded)");
        Assert.That(matching, Does.Not.Contain(5), "Colorless card must NOT pass");
    }

    [Test]
    public void SqlIntegration_MonoColorFilter_ExcludesMultiCards()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.MONO);
        var filter = ColorFilterHelper.GetFilter(boxes)!;
        var sql = filter.ToSqlWhere()!;

        var testCards = new Dictionary<int, int>
        {
            { 1, 0x084 },  // Mono-blue — should PASS
            { 2, 0x046 },  // WU multi-color — should be EXCLUDED
            { 3, 0x001 },  // Colorless — should be EXCLUDED (no colored bits)
        };

        var matching = EvaluateSqlAgainstSQLite(sql, testCards);

        Assert.That(matching, Does.Contain(1), "Mono-blue card must pass");
        Assert.That(matching, Does.Not.Contain(2), "Multi-color card must NOT pass");
        Assert.That(matching, Does.Not.Contain(3), "Colorless card must NOT pass");
    }

    [Test]
    public void SqlIntegration_MultiOnly_NoBaseColors()
    {
        // Only MULTI checkbox — should match any multi-color card regardless of specific colors
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.MULTI);
        var filter = ColorFilterHelper.GetFilter(boxes)!;
        var sql = filter.ToSqlWhere()!;

        var testCards = new Dictionary<int, int>
        {
            { 1, 0x084 },  // Mono-blue — EXCLUDED
            { 2, 0x046 },  // WU multi-color — PASS
            { 3, 0x0CE },  // WUB multi-color — PASS
            { 4, 0x072 },  // WRG multi-color (W=2, R=16, G=32, MULTI=64) — PASS
        };

        var matching = EvaluateSqlAgainstSQLite(sql, testCards);

        Assert.That(matching, Does.Contain(2), "WU multi must pass");
        Assert.That(matching, Does.Contain(3), "WUB multi must pass");
        Assert.That(matching, Does.Contain(4), "WRG multi must pass");
        Assert.That(matching, Does.Not.Contain(1), "Mono-blue must NOT pass");
    }

    [Test]
    public void SqlIntegration_W_Only_AllowsMonoWhite()
    {
        // W + Only — should allow mono-white cards (W|MONO)
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        var filter = ColorFilterHelper.GetFilter(boxes, onlyMode: true)!;
        var sql = filter.ToSqlWhere()!;

        var testCards = new Dictionary<int, int>
        {
            { 1, 0x082 },  // Mono-white (W|MONO) — PASS
            { 2, 0x046 },  // WU multi-color — EXCLUDED (U in excludedMask)
            { 3, 0x084 },  // Mono-blue — EXCLUDED
        };

        var matching = EvaluateSqlAgainstSQLite(sql, testCards);

        Assert.That(matching, Does.Contain(1), "Mono-white must pass W-only filter");
        Assert.That(matching, Does.Not.Contain(2), "WU card must NOT pass (U excluded)");
        Assert.That(matching, Does.Not.Contain(3), "Mono-blue must NOT pass");
    }

    [Test]
    public void SqlIntegration_IdentityMode_UsesIdentityBits()
    {
        var boxes = EmptyBoxes();
        Set(boxes, ColorBitFlags.W);
        var filter = ColorFilterHelper.GetFilter(boxes, identityMode: true)!;
        var sql = filter.ToSqlWhere()!;

        Assert.That(sql, Does.Contain("IdentityBits"), "Identity mode must use IdentityBits column");
    }
}
