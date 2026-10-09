using Dapper;
using Microsoft.Data.Sqlite;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.Enums;
using MagicAssistant.Data.Model.Legality;
using MagicAssistant.Data.Model.NavigatorNodes;

namespace MagicAssistant.Data.Db;

/// <summary>
/// SQLite-backed store for card collections and decks.
/// Works on the same database file as SqliteCardStore (cards.db).
/// Tables Collection and Deck are created by InitializeDb.EnsureCreatedAsync().
///
/// All card lookups use CardId FK → Cards table, so queries can JOIN for full card info.
/// Uses per-request connections (same pattern as SqliteCardStore) — no single shared connection,
/// which is safer for Blazor Server with multiple concurrent circuits.
/// </summary>
public class SqliteCollectionStore : ICollectionStore, ICardStore<MagicCardPhysical>, IAsyncDisposable
{
    private readonly string _dbPath;

    private static readonly string ConnectionStringTemplate = "Data Source={0};Default Timeout=30";

    public SqliteCollectionStore(string dbPath)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
    }

    /// <summary>
    /// Creates a new per-request connection. Pooling is enabled via the connection string,
    /// so connections are reused by ADO.NET without a single shared instance.
    /// </summary>
    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
    }

    /// <summary>
    /// Shared column list for all Deck LEFT JOIN Cards queries.
    /// Keep in sync with FullDeckCardRow — Dapper maps by exact column name.
    /// </summary>
    private const string DeckCardColumns = @"d.CollectionId, n.NodeType, n.Name AS CollectionName,
                   n.ParentId AS CollectionParentId, n.Locked AS CollectionLocked,
                   d.CardId, d.Count, d.IsOwn, d.Comment,
                   d.DateAdded, d.ForTradeCount, d.Grade,
                   c.Name, c.ManaCost, c.Layout, c.Type, c.Power, c.Toughness,
                   c.SetCode, c.Rarity, c.OracleText, c.Rulings, c.Artist, c.Language,
                   c.CollectorNumber, c.Text, c.CmcNumber, c.CmcText, c.ColorType, c.Side, c.CollectorNumberId,
                   c.PrintedName, c.PrintedText, c.PrintedTypeLine,
                   d.MyPrice, c.CommunityRating, c.LegalityFormats";

    // ═══════════════════════════════════════════════════════════
    //  ICollectionStore — deck / folder CRUD
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// No-op — schema is created by InitializeDb.EnsureCreatedAsync().
    /// Kept for ICollectionStore contract compliance.
    /// </summary>
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Fetch the entire branch (subtree) rooted at the given node.
    /// Uses a recursive CTE to traverse all descendants.
    /// Returns the root node plus every child, grandchild, etc.
    /// </summary>
    public async Task<ICollection<Collection>> GetBranchAsync(int collectionId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // Recursive CTE: start from the root node, then walk down via ParentId FK.
        var sql = @"
            WITH RECURSIVE Branch(Id, Name, ParentId, NodeType, Locked) AS (
                SELECT Id, Name, ParentId, NodeType, Locked
                FROM Collection WHERE Id = @RootId
                UNION ALL
                SELECT c.Id, c.Name, c.ParentId, c.NodeType, c.Locked
                FROM Collection c
                INNER JOIN Branch b ON c.ParentId = b.Id
            )
            SELECT Id, Name, ParentId, NodeType, Locked FROM Branch";

        var rows = (await conn.QueryAsync<CollectionRow>(sql, new { RootId = collectionId })).ToList();

        return await AttachCardCountsAsync(conn, rows);
    }

    /// <summary>
    /// Fetch ALL nodes (including sideboard) in one query.
    /// SQL order: Collection (1) → Deck (2) → Folder (0) → Sideboard (3).
    /// Caller builds the tree on .NET with per-level sort.
    /// </summary>
    public async Task<ICollection<Collection>> GetAllCollectionsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // All nodes including sideboard. Sort: Collection(1), Deck(2), Folder(0), Sideboard(3).
        var rows = (await conn.QueryAsync<CollectionRow>(
            "SELECT Id, Name, ParentId, NodeType, Locked FROM Collection")).ToList();

        return await AttachCardCountsAsync(conn, rows);
    }

    public async Task<Collection?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<CollectionRow>(
            "SELECT Id, Name, ParentId, NodeType, Locked FROM Collection WHERE Id = @Id",
            new { Id = id });

        if (row == null) return null;
        var list = await AttachCardCountsAsync(conn, new List<CollectionRow> { row });
        return list.FirstOrDefault();
    }

    public async Task<Collection?> GetByParentIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<CollectionRow>(
            "SELECT Id, Name, ParentId, NodeType, Locked FROM Collection WHERE ParentId = @Id",
            new { Id = id });

        if (row == null) return null;
        var list = await AttachCardCountsAsync(conn, new List<CollectionRow> { row });
        return list.FirstOrDefault();
    }

    public async Task<Collection> CreateDeckAsync(string name, int? parentId = null, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // Insert main deck (NodeType = 2)
            await conn.ExecuteAsync(
                $"INSERT INTO Collection (Name, ParentId, NodeType) VALUES (@Name, @ParentId, {(int)NodeType.Deck})",
                new { Name = name, ParentId = parentId }, tx);

            var deckId = Convert.ToInt32(await conn.ExecuteScalarAsync("SELECT last_insert_rowid()"));

            // Insert sideboard child (NodeType = 3, ParentId = deckId)
            await conn.ExecuteAsync(
                $"INSERT INTO Collection (Name, ParentId, NodeType) VALUES (@Name, @ParentId, {(int)NodeType.Sideboard})",
                new { Name = $"{name} (sb)", ParentId = deckId }, tx);

            tx.Commit();
            return new Collection(deckId, name, parentId, NodeType.Deck, 0);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<Collection> CreateDeckWithCardsAsync(string name, IEnumerable<MagicCardPhysical> cards, int? parentId = null,
        IEnumerable<MagicCardPhysical>? sideboardCards = null, CancellationToken ct = default)
    {
        var list = cards.ToList();
        var sbList = (sideboardCards ?? Enumerable.Empty<MagicCardPhysical>()).ToList();

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // Insert main deck (NodeType = 2)
            await conn.ExecuteAsync(
                $"INSERT INTO Collection (Name, ParentId, NodeType) VALUES (@Name, @ParentId, {(int)NodeType.Deck})",
                new { Name = name, ParentId = parentId }, tx);

            var deckId = Convert.ToInt32(await conn.ExecuteScalarAsync("SELECT last_insert_rowid()"));

            // Insert sideboard child (NodeType = 3, ParentId = deckId)
            await conn.ExecuteAsync(
                $"INSERT INTO Collection (Name, ParentId, NodeType) VALUES (@Name, @ParentId, {(int)NodeType.Sideboard})",
                new { Name = $"{name} (sb)", ParentId = deckId }, tx);

            var sbId = Convert.ToInt32(await conn.ExecuteScalarAsync("SELECT last_insert_rowid()"));

            // Single INSERT used for both pools — merge semantics on conflict
            async Task InsertCardRowAsync(int collectionId, MagicCardPhysical card)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO Deck (CollectionId, CardId, Count, IsOwn, Comment, DateAdded, ForTradeCount, MyPrice, Grade)
                    VALUES (@CollectionId, @CardId, @Count, @IsOwn, @Comment, @DateAdded, @ForTradeCount, @MyPrice, @Grade)
                    ON CONFLICT(CollectionId, CardId) DO UPDATE SET
                        Count = Count + excluded.Count,
                        IsOwn = excluded.IsOwn,
                        Comment = excluded.Comment,
                        ForTradeCount = excluded.ForTradeCount,
                        -- COALESCE: re-adding a card (fresh instance with NULL grade) must not wipe the stored grade
                        Grade = COALESCE(excluded.Grade, Grade)",
                    new
                    {
                        CollectionId = collectionId,
                        CardId = card.Card.Id,
                        Count = card.Count,
                        IsOwn = card.IsOwn,
                        Comment = card.Comment,
                        DateAdded = card.DateAdded,
                        ForTradeCount = card.GetForTradeCount() > 0 ? card.GetForTradeCount() : (int?)null,
                        MyPrice = card.MyPrice,
                        Grade = card.Grade is { } g ? (int?)g : null
                    }, tx);
            }

            // Stamp the created node into every main card and insert
            var deckNode = new Collection(deckId, name, parentId, NodeType.Deck, 0);
            foreach (var card in list)
            {
                card.SetCollection(deckNode);
                await InsertCardRowAsync(deckId, card);
            }

            // Sideboard pool — stamped with the sideboard node itself
            if (sbList.Count > 0)
            {
                var sbNode = new Collection(sbId, $"{name} (sb)", deckId, NodeType.Sideboard, 0);
                foreach (var card in sbList)
                {
                    card.SetCollection(sbNode);
                    await InsertCardRowAsync(sbId, card);
                }
            }

            tx.Commit();
            return new Collection(deckId, name, parentId, NodeType.Deck, 0);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // Locked nodes cannot be deleted (any NodeType: 0-3)
            var locked = await conn.QuerySingleOrDefaultAsync<bool>(
                "SELECT Locked FROM Collection WHERE Id = @Id",
                new { Id = id }, tx);
            if (locked)
            {
                return false;
            }

            // Find sideboard child if it exists
            var sbRows = (await conn.QueryAsync<int>(
                "SELECT Id FROM Collection WHERE ParentId = @Id AND NodeType = 3",
                new { Id = id }, tx)).ToList();

            foreach (var sbId in sbRows)
            {
                await conn.ExecuteAsync("DELETE FROM Collection WHERE Id = @Id", new { Id = sbId }, tx);
            }

            var deleted = await conn.ExecuteAsync(
                "DELETE FROM Collection WHERE Id = @Id", new { Id = id }, tx);

            tx.Commit();
            return deleted > 0;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> RenameAsync(int id, string newName, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // Locked nodes cannot be renamed (any NodeType: 0-3)
            var locked = await conn.QuerySingleOrDefaultAsync<bool>(
                "SELECT Locked FROM Collection WHERE Id = @Id",
                new { Id = id }, tx);
            if (locked)
            {
                return false;
            }

            var affected = await conn.ExecuteAsync(
                "UPDATE Collection SET Name = @Name WHERE Id = @Id",
                new { Name = newName, Id = id }, tx);

            // Also rename the sideboard child if it exists
            var sbRows = (await conn.QueryAsync<int>(
                "SELECT Id FROM Collection WHERE ParentId = @Id AND NodeType = 3",
                new { Id = id }, tx)).ToList();

            foreach (var sbId in sbRows)
            {
                await conn.ExecuteAsync(
                    "UPDATE Collection SET Name = @Name WHERE Id = @Id",
                    new { Name = $"{newName} (sb)", Id = sbId }, tx);
            }

            tx.Commit();
            return affected > 0;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> LockAsync(int id, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var affected = await conn.ExecuteAsync(
            "UPDATE Collection SET Locked = 1 WHERE Id = @Id",
            new { Id = id });
        return affected > 0;
    }

    public async Task<bool> UnlockAsync(int id, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var affected = await conn.ExecuteAsync(
            "UPDATE Collection SET Locked = 0 WHERE Id = @Id",
            new { Id = id });
        return affected > 0;
    }

    public async Task<IEnumerable<Collection>> GetChildrenAsync(int? parentId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // Exclude sideboard children from navigation tree
        var rows = (await conn.QueryAsync<CollectionRow>(
            "SELECT Id, Name, ParentId, NodeType, Locked FROM Collection WHERE ParentId = @ParentId AND NodeType != 3",
            new { ParentId = parentId })).ToList();

        return await AttachCardCountsAsync(conn, rows);
    }

    // ═══════════════════════════════════════════════════════════
    //  ICardStore<MagicCardPhysical> — card CRUD within decks
    // ═══════════════════════════════════════════════════════════

    public async Task<bool> AddAsync(MagicCardPhysical card, CancellationToken ct = default)
    {
        var collectionId = card.Collection?.Id ?? 0;
        if (collectionId <= 0)
            throw new ArgumentException("Card must have a valid location", nameof(card));

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // Locked nodes (Collection/Deck/Sideboard: NodeType 1-3) block card removal; the node must exist.
        var locked = await conn.QuerySingleOrDefaultAsync<bool?>(
            "SELECT Locked FROM Collection WHERE Id = @Id AND NodeType IN (1,2,3)",
            new { Id = collectionId });
        if (locked == null)
            throw new InvalidOperationException($"Deck '{collectionId}' not found");
        if (locked == true)
            throw new InvalidOperationException($"Cannot remove cards from a locked node (Id={collectionId})");

        await conn.ExecuteAsync(@"
            INSERT INTO Deck (CollectionId, CardId, Count, IsOwn, Comment, DateAdded, ForTradeCount, MyPrice, Grade)
            VALUES (@CollectionId, @CardId, @Count, @IsOwn, @Comment, @DateAdded, @ForTradeCount, @MyPrice, @Grade)
            ON CONFLICT(CollectionId, CardId) DO UPDATE SET
                Count = Count + excluded.Count,
                IsOwn = excluded.IsOwn,
                Comment = excluded.Comment,
                ForTradeCount = excluded.ForTradeCount,
                -- COALESCE: re-adding a card (fresh instance with NULL grade) must not wipe the stored grade
                Grade = COALESCE(excluded.Grade, Grade)",
            new
            {
                CollectionId = collectionId,
                CardId = card.Card.Id,
                Count = card.Count,
                IsOwn = card.IsOwn,
                Comment = card.Comment,
                DateAdded = card.DateAdded,
                ForTradeCount = card.GetForTradeCount() > 0 ? card.GetForTradeCount() : (int?)null,
                MyPrice = card.MyPrice,
                Grade = card.Grade is { } g ? (int?)g : null
            });

        return true;
    }

    public async Task<bool> AddAllAsync(IEnumerable<MagicCardPhysical> cards, CancellationToken ct = default)
    {
        var list = cards.ToList();
        if (!list.Any()) return true;

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // Check all target locations for locked status (NodeType 1-3)
            var locationIds = list.Select(c => c.Collection?.Id ?? 0).Distinct().ToList();
            if (locationIds.Any(id => id > 0))
            {
                var allExistedLocationIds = (await conn.QueryAsync<int>(
                    "SELECT Id FROM Collection WHERE Id IN @Ids;", new { Ids = locationIds }, tx)).ToList();

                if (locationIds.Except(allExistedLocationIds).Any())
                {
                    throw new InvalidOperationException($"Some locations are not exist");
                }

                var lockedIds = (await conn.QueryAsync<int>(
                    "SELECT Id FROM Collection WHERE Id IN @Ids AND NodeType IN (1,2,3) AND Locked = 1",
                    new { Ids = locationIds }, tx)).ToList();

                if (lockedIds.Any())
                    throw new InvalidOperationException($"Cannot add cards to locked nodes: [{string.Join(", ", lockedIds)}]");
            }

            foreach (var card in list)
            {
                var locationId = card.Collection?.Id ?? 0;
                if (locationId <= 0)
                    throw new ArgumentException($"Card '{card.Name}' has no valid collection", nameof(card));

                await conn.ExecuteAsync(@"
                    INSERT INTO Deck (CollectionId, CardId, Count, IsOwn, Comment, DateAdded, ForTradeCount, MyPrice, Grade)
                    VALUES (@CollectionId, @CardId, @Count, @IsOwn, @Comment, @DateAdded, @ForTradeCount, @MyPrice, @Grade)
                    ON CONFLICT(CollectionId, CardId) DO UPDATE SET
                        Count = Count + excluded.Count,
                        IsOwn = excluded.IsOwn,
                        Comment = excluded.Comment,
                        ForTradeCount = excluded.ForTradeCount,
                        -- COALESCE: re-adding a card (fresh instance with NULL grade) must not wipe the stored grade
                        Grade = COALESCE(excluded.Grade, Grade)",
                    new
                    {
                        CollectionId = locationId,
                        CardId = card.Card.Id,
                        Count = card.Count,
                        IsOwn = card.IsOwn,
                        Comment = card.Comment,
                        DateAdded = card.DateAdded,
                        ForTradeCount = card.GetForTradeCount() > 0 ? card.GetForTradeCount() : (int?)null,
                        MyPrice = card.MyPrice,
                        Grade = card.Grade is { } g ? (int?)g : null
                    }, tx);
            }

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> RemoveAsync(MagicCardPhysical card, CancellationToken ct = default)
    {
        var collectionId = card.Collection?.Id ?? 0;
        if (collectionId <= 0) return false;

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // Locked nodes (Collection/Deck/Sideboard: NodeType 1-3) block card removal; the node must exist.
        var locked = await conn.QuerySingleOrDefaultAsync<bool?>(
            "SELECT Locked FROM Collection WHERE Id = @Id AND NodeType IN (1,2,3)",
            new { Id = collectionId });
        if (locked == null)
            throw new InvalidOperationException($"Deck '{collectionId}' not found");
        if (locked == true)
            throw new InvalidOperationException($"Cannot remove cards from a locked node (Id={collectionId})");

        // If requested count >= stored count, remove the row entirely.
        // Otherwise decrease the count (never below 0).
        var removed = await conn.ExecuteAsync(@"
            DELETE FROM Deck
            WHERE CollectionId = @CollectionId AND CardId = @CardId AND Count <= @Count",
            new { CollectionId = collectionId, CardId = card.Card.Id, Count = card.Count });

        if (removed == 0)
        {
            await conn.ExecuteAsync(@"
                UPDATE Deck SET Count = MAX(0, Count - @Count)
                WHERE CollectionId = @CollectionId AND CardId = @CardId",
                new { CollectionId = collectionId, CardId = card.Card.Id, Count = card.Count });
        }

        return true;
    }

    public async Task<bool> RemoveAllAsync(IEnumerable<MagicCardPhysical> cards, CancellationToken ct = default)
    {
        foreach (var card in cards)
            await RemoveAsync(card, ct);
        return true;
    }

    public async Task<int> RemoveCardsAsync(int collectionId, ICollection<Guid> cardIds, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // Locked nodes (Collection/Deck/Sideboard: NodeType 1-3) block card removal; the node must exist.
        var locked = await conn.QuerySingleOrDefaultAsync<bool?>(
            "SELECT Locked FROM Collection WHERE Id = @Id AND NodeType IN (1,2,3)",
            new { Id = collectionId });
        if (locked == null)
            throw new InvalidOperationException($"Deck '{collectionId}' not found");
        if (locked == true)
            throw new InvalidOperationException($"Cannot remove cards from a locked node (Id={collectionId})");

        // Single SQL round-trip: DELETE ... RETURNING Count — each deleted row yields its copy
        // count; the in-memory sum is the total number of card copies removed, not the row count.
        var ids = cardIds?.ToList() ?? new List<Guid>();
        const string clearAllSql = "DELETE FROM Deck WHERE CollectionId = @CollectionId RETURNING Count";
        const string clearListedSql = "DELETE FROM Deck WHERE CollectionId = @CollectionId AND CardId IN @CardIds RETURNING Count";

        var counts = (await conn.QueryAsync<int>(
            ids.Any() ? clearListedSql : clearAllSql,
            new { CollectionId = collectionId, CardIds = ids })).ToList();

        return counts.Sum();
    }

    public async Task<bool> UpdateAsync(MagicCardPhysical card, ICollection<MagicCardField> fields, CancellationToken ct = default)
    {
        var locationId = card.Collection?.Id ?? 0;
        if (locationId <= 0) return false;

        using var conn2 = CreateConnection();
        await conn2.OpenAsync(ct);

        // Locked nodes (Collection/Deck/Sideboard: NodeType 1-3) block card updates
        var locked = await conn2.QuerySingleOrDefaultAsync<bool>(
            "SELECT Locked FROM Collection WHERE Id = @Id AND NodeType IN (1,2,3)",
            new { Id = locationId });
        if (locked)
            throw new InvalidOperationException($"Cannot update cards in a locked node (Id={locationId})");

        var setClauses = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("@CollectionId", locationId);
        parameters.Add("@CardId", card.Card.Id);

        foreach (var field in fields)
        {
            var value = field.Get(card);
            switch (field)
            {
                case MagicCardField.Count:
                    setClauses.Add("Count = @Count");
                    parameters.Add("@Count", CastToInt(value));
                    break;
                case MagicCardField.IsOwn:
                    setClauses.Add("IsOwn = @IsOwn");
                    parameters.Add("@IsOwn", value is bool b ? b : false);
                    break;
                case MagicCardField.Comment:
                    setClauses.Add("Comment = @Comment");
                    parameters.Add("@Comment", value?.ToString());
                    break;
                case MagicCardField.MyPrice:
                    setClauses.Add("MyPrice = @MyPrice");
                    parameters.Add("@MyPrice", CastToFloat(value));
                    break;
                case MagicCardField.ForTradeCount:
                    setClauses.Add("ForTradeCount = @ForTradeCount");
                    var ftValue = CastToInt(value);
                    parameters.Add("@ForTradeCount", ftValue > 0 ? (int?)ftValue : null);
                    break;
                case MagicCardField.Grade:
                    setClauses.Add("Grade = @Grade");
                    parameters.Add("@Grade", value is CardGrade g ? (int?)g : null);
                    break;
            }
        }

        if (!setClauses.Any()) return true;

        var sql = $"UPDATE Deck SET {string.Join(", ", setClauses)} WHERE CollectionId = @CollectionId AND CardId = @CardId";

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var result = await conn.ExecuteAsync(sql, parameters);
        return result > 0;
    }

    public async Task<MagicCardPhysical?> GetCardAsync(Guid cardId, CancellationToken ct = default)
    {
        // JOIN Deck + Cards to get full card info
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<FullDeckCardRow>(
            $"SELECT {DeckCardColumns} FROM Deck d LEFT JOIN Cards c ON d.CardId = c.Id LEFT JOIN Collection n ON d.CollectionId = n.Id WHERE d.CardId = @CardId LIMIT 1",
            new { CardId = cardId });

        var physical = row != null ? MapToFullPhysical(row) : null;
        if (physical is not null)
            await AttachFacesAsync(conn, new[] { physical }, ct);
        return physical;
    }

    public async Task<ICollection<MagicCardPhysical>> GetCardsAsync(Guid cardId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<FullDeckCardRow>(
            $"SELECT {DeckCardColumns} FROM Deck d LEFT JOIN Cards c ON d.CardId = c.Id LEFT JOIN Collection n ON d.CollectionId = n.Id WHERE d.CardId = @CardId",
            new { CardId = cardId });

        var ownCountsMap = await LoadAllOwnCountsAsync(conn, ct);
        var physicals = rows.Select(r => MapToFullPhysical(r, ownCountsMap.GetValueOrDefault(r.CardId))!).ToList();
        await AttachFacesAsync(conn, physicals, ct);
        return physicals;
    }

    public async Task<int> TotalUniqueCardsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        return await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM Deck");
    }

    public async Task<IEnumerable<MagicCardPhysical>> GetAllDeckCardsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        // Reconciliation/matching path (CardResolveService) — faces are not needed here,
        // so no AttachFacesAsync: one less batched query over every deck card.
        var rows = await conn.QueryAsync<FullDeckCardRow>(
            $"SELECT {DeckCardColumns} FROM Deck d LEFT JOIN Cards c ON d.CardId = c.Id LEFT JOIN Collection n ON d.CollectionId = n.Id");

        var ownCountsMap = await LoadAllOwnCountsAsync(conn, ct);
        return rows.Select(r => MapToFullPhysical(r, ownCountsMap.GetValueOrDefault(r.CardId))!).ToList();
    }

    /// <summary>
    /// Get all physical cards for a specific deck (non-sideboard only) with full card info from Cards table.
    /// </summary>
    public async Task<ICollection<MagicCardPhysical>> GetDeckCardsAsync(int deckId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<FullDeckCardRow>(
            $"SELECT {DeckCardColumns} FROM Deck d LEFT JOIN Cards c ON d.CardId = c.Id LEFT JOIN Collection n ON d.CollectionId = n.Id WHERE d.CollectionId = @DeckId",
            new { DeckId = deckId });

        var ownCountsMap = await LoadAllOwnCountsAsync(conn, ct);
        var physicals = rows.Select(r => MapToFullPhysical(r, ownCountsMap.GetValueOrDefault(r.CardId))!).ToList();
        await AttachFacesAsync(conn, physicals, ct);
        return physicals;
    }

    /// <inheritdoc />
    public async Task<Dictionary<(int CollectionId, Guid CardId), int>> GetCardCountsAsync(IEnumerable<int> collectionIds, IEnumerable<Guid> cardIds, CancellationToken ct = default)
    {
        var colls = collectionIds.Distinct().ToList();
        var ids = cardIds.Distinct().ToList();
        if (colls.Count == 0 || ids.Count == 0)
            return new Dictionary<(int CollectionId, Guid CardId), int>();

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // Parameterized IN clauses for SQLite compatibility
        var collPlaceholders = string.Join(",", colls.Select((_, i) => $"@c{i}"));
        var cardPlaceholders = string.Join(",", ids.Select((_, i) => $"@id{i}"));
        var parameters = new DynamicParameters();
        for (int i = 0; i < colls.Count; i++)
            parameters.Add($"@c{i}", colls[i]);
        for (int i = 0; i < ids.Count; i++)
            parameters.Add($"@id{i}", ids[i]);

        var rows = await conn.QueryAsync<(int CollectionId, Guid CardId, int Count)>(
            $"SELECT CollectionId, CardId, SUM(Count) AS Count FROM Deck " +
            $"WHERE CollectionId IN ({collPlaceholders}) AND CardId IN ({cardPlaceholders}) GROUP BY CollectionId, CardId",
            parameters);

        return rows.ToDictionary(r => (r.CollectionId, r.CardId), r => r.Count);
    }

    /// <inheritdoc />
    public async Task<int?> GetSideboardIdAsync(int deckId, CancellationToken ct = default)
    {
        if (deckId <= 0) return null;

        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        return await conn.ExecuteScalarAsync<int?>(
            "SELECT Id FROM Collection WHERE ParentId = @DeckId AND NodeType = 3",
            new { DeckId = deckId });
    }

    /// <summary>
    /// Get all physical cards for a specific deck including sideboard with full card info.
    /// </summary>
    public async Task<ICollection<MagicCardPhysical>> GetDeckWithSideboardAsync(int deckId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // Find the sideboard child of this deck
        var sbRows = (await conn.QueryAsync<int>(
            "SELECT Id FROM Collection WHERE ParentId = @DeckId AND NodeType = 3",
            new { DeckId = deckId })).ToList();

        var allCollectionIds = new List<int> { deckId };
        allCollectionIds.AddRange(sbRows);

        // Build parameterized IN clause for SQLite compatibility
        var placeholders = string.Join(",", allCollectionIds.Select((_, i) => $"@id{i}"));
        var parameters = new DynamicParameters();
        for (int i = 0; i < allCollectionIds.Count; i++)
            parameters.Add($"@id{i}", allCollectionIds[i]);

        var rows = await conn.QueryAsync<FullDeckCardRow>(
            $"SELECT {DeckCardColumns} FROM Deck d LEFT JOIN Cards c ON d.CardId = c.Id LEFT JOIN Collection n ON d.CollectionId = n.Id WHERE d.CollectionId IN (" + placeholders + ")",
            parameters);

        var ownCountsMap = await LoadAllOwnCountsAsync(conn, ct);
        var physicals = rows.Select(r => MapToFullPhysical(r, ownCountsMap.GetValueOrDefault(r.CardId))!).ToList();
        await AttachFacesAsync(conn, physicals, ct);
        return physicals;
    }

    /// <summary>
    /// Get ALL physical cards across ALL decks (for reconciliation).
    /// Delegates to ICardStore&lt;MagicCardPhysical&gt;.GetAllDeckCardsAsync.
    /// </summary>
    public async Task<ICollection<MagicCardPhysical>> GetAllPhysicalCardsAsync(CancellationToken ct = default)
    {
        return (await GetAllDeckCardsAsync(ct)).ToList();
    }

    /// <summary>
    /// Create a collection node (NodeType = 1) WITHOUT sideboard.
    /// Unlike CreateDeckAsync, this creates only one row in Collection table with NodeType = Collection.
    /// </summary>
    public async Task<Collection> CreateCollectionAsync(string name, int? parentId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO Collection (Name, ParentId, NodeType) VALUES (@Name, @ParentId, 1)",
            new { Name = name, ParentId = parentId });

        var id = Convert.ToInt32(await conn.ExecuteScalarAsync("SELECT last_insert_rowid()"));
        return new Collection(id, name, parentId, NodeType.Collection, 0);
    }

    /// <summary>
    /// Create a folder node (NodeType = 0) WITHOUT sideboard.
    /// Folders are containers for organizing other nodes. Cannot contain cards.
    /// </summary>
    public async Task<Collection> CreateFolderAsync(string name, int? parentId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO Collection (Name, ParentId, NodeType) VALUES (@Name, @ParentId, 0)",
            new { Name = name, ParentId = parentId });

        var id = Convert.ToInt32(await conn.ExecuteScalarAsync("SELECT last_insert_rowid()"));
        return new Collection(id, name, parentId, NodeType.Folder, 0);
    }

    /// <summary>
    /// Compute ownership statistics for all unique card IDs in one SQL query.
    /// Returns CardId → { OwnCount, TotalCount, OwnUnique } for efficient reconciliation.
    /// </summary>
    public async Task<Dictionary<Guid, OwnCountStats>> GetOwnCountStatsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<OwnCountRow>(@"
            SELECT d.CardId,
                   SUM(CASE WHEN d.IsOwn = 1 THEN d.Count ELSE 0 END) AS OwnCount,
                   SUM(d.Count) AS TotalCount,
                   COUNT(DISTINCT CASE WHEN d.IsOwn = 1 THEN d.CollectionId END) AS OwnUnique
            FROM Deck d
            GROUP BY d.CardId");

        return rows.ToDictionary(r => r.CardId, r => new OwnCountStats(r.CardId, r.OwnCount, r.TotalCount, r.OwnUnique));
    }

    // ═══════════════════════════════════════════════════════════
    //  Private helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Loads all ownership stats from the OwnCounts table into a dictionary keyed by CardId.
    /// </summary>
    private static async Task<Dictionary<Guid, OwnCountStats>> LoadAllOwnCountsAsync(
        SqliteConnection conn, CancellationToken ct = default)
    {
        var rows = await conn.QueryAsync<OwnCountRow>(
            "SELECT CardId, OwnCount, TotalCount, OwnUnique FROM OwnCounts");

        return rows.ToDictionary(
            r => r.CardId,
            r => new OwnCountStats(r.CardId, r.OwnCount, r.TotalCount, r.OwnUnique));
    }

    /// <summary>
    /// Attaches ownership statistics to a card's properties so that
    /// GetOwnCount(), GetOwnTotal(), GetOwnTotalAll(), GetOwnUnique() return real values.
    /// </summary>
    private static void AttachOwnCountStats(MagicCard card, OwnCountStats stats)
    {
        if (stats.OwnCount > 0)
            card.OwnCount = stats.OwnCount;
        if (stats.TotalCount > 0)
            card.OwnTotal = stats.TotalCount;
        if (stats.TotalCount > 0)
            card.OwnTotalAll = stats.TotalCount;
        if (stats.OwnUnique > 0)
            card.OwnUnique = stats.OwnUnique;
    }

    /// <summary>
    /// Batch-loads CardFaces for the given physicals in one query and attaches them to each card.
    /// Double-sided cards carry per-face names/text — needed by the edit dialog preview (ActiveName).
    /// </summary>
    private static async Task AttachFacesAsync(
        SqliteConnection conn, ICollection<MagicCardPhysical> physicals, CancellationToken ct = default)
    {
        var ids = physicals.Select(p => p.Card.Id).Distinct().ToList();
        if (ids.Count == 0) return;

        var parameters = new DynamicParameters();
        for (var i = 0; i < ids.Count; i++)
            parameters.Add($"@id{i}", ids[i]);

        var placeholders = string.Join(", ", ids.Select((_, i) => $"@id{i}"));
        var rows = await conn.QueryAsync<CardFaceRow>(
            $@"SELECT Id, CardId, FaceIndex, Name, ManaCost, TypeLine, OracleText, Power, PowerNumber,
                      Toughness, ToughnessNumber, CmcNumber, CmcText, Colors, ColorIndicator, Loyalty,
                      Artist, IllustrationId, FlavorText, PrintedName, PrintedText, PrintedTypeLine,
                      ColorBits, IdentityBits
               FROM CardFaces
               WHERE CardId IN ({placeholders})
               ORDER BY CardId, FaceIndex",
            parameters);

        var facesMap = rows.GroupBy(r => r.CardId)
            .ToDictionary(g => g.Key, g => g.Select(MapToCardFace).ToArray());

        foreach (var physical in physicals)
        {
            if (facesMap.TryGetValue(physical.Card.Id, out var faces))
                physical.Card.CardFaces = faces;
        }
    }

    private static CardFace MapToCardFace(CardFaceRow row)
    {
        var face = new CardFace
        {
            Name = row.Name ?? string.Empty,
            ManaCost = row.ManaCost,
            TypeLine = row.TypeLine,
            OracleText = row.OracleText,
            Power = row.Power,
            PowerNumber = row.PowerNumber,
            Toughness = row.Toughness,
            ToughnessNumber = row.ToughnessNumber,
            CmcNumber = row.CmcNumber,
            CmcText = row.CmcText,
            Loyalty = row.Loyalty,
            Artist = row.Artist,
            IllustrationId = row.IllustrationId,
            FlavorText = row.FlavorText,
            PrintedName = row.PrintedName,
            PrintedText = row.PrintedText,
            PrintedTypeLine = row.PrintedTypeLine,
            ColorBits = row.ColorBits,
            IdentityBits = row.IdentityBits,
        };

        if (!string.IsNullOrEmpty(row.Colors))
            face.Colors = row.Colors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!string.IsNullOrEmpty(row.ColorIndicator))
            face.ColorIndicator = row.ColorIndicator.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        return face;
    }

    private async Task<List<Collection>> AttachCardCountsAsync(SqliteConnection conn, List<CollectionRow> rows)
    {
        if (!rows.Any()) return new List<Collection>();

        var ids = rows.Select(r => r.Id).ToList();

        // Also include sideboard children for decks
        var sbRows = (await conn.QueryAsync<(int Id, int ParentId)>(
            "SELECT Id, ParentId FROM Collection WHERE ParentId IN @Ids AND NodeType = 3",
            new { Ids = ids })).ToList();

        var allIds = ids.Concat(sbRows.Select(s => s.Id)).ToList();

        var counts = (await conn.QueryAsync<(int CollectionId, int Count)>(
            "SELECT CollectionId, SUM(Count) AS Count FROM Deck WHERE CollectionId IN @Ids GROUP BY CollectionId",
            new { Ids = allIds })).ToDictionary(r => r.CollectionId, r => r.Count);

        var nodes = new List<Collection>();
        foreach (var row in rows)
        {
            counts.TryGetValue(row.Id, out var cardCount);
            nodes.Add(new Collection(
                row.Id, row.Name, row.ParentId, (NodeType)row.NodeType, cardCount, row.Locked));
        }

        return nodes;
    }

    /// <summary>
    /// Maps a full JOIN row (Deck + Cards) to MagicCardPhysical with complete base card info.
    /// </summary>
    private MagicCardPhysical? MapToFullPhysical(FullDeckCardRow row, OwnCountStats? ownCounts = null)
    {
        var baseCard = new MagicCard
        {
            Id = row.CardId,
            Name = row.Name ?? string.Empty,
            ManaCost = row.ManaCost,
            Layout = (LayoutEnum)row.Layout,
            Type = row.Type,
            Power = row.Power,
            Toughness = row.Toughness,
            SetCode = row.SetCode,
            Rarity = row.Rarity,
            OracleText = row.OracleText,
            Rulings = row.Rulings,
            Artist = row.Artist,
            Language = row.Language,
            CollectorNumber = row.CollectorNumber,
            Text = row.Text,
            PrintedName = row.PrintedName,
            PrintedText = row.PrintedText,
            PrintedTypeLine = row.PrintedTypeLine,
            CmcNumber = row.CmcNumber,
            CmcText = row.CmcText,
            Side = row.Side,
            CollectorNumberId = row.CollectorNumberId,
            CommunityRating = row.CommunityRating,
        };

        // Restore LegalityMap
        if (!string.IsNullOrEmpty(row.LegalityFormats))
        {
            var formats = row.LegalityFormats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var fmt in formats)
            {
                if (Enum.TryParse<Format>(fmt, true, out var format))
                    baseCard.LegalityMap.SetLegal(format);
            }
        }

        // Attach ownership stats from OwnCounts table (if available)
        if (ownCounts is { } stats)
        {
            AttachOwnCountStats(baseCard, stats);
        }

        // Stamp the owning collection node fully from the JOINed row (all-null when the node is gone).
        // CardCount is a computed POCO field, not a table column — left at 0 here.
        var collection = new Collection(
            row.CollectionId,
            row.CollectionName ?? string.Empty,
            row.CollectionParentId,
            (NodeType)row.NodeType,
            0,
            row.CollectionLocked);
        var physical = new MagicCardPhysical(baseCard, collection, row.IsOwn)
        {
            Count = row.Count,
            MyPrice = row.MyPrice,
        };

        if (!string.IsNullOrEmpty(row.Comment))
            physical.Comment = row.Comment;
        if (!string.IsNullOrEmpty(row.DateAdded) && DateTimeOffset.TryParse(row.DateAdded, out var dateAdded))
            physical.DateAdded = dateAdded;
        if (row.ForTradeCount.HasValue && row.ForTradeCount.Value > 0)
            physical.SetForTradeCount(row.ForTradeCount.Value);
        if (row.Grade.HasValue)
            physical.Grade = (CardGrade)row.Grade.Value;

        return physical;
    }

    private static int CastToInt(object? value)
    {
        if (value == null) return 0;
        if (value is int i) return i;
        if (value is string s) return int.Parse(s);
        throw new InvalidCastException(value.GetType().ToString());
    }

    private static float CastToFloat(object? value)
    {
        if (value == null) return 0f;
        if (value is float f) return f;
        if (value is string s) return float.Parse(s);
        throw new InvalidCastException(value.GetType().ToString());
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        // No persistent connection to dispose — all connections are per-request (using/Dispose).
        // The class itself is registered as Singleton, but each method creates and disposes
        // its own SqliteConnection via using. ADO.NET pooling handles connection reuse.
    }

    // ═══════════════════════════════════════════════════════════
    //  Dapper row mappings
    // ═══════════════════════════════════════════════════════════

    private class CollectionRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? ParentId { get; set; }
        public int NodeType { get; set; }
        public bool Locked { get; set; }
    }

    /// <summary>
    /// Full row from Deck LEFT JOIN Cards — contains both deck-specific and card info.
    /// </summary>
    private class FullDeckCardRow
    {
        // Deck columns
        public int CollectionId { get; set; }
        public int NodeType { get; set; }  // from LEFT JOIN Collection n (0 if the node row is gone)
        public string? CollectionName { get; set; }
        public int? CollectionParentId { get; set; }
        public bool CollectionLocked { get; set; }
        public int Count { get; set; }
        public bool IsOwn { get; set; }
        public string? Comment { get; set; }
        public string? DateAdded { get; set; }  // SQLite stores as ISO 8601 string
        public int? ForTradeCount { get; set; }
        public int? Grade { get; set; }

        // Cards columns (aliased to avoid collision)
        public Guid CardId { get; set; }
        public string? Name { get; set; }
        public string? ManaCost { get; set; }
        public int Layout { get; set; }
        public string? Type { get; set; }
        public string? Power { get; set; }
        public string? Toughness { get; set; }
        public string? SetCode { get; set; }  // matches the c.SetCode column name (Dapper maps by exact name)
        public string? Rarity { get; set; }
        public string? OracleText { get; set; }
        public string? Rulings { get; set; }
        public string? Artist { get; set; }
        public string? Language { get; set; }
        public string? CollectorNumber { get; set; }
        public string? Text { get; set; }
        public string? PrintedName { get; set; }
        public string? PrintedText { get; set; }
        public string? PrintedTypeLine { get; set; }
        public int? CmcNumber { get; set; }
        public string? CmcText { get; set; }
        public string? ColorType { get; set; }
        public int Side { get; set; }
        public int CollectorNumberId { get; set; }
        public float MyPrice { get; set; }
        public float CommunityRating { get; set; }
        public string? LegalityFormats { get; set; }
    }

    private class OwnCountRow
    {
        public Guid CardId { get; set; }
        public int OwnCount { get; set; }
        public int TotalCount { get; set; }
        public int OwnUnique { get; set; }
    }

    /// <summary>Row from the CardFaces table (one face of a multi-faced card).</summary>
    private class CardFaceRow
    {
        public int Id { get; set; }
        public Guid CardId { get; set; }
        public int FaceIndex { get; set; }
        public string? Name { get; set; }
        public string? ManaCost { get; set; }
        public string? TypeLine { get; set; }
        public string? OracleText { get; set; }
        public string? Power { get; set; }
        public int? PowerNumber { get; set; }
        public string? Toughness { get; set; }
        public int? ToughnessNumber { get; set; }
        public int? CmcNumber { get; set; }
        public string? CmcText { get; set; }
        public string? Colors { get; set; }
        public string? ColorIndicator { get; set; }
        public string? Loyalty { get; set; }
        public string? Artist { get; set; }
        public Guid? IllustrationId { get; set; }
        public string? FlavorText { get; set; }
        public string? PrintedName { get; set; }
        public string? PrintedText { get; set; }
        public string? PrintedTypeLine { get; set; }
        public int ColorBits { get; set; }
        public int IdentityBits { get; set; }
    }
}
