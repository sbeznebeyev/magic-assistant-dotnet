using System.Globalization;
using System.Collections.Concurrent;
using System.Linq;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.Enums;
using MagicAssistant.Data.Model.Legality;
using Dapper;
using Microsoft.Data.Sqlite;

namespace MagicAssistant.Data.Db;

public class SqliteCardStore : IDbCardStore, IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly ConcurrentDictionary<Guid, MagicCard> _cache = new();

    private static readonly string ConnectionStringTemplate = "Data Source={0};Default Timeout=30";

    public SqliteCardStore(string dbPath)
    {
        _dbPath = dbPath;
    }

    /// <summary>
    /// No-op — schema is created by InitializeDb.EnsureCreatedAsync().
    /// Kept for ICardStore contract compliance.
    /// </summary>
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task<bool> AddAsync(MagicCard card, CancellationToken ct = default)
    {
        if (card.Id == Guid.Empty || card.Id == default)
            throw new ArgumentException("Card ID can't be a default guid");

        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);

        var sql = @"
            INSERT OR REPLACE INTO Cards (Id, OracleId, Name, ManaCost, Layout, Type, Power, PowerNumber, Toughness, ToughnessNumber, SetCode, Rarity, OracleText, Rulings, Artist, Language, CollectorNumber, Text, PrintedName, PrintedText, PrintedTypeLine, CmcNumber, CmcText, ColorType, Side, CollectorNumberId, DbPrice, CommunityRating, LegalityFormats, Colors, ColorIdentity, ColorIndicator, GameChanger, Keywords, ProducedMana, Legal, NotLegal, Restricted, Banned, Usd, UsdFoil, ColorBits, IdentityBits)
            VALUES (@Id, @OracleId, @Name, @ManaCost, @Layout, @Type, @Power, @PowerNumber, @Toughness, @ToughnessNumber, @SetCode, @Rarity, @OracleText, @Rulings, @Artist, @Language, @CollectorNumber, @Text, @PrintedName, @PrintedText, @PrintedTypeLine, @CmcNumber, @CmcText, @ColorType, @Side, @CollectorNumberId, @DbPrice, @CommunityRating, @LegalityFormats, @Colors, @ColorIdentity, @ColorIndicator, @GameChanger, @Keywords, @ProducedMana, @Legal, @NotLegal, @Restricted, @Banned, @Usd, @UsdFoil, @ColorBits, @IdentityBits)";

        var result = await conn.ExecuteAsync(sql, new
        {
            card.Id,
            card.OracleId,
            card.Name,
            card.ManaCost,
            card.Layout,
            card.Type,
            card.Power,
            card.PowerNumber,
            card.Toughness,
            card.ToughnessNumber,
            card.SetCode,
            card.Rarity,
            card.OracleText,
            card.Rulings,
            card.Artist,
            card.Language,
            card.CollectorNumber,
            card.Text,
            card.PrintedName,
            card.PrintedText,
            card.PrintedTypeLine,
            card.CmcNumber,
            card.CmcText,
            card.ColorType,
            card.Side,
            card.CollectorNumberId,
            card.DbPrice,
            card.CommunityRating,
            LegalityFormats = card.LegalityMap.IsEmpty ? null : string.Join(',', card.LegalityMap.LegalFormats.Select(f => f.ToString())),
            Colors = card.Colors is { Length: > 0 } ? string.Join(',', card.Colors) : (string?)null,
            ColorIdentity = card.ColorIdentity is { Length: > 0 } ? string.Join(',', card.ColorIdentity) : (string?)null,
            ColorIndicator = card.ColorIndicator is { Length: > 0 } ? string.Join(',', card.ColorIndicator) : (string?)null,
            GameChanger = card.GameChanger,
            Keywords = card.Keywords is { Length: > 0 } ? string.Join(';', card.Keywords) : (string?)null,
            ProducedMana = card.ProducedMana is { Length: > 0 } ? string.Join(',', card.ProducedMana) : (string?)null,
            Legal = card.Legal,
            NotLegal = card.NotLegal,
            Restricted = card.Restricted,
            Banned = card.Banned,
            Usd = card.Usd,
            UsdFoil = card.UsdFoil,
            ColorBits = card.ColorBits,
            IdentityBits = card.IdentityBits
        });

        // Always save card faces, all_parts, and image uris — even when null/empty we must
        // DELETE old rows so stale data is cleaned up on re-sync.
        await SaveCardFacesAsync(conn, card);
        await SaveAllPartsAsync(conn, card);
        await SaveCardsImageUrisAsync(conn, card);

        _cache[card.Id] = card;
        return result > 0;
    }

    public async Task<bool> AddAllAsync(IEnumerable<MagicCard> cards, CancellationToken ct = default)
    {

        var list = cards.ToList();
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);

        // Use transaction for batch insert
        await conn.ExecuteAsync(@"BEGIN TRANSACTION");

        try
        {
            var sql = @"
                INSERT OR REPLACE INTO Cards (Id, OracleId, Name, ManaCost, Layout, Type, Power, PowerNumber, Toughness, ToughnessNumber, SetCode, Rarity, OracleText, Rulings, Artist, Language, CollectorNumber, Text, PrintedName, PrintedText, PrintedTypeLine, CmcNumber, CmcText, ColorType, Side, CollectorNumberId, DbPrice, CommunityRating, LegalityFormats, Colors, ColorIdentity, ColorIndicator, GameChanger, Keywords, ProducedMana, Legal, NotLegal, Restricted, Banned, Usd, UsdFoil, ColorBits, IdentityBits)
                VALUES (@Id, @OracleId, @Name, @ManaCost, @Layout, @Type, @Power, @PowerNumber, @Toughness, @ToughnessNumber, @SetCode, @Rarity, @OracleText, @Rulings, @Artist, @Language, @CollectorNumber, @Text, @PrintedName, @PrintedText, @PrintedTypeLine, @CmcNumber, @CmcText, @ColorType, @Side, @CollectorNumberId, @DbPrice, @CommunityRating, @LegalityFormats, @Colors, @ColorIdentity, @ColorIndicator, @GameChanger, @Keywords, @ProducedMana, @Legal, @NotLegal, @Restricted, @Banned, @Usd, @UsdFoil, @ColorBits, @IdentityBits)";

            await conn.ExecuteAsync(sql, list.Select(c => new
            {
                c.Id,
                c.OracleId,
                c.Name,
                c.ManaCost,
                c.Layout,
                c.Type,
                c.Power,
                c.PowerNumber,
                c.Toughness,
                c.ToughnessNumber,
                c.SetCode,
                c.Rarity,
                c.OracleText,
                c.Rulings,
                c.Artist,
                c.Language,
                c.CollectorNumber,
                c.Text,
                c.PrintedName,
                c.PrintedText,
                c.PrintedTypeLine,
                c.CmcNumber,
                c.CmcText,
                c.ColorType,
                c.Side,
                c.CollectorNumberId,
                c.DbPrice,
                c.CommunityRating,
                LegalityFormats = c.LegalityMap.IsEmpty ? null : string.Join(',', c.LegalityMap.LegalFormats.Select(f => f.ToString())),
                Colors = c.Colors is { Length: > 0 } ? string.Join(',', c.Colors) : (string?)null,
                ColorIdentity = c.ColorIdentity is { Length: > 0 } ? string.Join(',', c.ColorIdentity) : (string?)null,
                ColorIndicator = c.ColorIndicator is { Length: > 0 } ? string.Join(',', c.ColorIndicator) : (string?)null,
                GameChanger = c.GameChanger,
                Keywords = c.Keywords is { Length: > 0 } ? string.Join(';', c.Keywords) : (string?)null,
                ProducedMana = c.ProducedMana is { Length: > 0 } ? string.Join(',', c.ProducedMana) : (string?)null,
                Legal = c.Legal,
                NotLegal = c.NotLegal,
                Restricted = c.Restricted,
                Banned = c.Banned,
                Usd = c.Usd,
                UsdFoil = c.UsdFoil,
                ColorBits = c.ColorBits,
                IdentityBits = c.IdentityBits
            }));

            // Always save card faces, all_parts, and image uris for every card.
            // Even when the collection is null/empty we must DELETE old rows
            // so that a card which previously had faces/parts but now doesn't
            // (e.g. Scryfall data changed) gets cleaned up correctly.
            foreach (var card in list)
            {
                await SaveCardFacesAsync(conn, card);
                await SaveAllPartsAsync(conn, card);
                await SaveCardsImageUrisAsync(conn, card);
            }

            await conn.ExecuteAsync(@"COMMIT");
        }
        catch
        {
            await conn.ExecuteAsync(@"ROLLBACK");
            throw;
        }

        foreach (var card in list)
            _cache[card.Id] = card;

        return true;
    }

    public async Task<bool> RemoveAsync(MagicCard card, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        throw new NotSupportedException("Cannot remove cards from knowledge base directly.");
    }

    public async Task<bool> RemoveAllAsync(IEnumerable<MagicCard> cards, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        throw new NotSupportedException("Cannot remove cards from knowledge base directly.");
    }

    public async Task<bool> UpdateAsync(MagicCard card, ICollection<MagicCardField> fields, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);

        var setClauses = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("@Id", card.Id);

        foreach (var field in fields)
        {
            switch (field)
            {
                case MagicCardField.Name:
                    setClauses.Add("Name = @Name");
                    parameters.Add("@Name", card.Name);
                    break;
                case MagicCardField.ManaCost:
                    setClauses.Add("ManaCost = @ManaCost");
                    parameters.Add("@ManaCost", card.ManaCost);
                    break;
                case MagicCardField.Layout:
                    setClauses.Add("Layout = @Layout");
                    parameters.Add("@Layout", (int)card.Layout);
                    break;
                case MagicCardField.Type:
                    setClauses.Add("Type = @Type");
                    parameters.Add("@Type", card.Type);
                    break;
                case MagicCardField.Power:
                    setClauses.Add("Power = @Power");
                    parameters.Add("@Power", card.Power);
                    break;
                case MagicCardField.Toughness:
                    setClauses.Add("Toughness = @Toughness");
                    parameters.Add("@Toughness", card.Toughness);
                    break;
                case MagicCardField.Rarity:
                    setClauses.Add("Rarity = @Rarity");
                    parameters.Add("@Rarity", card.Rarity);
                    break;
                case MagicCardField.OracleText:
                    setClauses.Add("OracleText = @OracleText");
                    parameters.Add("@OracleText", card.OracleText);
                    break;
                case MagicCardField.Rulings:
                    setClauses.Add("Rulings = @Rulings");
                    parameters.Add("@Rulings", card.Rulings);
                    break;
                case MagicCardField.Artist:
                    setClauses.Add("Artist = @Artist");
                    parameters.Add("@Artist", card.Artist);
                    break;
                case MagicCardField.CollectorNumber:
                    setClauses.Add("CollectorNumber = @CollectorNumber");
                    parameters.Add("@CollectorNumber", card.CollectorNumber);
                    break;
                case MagicCardField.Language:
                    setClauses.Add("Language = @Language");
                    parameters.Add("@Language", card.Language);
                    break;
                case MagicCardField.Text:
                    setClauses.Add("Text = @Text");
                    parameters.Add("@Text", card.Text);
                    break;
                case MagicCardField.DbPrice:
                    setClauses.Add("DbPrice = @DbPrice");
                    parameters.Add("@DbPrice", card.DbPrice);
                    break;
                case MagicCardField.CommunityRating:
                    setClauses.Add("CommunityRating = @CommunityRating");
                    parameters.Add("@CommunityRating", card.CommunityRating);
                    break;
                case MagicCardField.Set:
                    setClauses.Add("SetCode = @SetCode");
                    parameters.Add("@SetCode", card.SetCode);
                    break;
            }
        }

        if (!setClauses.Any()) return true;

        var sql = $"UPDATE Cards SET {string.Join(", ", setClauses)} WHERE Id = @Id";
        var result = await conn.ExecuteAsync(sql, parameters);

        if (result > 0)
            _cache[card.Id] = card;

        return result > 0;
    }

    public async Task<MagicCard?> GetCardAsync(Guid cardId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(cardId, out var cached))
            return cached;

        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<MagicCardRow>(
            "SELECT * FROM Cards WHERE Id = @Id", new { Id = cardId });

        if (row == null) return null;

        var ownCounts = await LoadOwnCountForCardAsync(conn, cardId, ct);
        var card = MapToMagicCard(row, ownCounts);

        // Load card faces
        var faces = await LoadCardFacesAsync(conn, cardId, ct);
        if (faces.Length > 0)
            card.CardFaces = faces;

        // Load all_parts (related cards)
        var allParts = await LoadAllPartsAsync(conn, cardId, ct);
        if (allParts.Length > 0)
            card.AllParts = allParts;

        // Load ImageUris
        var imageUris = await LoadCardsImageUrisAsync(conn, cardId, ct);
        if (imageUris != null)
            card.ImageUris = imageUris;

        _cache[cardId] = card;
        return card;
    }

    public async Task<ICollection<MagicCard>> GetCardsAsync(Guid cardId, CancellationToken ct = default)
    {
        var card = await GetCardAsync(cardId, ct);
        return card != null ? new[] { card } : Array.Empty<MagicCard>();
    }

    public async Task<int> TotalUniqueCardsAsync(CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        return await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM Cards");
    }

    public async Task<IEnumerable<MagicCard>> GetAllDeckCardsAsync(CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<MagicCardRow>("SELECT * FROM Cards");
        var ownCountsMap = await LoadAllOwnCountsAsync(conn, ct);
        var cards = rows.Select(r => MapToMagicCard(r, ownCountsMap.GetValueOrDefault(r.Id))).ToList();

        // Load card faces for all cards
        var cardIds = cards.Select(c => c.Id).ToList();
        var facesMap = await LoadCardFacesForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (facesMap.TryGetValue(card.Id, out var faces))
                card.CardFaces = faces;
        }

        // Load all_parts (related cards) for all cards
        var allPartsMap = await LoadAllPartsForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (allPartsMap.TryGetValue(card.Id, out var parts))
                card.AllParts = parts;
        }

        // Load ImageUris for all cards
        var imageUrisMap = await LoadCardsImageUrisForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (imageUrisMap.TryGetValue(card.Id, out var imageUris))
                card.ImageUris = imageUris;
        }

        return cards;
    }

    /// <summary>
    /// Gets up to <paramref name="limit"/> cards from the knowledge base.
    /// Uses SQL LIMIT for efficiency — does not load all cards into memory.
    /// </summary>
    public async Task<ICollection<MagicCard>> GetAllAsync(int limit, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<MagicCardRow>("SELECT * FROM Cards LIMIT @Limit", new { Limit = limit });
        var ownCountsMap = await LoadAllOwnCountsAsync(conn, ct);
        var cards = rows.Select(r => MapToMagicCard(r, ownCountsMap.GetValueOrDefault(r.Id))).ToList();

        // Load card faces for all cards
        var cardIds = cards.Select(c => c.Id).ToList();
        var facesMap = await LoadCardFacesForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (facesMap.TryGetValue(card.Id, out var faces))
                card.CardFaces = faces;
        }

        // Load all_parts (related cards) for all cards
        var allPartsMap = await LoadAllPartsForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (allPartsMap.TryGetValue(card.Id, out var parts))
                card.AllParts = parts;
        }

        // Load ImageUris for all cards
        var imageUrisMap = await LoadCardsImageUrisForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (imageUrisMap.TryGetValue(card.Id, out var imageUris))
                card.ImageUris = imageUris;
        }

        return cards;
    }

    public async Task<ICollection<MagicCard>> SearchAsync(string query, int limit = 100, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);

        // FTS5 full-text search with ranking by relevance.
        // Query is passed directly to FTS5 — supports:
        //   - Simple terms: "lightning bolt" → matches cards containing both words in any column
        //   - Column-specific: "name:lightning" → search only Name column
        //   - Phrase: '"lightning bolt"' → exact phrase match
        //   - Boolean: "lightning OR bolt" → either word
        var ftsSql = @"
            SELECT c.* FROM CardsFts f
            JOIN Cards c ON c.Id = f.CardId
            WHERE CardsFts MATCH @Query
            ORDER BY rank
            LIMIT @Limit";

        var rows = await conn.QueryAsync<MagicCardRow>(ftsSql, new { Query = query, Limit = limit });
        var ownCountsMap = await LoadAllOwnCountsAsync(conn, ct);
        var cards = rows.Select(r => MapToMagicCard(r, ownCountsMap.GetValueOrDefault(r.Id))).ToList();

        // Load card faces for all cards
        var cardIds = cards.Select(c => c.Id).ToList();
        var facesMap = await LoadCardFacesForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (facesMap.TryGetValue(card.Id, out var faces))
                card.CardFaces = faces;
        }

        // Load all_parts (related cards) for all cards
        var allPartsMap = await LoadAllPartsForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (allPartsMap.TryGetValue(card.Id, out var parts))
                card.AllParts = parts;
        }

        // Load ImageUris for all cards
        var imageUrisMap = await LoadCardsImageUrisForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (imageUrisMap.TryGetValue(card.Id, out var imageUris))
                card.ImageUris = imageUris;
        }

        return cards;
    }

    public async Task<ICollection<MagicCard>> GetBySetAsync(string setAbbreviation, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<MagicCardRow>(
            "SELECT * FROM Cards WHERE SetCode = @SetCode ORDER BY CollectorNumber",
            new { SetCode = setAbbreviation });
        var ownCountsMap = await LoadAllOwnCountsAsync(conn, ct);
        var cards = rows.Select(r => MapToMagicCard(r, ownCountsMap.GetValueOrDefault(r.Id))).ToList();

        // Load card faces for all cards
        var cardIds = cards.Select(c => c.Id).ToList();
        var facesMap = await LoadCardFacesForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (facesMap.TryGetValue(card.Id, out var faces))
                card.CardFaces = faces;
        }

        // Load all_parts (related cards) for all cards
        var allPartsMap = await LoadAllPartsForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (allPartsMap.TryGetValue(card.Id, out var parts))
                card.AllParts = parts;
        }

        // Load ImageUris for all cards
        var imageUrisMap = await LoadCardsImageUrisForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (imageUrisMap.TryGetValue(card.Id, out var imageUris))
                card.ImageUris = imageUris;
        }

        return cards;
    }

    public async Task<ICollection<MagicCard>> GetPrintingsAsync(string name, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<MagicCardRow>(
            "SELECT * FROM Cards WHERE Name = @Name ORDER BY SetCode",
            new { Name = name });
        var ownCountsMap = await LoadAllOwnCountsAsync(conn, ct);
        var cards = rows.Select(r => MapToMagicCard(r, ownCountsMap.GetValueOrDefault(r.Id))).ToList();

        // Load card faces for all cards
        var cardIds = cards.Select(c => c.Id).ToList();
        var facesMap = await LoadCardFacesForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (facesMap.TryGetValue(card.Id, out var faces))
                card.CardFaces = faces;
        }

        // Load all_parts (related cards) for all cards
        var allPartsMap = await LoadAllPartsForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (allPartsMap.TryGetValue(card.Id, out var parts))
                card.AllParts = parts;
        }

        // Load ImageUris for all cards
        var imageUrisMap = await LoadCardsImageUrisForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (imageUrisMap.TryGetValue(card.Id, out var imageUris))
                card.ImageUris = imageUris;
        }

        return cards;
    }

    /// <summary>
    /// Paginated card list with SQL-level filtering, sorting and pagination.
    /// Filters (SetCode, Rarity), sort field/direction, LIMIT/OFFSET applied in SQL.
    /// </summary>
    public async Task<CardListResult> GetPagedAsync(CardListFilter filter, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);

        // SQLite LIKE does NOT use custom collations. Override the like() scalar function for full Unicode case-insensitive support.
        // SQLite maps: X LIKE Y  ->  like(Y, X)
        conn.CreateFunction("like", (string pattern, string value) =>
        {
            if (pattern is null || value is null) return false;
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("%", ".*").Replace("_", ".") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(value, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        });

        // Build WHERE clause
        var whereParts = new List<string>();
        var parameters = new DynamicParameters();

        // Set code filters: multiple values combined as OR (set1 OR set2 OR ...)
        if (filter.SetCodes != null && filter.SetCodes.Count > 0)
        {
            var setConditions = new List<string>();
            for (int i = 0; i < filter.SetCodes.Count; i++)
            {
                var param = $"SetCodeFilter{i}";
                setConditions.Add($"SetCode = @{param}");
                parameters.Add(param, filter.SetCodes[i]);
            }
            whereParts.Add("(" + string.Join(" OR ", setConditions) + ")");
        }
        // Rarity filters: multiple values combined as OR (common OR uncommon OR ...)
        if (filter.Rarity != null && filter.Rarity.Count > 0)
        {
            var rarityConditions = new List<string>();
            for (int i = 0; i < filter.Rarity.Count; i++)
            {
                var param = $"RarityFilter{i}";
                rarityConditions.Add($"Rarity = @{param}");
                parameters.Add(param, filter.Rarity[i]);
            }
            whereParts.Add("(" + string.Join(" OR ", rarityConditions) + ")");
        }
        // Text search: FTS5 via CardsFts virtual table (relevance-ranked)
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            whereParts.Add(@"Id IN (SELECT CardId FROM CardsFts WHERE CardsFts MATCH @QueryFTS ORDER BY rank LIMIT 5000)");
            parameters.Add("QueryFTS", filter.Query);
        }
        // Type filters: multiple values combined as OR (item1 OR item2 OR ...)
        if (filter.TypeFilters != null && filter.TypeFilters.Count > 0)
        {
            var typeConditions = new List<string>();
            for (int i = 0; i < filter.TypeFilters.Count; i++)
            {
                var param = $"TypeFilter{i}";
                typeConditions.Add($"Type LIKE @{param}");
                parameters.Add(param, "%" + filter.TypeFilters[i] + "%");
            }
            whereParts.Add("(" + string.Join(" OR ", typeConditions) + ")");
        }
        // Artist filter: LIKE '%value%' (case-insensitive in SQLite)
        if (!string.IsNullOrWhiteSpace(filter.Artist))
        {
            whereParts.Add("Artist LIKE @ArtistFilter");
            parameters.Add("ArtistFilter", "%" + filter.Artist + "%");
        }
        // ColorType filter: exact match (case-insensitive). Legacy — superseded by ColorFilter.
        if (!string.IsNullOrWhiteSpace(filter.ColorType) && filter.ColorFilter == null)
        {
            whereParts.Add("ColorType = @ColorType");
            parameters.Add("ColorType", filter.ColorType);
        }
        // ColorFilter: bitmask-based color filtering (12-checkbox logic → bit masks)
        // Uses DFC support: checks Cards.ColorBits/IdentityBits directly AND CardFaces via subquery
        if (filter.ColorFilter != null && !filter.ColorFilter.IsEmpty)
        {
            var sqlWhere = filter.ColorFilter.ToSqlWhereWithDfcSupport();
            if (sqlWhere != null)
                whereParts.Add(sqlWhere);
        }

        // Name filter: case-insensitive LIKE (function overridden for Unicode support)
        if (!string.IsNullOrWhiteSpace(filter.NameFilter))
        {
            var likeVal = "%" + filter.NameFilter + "%";
            whereParts.Add(@"(
                PrintedName LIKE @NameFilter OR Name LIKE @NameFilter OR
                Id IN (SELECT CardId FROM CardFaces WHERE (PrintedName LIKE @NameFilter OR Name LIKE @NameFilter))
            )");
            parameters.Add("NameFilter", likeVal);
        }

        // Numeric property filters: PowerNumber (INTEGER), ToughnessNumber (INTEGER), CmcNumber (INTEGER)
        // Uses DFC support: checks Cards NUMBER columns directly AND CardFaces via subquery
        if (filter.PowerFilter?.IsActive == true)
        {
            whereParts.Add(filter.PowerFilter.ToSqlWhereWithDfcSupport("PowerNumber", "cf.PowerNumber")!);
        }
        if (filter.ToughnessFilter?.IsActive == true)
        {
            whereParts.Add(filter.ToughnessFilter.ToSqlWhereWithDfcSupport("ToughnessNumber", "cf.ToughnessNumber")!);
        }
        if (filter.CmcFilter?.IsActive == true)
        {
            whereParts.Add(filter.CmcFilter.ToSqlWhereWithDfcSupport("CmcNumber", "cf.CmcNumber")!);
        }

        // Layout filter: parse string → LayoutEnum → integer for exact match on Layout column
        if (!string.IsNullOrWhiteSpace(filter.LayoutFilter)
            && Enum.TryParse<LayoutEnum>(filter.LayoutFilter, true, out var layoutEnum))
        {
            whereParts.Add("Layout = @LayoutFilter");
            parameters.Add("LayoutFilter", (int)layoutEnum);
        }

        // Legality filter: exact match within comma-separated Legal column.
        // Pads with commas to avoid partial matches (e.g. "standard" != "future_standard").
        if (!string.IsNullOrWhiteSpace(filter.LegalityFilter))
        {
            whereParts.Add("(',' || Legal || ',' LIKE @LegalityFilter)");
            parameters.Add("LegalityFilter", "%," + filter.LegalityFilter + ",%");
        }

        // GameChanger filter: only cards with GameChanger = 1
        if (filter.GameChangerFilter)
        {
            whereParts.Add("GameChanger = 1");
        }

        // Location filter: restrict to cards present in a specific collection node
        // (deck/sideboard/collection/folder). Composite PK (CollectionId, CardId)
        // guarantees one row per card — no duplicates in pagination.
        var fromClause = "FROM Cards";
        if (filter.LocationFilter.HasValue)
        {
            fromClause = @"FROM Cards
                INNER JOIN Deck d ON d.CardId = Cards.Id AND d.CollectionId = @LocationId";
            parameters.Add("LocationId", filter.LocationFilter.Value);

            // Collection filters — location mode only (alias d exists here).
            if (!string.IsNullOrEmpty(filter.Ownership) && filter.Ownership != "all")
            {
                whereParts.Add(filter.Ownership == "owned" ? "d.IsOwn = 1" : "d.IsOwn = 0");
            }
            if (filter.MinCount.HasValue)
            {
                whereParts.Add("d.Count >= @MinCount");
                parameters.Add("MinCount", filter.MinCount.Value);
            }
            if (filter.MaxCount.HasValue)
            {
                whereParts.Add("d.Count <= @MaxCount");
                parameters.Add("MaxCount", filter.MaxCount.Value);
            }
            // Price range on Cards.Usd (TEXT from Scryfall — cast to REAL; guard NULL/empty).
            if (filter.MinPrice.HasValue)
            {
                whereParts.Add("(Cards.Usd IS NOT NULL AND Cards.Usd != '' AND CAST(Cards.Usd AS REAL) >= @MinPrice)");
                parameters.Add("MinPrice", filter.MinPrice.Value);
            }
            if (filter.MaxPrice.HasValue)
            {
                whereParts.Add("(Cards.Usd IS NOT NULL AND Cards.Usd != '' AND CAST(Cards.Usd AS REAL) <= @MaxPrice)");
                parameters.Add("MaxPrice", filter.MaxPrice.Value);
            }
            if (filter.HasForTrade)
            {
                whereParts.Add("d.ForTradeCount IS NOT NULL AND d.ForTradeCount > 0");
            }
            if (filter.HasComment)
            {
                whereParts.Add("d.Comment IS NOT NULL AND d.Comment != ''");
            }
            // filter.IsFoil — placeholder: no foil flag column yet, intentionally not used in SQL.
        }

        var whereClause = whereParts.Count > 0 ? "WHERE " + string.Join(" AND ", whereParts) : "";

        // Validate sort field
        var allowedSortFields = new[] { CardListFilter.SortFields.Name, CardListFilter.SortFields.CmcNumber,
            CardListFilter.SortFields.Type, CardListFilter.SortFields.SetCode, CardListFilter.SortFields.Rarity };
        var sortField = allowedSortFields.Contains(filter.SortField, StringComparer.OrdinalIgnoreCase)
            ? filter.SortField : CardListFilter.SortFields.Name;

        // Count query
        var countSql = $"SELECT COUNT(*) {fromClause} {whereClause}";
        var totalCount = await conn.QuerySingleAsync<int>(countSql, parameters);

        // Data query with sorting and pagination
        var sortDirection = filter.SortAsc ? "ASC" : "DESC";
        var dataSql = $@"
            SELECT Cards.* {fromClause}
            {whereClause}
            ORDER BY [{sortField}] {sortDirection}
            LIMIT @Limit OFFSET @Offset";

        parameters.Add("Limit", filter.PageSize);
        parameters.Add("Offset", filter.Offset);

        var rows = await conn.QueryAsync<MagicCardRow>(dataSql, parameters);
        var ownCountsMap = await LoadAllOwnCountsAsync(conn, ct);
        var cards = rows.Select(r => MapToMagicCard(r, ownCountsMap.GetValueOrDefault(r.Id))).ToList();

        // Load card faces
        var cardIds = cards.Select(c => c.Id).ToList();
        var facesMap = await LoadCardFacesForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (facesMap.TryGetValue(card.Id, out var faces))
                card.CardFaces = faces;
        }

        // Load all_parts
        var allPartsMap = await LoadAllPartsForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (allPartsMap.TryGetValue(card.Id, out var parts))
                card.AllParts = parts;
        }

        // Load ImageUris
        var imageUrisMap = await LoadCardsImageUrisForIdsAsync(conn, cardIds, ct);
        foreach (var card in cards)
        {
            if (imageUrisMap.TryGetValue(card.Id, out var imageUris))
                card.ImageUris = imageUris;
        }

        return new CardListResult(cards, totalCount);
    }

    // === CardSet methods ===
    public async Task AddSetAsync(CardSet cardSet, CancellationToken ct = default)
    {
        if (cardSet.Id == Guid.Empty || cardSet.Id == default)
            throw new ArgumentException("Card Set ID can't be a default guid");

        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO CardSet (
                Id, Name, SetCode, Uri, ScryfallUri, SearchUri,
                SetType, CardCount, ParentSetCode, Digital, NonfoilOnly, FoilOnly,
                IconSvgUri, ReleaseDate)
            VALUES (@Id, @Name, @SetCode, @Uri, @ScryfallUri, @SearchUri,
                    @SetType, @CardCount, @ParentSetCode, @Digital, @NonfoilOnly, @FoilOnly,
                    @IconSvgUri, @ReleaseDate)",
            new
            {
                cardSet.Id,
                cardSet.Name,
                cardSet.SetCode,
                cardSet.Uri,
                cardSet.ScryfallUri,
                cardSet.SearchUri,
                cardSet.SetType,
                cardSet.CardCount,
                cardSet.ParentSetCode,
                cardSet.Digital,
                cardSet.NonfoilOnly,
                cardSet.FoilOnly,
                cardSet.IconSvgUri,
                cardSet.ReleaseDate
            });
    }

    public async Task<CardSet?> GetCardSetAsync(string setCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(setCode)) return null;

        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync<CardSetRow>(
            "SELECT * FROM CardSet WHERE SetCode = @SetCode",
            new { SetCode = setCode.ToLowerInvariant() });

        if (row == null) return null;

        return new CardSet(
            row.Id,
            row.SetCode,
            row.Name,
            uri: row.Uri,
            scryfallUri: row.ScryfallUri,
            searchUri: row.SearchUri,
            setType: row.SetType ?? "?",
            cardCount: row.CardCount,
            parentSetCode: row.ParentSetCode,
            digital: row.Digital,
            nonfoilOnly: row.NonfoilOnly,
            foilOnly: row.FoilOnly,
            iconSvgUri: row.IconSvgUri,
            releaseDate: row.ReleaseDate);
    }

    public async Task<ICollection<CardSet>> GetCardSetsAsync(CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<CardSetRow>(
            "SELECT * FROM CardSet ORDER BY ReleaseDate");
        return rows.Select(r =>
        {
            var cardSet = new CardSet(
                r.Id,
                r.SetCode,
                r.Name,
                uri: r.Uri,
                scryfallUri: r.ScryfallUri,
                searchUri: r.SearchUri,
                setType: r.SetType ?? "?",
                cardCount: r.CardCount,
                parentSetCode: r.ParentSetCode,
                digital: r.Digital,
                nonfoilOnly: r.NonfoilOnly,
                foilOnly: r.FoilOnly,
                iconSvgUri: r.IconSvgUri,
                releaseDate: r.ReleaseDate);
            return cardSet;
        }).ToList();
    }

    public async Task<int> BulkUpsertSetsAsync(CardSet[] sets, CancellationToken ct = default)
    {
        if (sets.Length == 0) return 0;

        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var parameters = sets.Select(s => new
            {
                s.Id,
                s.Name,
                s.SetCode,
                s.Uri,
                s.ScryfallUri,
                s.SearchUri,
                s.SetType,
                s.CardCount,
                s.ParentSetCode,
                s.Digital,
                s.NonfoilOnly,
                s.FoilOnly,
                s.IconSvgUri,
                s.ReleaseDate
            }).ToArray();

            var count = await conn.ExecuteAsync(@"
                INSERT OR REPLACE INTO CardSet (
                    Id, Name, SetCode, Uri, ScryfallUri, SearchUri,
                    SetType, CardCount, ParentSetCode, Digital, NonfoilOnly, FoilOnly,
                    IconSvgUri, ReleaseDate)
                VALUES (@Id, @Name, @SetCode, @Uri, @ScryfallUri, @SearchUri,
                        @SetType, @CardCount, @ParentSetCode, @Digital, @NonfoilOnly, @FoilOnly,
                        @IconSvgUri, @ReleaseDate)",
                parameters, transaction: tx);

            await tx.CommitAsync(ct);
            return count;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> BulkUpdateSetsAsync(CardSet[] sets, CancellationToken ct = default)
    {
        if (sets.Length == 0) return 0;

        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var parameters = sets.Select(s => new
            {
                s.Id,
                s.Name,
                s.SetCode,
                s.Uri,
                s.ScryfallUri,
                s.SearchUri,
                s.SetType,
                s.CardCount,
                s.ParentSetCode,
                s.Digital,
                s.NonfoilOnly,
                s.FoilOnly,
                s.IconSvgUri,
                s.ReleaseDate
            }).ToArray();

            var count = await conn.ExecuteAsync(@"
                UPDATE CardSet SET
                    Name = @Name, SetCode = @SetCode, Uri = @Uri, ScryfallUri = @ScryfallUri,
                    SearchUri = @SearchUri, SetType = @SetType, CardCount = @CardCount,
                    ParentSetCode = @ParentSetCode, Digital = @Digital, NonfoilOnly = @NonfoilOnly,
                    FoilOnly = @FoilOnly, IconSvgUri = @IconSvgUri, ReleaseDate = @ReleaseDate
                WHERE Id = @Id",
                parameters, transaction: tx);

            await tx.CommitAsync(ct);
            return count;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> BulkInsertNewSetsAsync(CardSet[] sets, CancellationToken ct = default)
    {
        if (sets.Length == 0) return 0;

        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);
        using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var parameters = sets.Select(s => new
            {
                s.Id,
                s.Name,
                s.SetCode,
                s.Uri,
                s.ScryfallUri,
                s.SearchUri,
                s.SetType,
                s.CardCount,
                s.ParentSetCode,
                s.Digital,
                s.NonfoilOnly,
                s.FoilOnly,
                s.IconSvgUri,
                s.ReleaseDate
            }).ToArray();

            var count = await conn.ExecuteAsync(@"
                INSERT OR IGNORE INTO CardSet (
                    Id, Name, SetCode, Uri, ScryfallUri, SearchUri,
                    SetType, CardCount, ParentSetCode, Digital, NonfoilOnly, FoilOnly,
                    IconSvgUri, ReleaseDate)
                VALUES (@Id, @Name, @SetCode, @Uri, @ScryfallUri, @SearchUri,
                        @SetType, @CardCount, @ParentSetCode, @Digital, @NonfoilOnly, @FoilOnly,
                        @IconSvgUri, @ReleaseDate)",
                parameters, transaction: tx);

            await tx.CommitAsync(ct);
            return count;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Saves card faces to the database using a three-way sync:
    ///   - UPDATE existing rows (matching FaceIndex)
    ///   - INSERT new rows (FaceIndex not in DB)
    ///   - DELETE removed rows (FaceIndex in DB but not in incoming data)
    /// If the card has no faces, only DELETE is executed — cleaning up stale data.
    /// </summary>
    private static async Task SaveCardFacesAsync(SqliteConnection conn, MagicCard card)
    {
        var cardId = card.Id;

        // Load existing faces from DB keyed by FaceIndex
        var existingRows = await conn.QueryAsync<CardFaceRow>(
            @"SELECT FaceIndex FROM CardFaces WHERE CardId = @CardId ORDER BY FaceIndex",
            new { CardId = cardId });
        var existingIndices = existingRows.ToDictionary(r => r.FaceIndex);

        // Incoming faces keyed by index
        var incomingFaces = card.CardFaces ?? new CardFace[0];
        var incomingIndices = Enumerable.Range(0, incomingFaces.Length).ToHashSet();

        // 1) UPDATE existing rows whose FaceIndex still exists in incoming data
        foreach (var kvp in existingIndices)
        {
            if (!incomingIndices.Contains(kvp.Key))
                continue; // will be deleted below

            var face = incomingFaces[kvp.Key];
            await conn.ExecuteAsync(@"
                UPDATE CardFaces SET
                    Name = @Name, ManaCost = @ManaCost, TypeLine = @TypeLine,
                    OracleText = @OracleText, Power = @Power, PowerNumber = @PowerNumber, Toughness = @Toughness, ToughnessNumber = @ToughnessNumber,
                    CmcNumber = @CmcNumber, CmcText = @CmcText, Colors = @Colors, ColorIndicator = @ColorIndicator,
                    Loyalty = @Loyalty, Artist = @Artist, IllustrationId = @IllustrationId,
                    FlavorText = @FlavorText,
                    PrintedName = @PrintedName, PrintedText = @PrintedText,
                    PrintedTypeLine = @PrintedTypeLine,
                    ColorBits = @ColorBits, IdentityBits = @IdentityBits
                WHERE CardId = @CardId AND FaceIndex = @FaceIndex",
                new
                {
                    CardId = cardId,
                    FaceIndex = kvp.Key,
                    face.Name,
                    face.ManaCost,
                    face.TypeLine,
                    face.OracleText,
                    face.Power,
                    face.PowerNumber,
                    face.Toughness,
                    face.ToughnessNumber,
                    face.CmcNumber,
                    face.CmcText,
                    Colors = face.Colors is { Count: > 0 } ? string.Join(',', face.Colors) : (string?)null,
                    ColorIndicator = face.ColorIndicator is { Count: > 0 } ? string.Join(',', face.ColorIndicator) : (string?)null,
                    face.Loyalty,
                    face.Artist,
                    face.IllustrationId,
                    
                    face.FlavorText,
                    face.PrintedName,
                    face.PrintedText,
                    face.PrintedTypeLine,
                    face.ColorBits,
                    face.IdentityBits
                });
        }

        // 2) INSERT new rows (FaceIndex not in DB)
        var insertSql = @"
            INSERT INTO CardFaces (CardId, FaceIndex, Name, ManaCost, TypeLine, OracleText, Power, PowerNumber, Toughness, ToughnessNumber, CmcNumber, CmcText, Colors, ColorIndicator, Loyalty, Artist, IllustrationId, FlavorText, PrintedName, PrintedText, PrintedTypeLine, ColorBits, IdentityBits)
            VALUES (@CardId, @FaceIndex, @Name, @ManaCost, @TypeLine, @OracleText, @Power, @PowerNumber, @Toughness, @ToughnessNumber, @CmcNumber, @CmcText, @Colors, @ColorIndicator, @Loyalty, @Artist, @IllustrationId, @FlavorText, @PrintedName, @PrintedText, @PrintedTypeLine, @ColorBits, @IdentityBits)";

        foreach (var idx in incomingIndices)
        {
            if (existingIndices.ContainsKey(idx))
                continue; // already updated above

            var face = incomingFaces[idx];
            await conn.ExecuteAsync(insertSql, new
            {
                CardId = cardId,
                FaceIndex = idx,
                face.Name,
                face.ManaCost,
                face.TypeLine,
                face.OracleText,
                face.Power,
                face.PowerNumber,
                face.Toughness,
                face.ToughnessNumber,
                face.CmcNumber,
                face.CmcText,
                Colors = face.Colors is { Count: > 0 } ? string.Join(',', face.Colors) : (string?)null,
                ColorIndicator = face.ColorIndicator is { Count: > 0 } ? string.Join(',', face.ColorIndicator) : (string?)null,
                face.Loyalty,
                face.Artist,
                face.IllustrationId,
                
                face.FlavorText,
                face.PrintedName,
                face.PrintedText,
                face.PrintedTypeLine,
                face.ColorBits,
                face.IdentityBits
            });
        }

        // 3) DELETE rows whose FaceIndex is no longer in incoming data
        foreach (var kvp in existingIndices)
        {
            if (!incomingIndices.Contains(kvp.Key))
            {
                await conn.ExecuteAsync(
                    @"DELETE FROM CardFaces WHERE CardId = @CardId AND FaceIndex = @FaceIndex",
                    new { cardId, FaceIndex = kvp.Key });
            }
        }

        // Save ImageUris for all faces
        foreach (var idx in incomingIndices)
        {
            var face = incomingFaces[idx];
            // Get the face Id from DB (for UPDATE) or assume it will be inserted (for INSERT)
            var existingRow = existingIndices.GetValueOrDefault(idx);
            if (existingRow != null)
            {
                // For updated faces, we need to get the actual row Id
                var faceRow = await conn.QuerySingleOrDefaultAsync<CardFaceRowWithId>(
                    "SELECT Id FROM CardFaces WHERE CardId = @CardId AND FaceIndex = @FaceIndex",
                    new { CardId = cardId, FaceIndex = idx });
                if (faceRow != null)
                    await SaveCardFacesImageUrisAsync(conn, faceRow.Id, face);
            }
            else
            {
                // For new faces, we need to get the Id after INSERT
                // The INSERT above already happened, so we can query it now
                var faceRow = await conn.QuerySingleOrDefaultAsync<CardFaceRowWithId>(
                    "SELECT Id FROM CardFaces WHERE CardId = @CardId AND FaceIndex = @FaceIndex",
                    new { CardId = cardId, FaceIndex = idx });
                if (faceRow != null)
                    await SaveCardFacesImageUrisAsync(conn, faceRow.Id, face);
            }
        }

        // Delete ImageUris for removed faces
        foreach (var kvp in existingIndices)
        {
            if (!incomingIndices.Contains(kvp.Key))
            {
                var faceRow = await conn.QuerySingleOrDefaultAsync<CardFaceRowWithId>(
                    "SELECT Id FROM CardFaces WHERE CardId = @CardId AND FaceIndex = @FaceIndex",
                    new { CardId = cardId, FaceIndex = kvp.Key });
                if (faceRow != null)
                {
                    await conn.ExecuteAsync("DELETE FROM CardFacesImageUris WHERE FaceId = @FaceId", new { FaceId = faceRow.Id });
                }
            }
        }
    }

    /// <summary>
    /// Loads card faces from the database for a single card.
    /// </summary>
    private static async Task<CardFace[]> LoadCardFacesAsync(SqliteConnection conn, Guid cardId, CancellationToken ct = default)
    {
        var sql = @"
            SELECT Id, Name, ManaCost, TypeLine, OracleText, Power, PowerNumber, Toughness, ToughnessNumber, CmcNumber, CmcText, Colors, ColorIndicator, Loyalty, Artist, IllustrationId, FlavorText, PrintedName, PrintedText, PrintedTypeLine, ColorBits, IdentityBits
            FROM CardFaces
            WHERE CardId = @CardId
            ORDER BY FaceIndex";

        var rows = await conn.QueryAsync<CardFaceRowWithId2>(sql, new { CardId = cardId });
        
        // Load ImageUris for all faces
        var faceIds = rows.Select(r => r.Id).ToList();
        var imageUrisMap = new Dictionary<int, ImageUris>();
        foreach (var faceId in faceIds)
        {
            var imageUris = await LoadCardFacesImageUrisByFaceIdAsync(conn, faceId, ct);
            if (imageUris != null)
                imageUrisMap[faceId] = imageUris;
        }

        return rows.Select(r => MapToCardFace(r, imageUrisMap.GetValueOrDefault(r.Id))).ToArray();
    }

    /// <summary>
    /// Loads card faces from the database for multiple cards.
    /// </summary>
    private static async Task<Dictionary<Guid, CardFace[]>> LoadCardFacesForIdsAsync(SqliteConnection conn, ICollection<Guid> cardIds, CancellationToken ct = default)
    {
        if (cardIds == null || !cardIds.Any())
            return new Dictionary<Guid, CardFace[]>();

        var parameters = new DynamicParameters();
        for (var i = 0; i < cardIds.Count; i++)
            parameters.Add($"@id{i}", cardIds.ElementAt(i));

        var placeholders = string.Join(", ", cardIds.Select((_, i) => $"@id{i}"));
        var sql = $@"
            SELECT Id, CardId, Name, ManaCost, TypeLine, OracleText, Power, PowerNumber, Toughness, ToughnessNumber, CmcNumber, CmcText, Colors, ColorIndicator, Loyalty, Artist, IllustrationId, FlavorText, PrintedName, PrintedText, PrintedTypeLine, ColorBits, IdentityBits
            FROM CardFaces
            WHERE CardId IN ({placeholders})
            ORDER BY CardId, FaceIndex";

        var rows = await conn.QueryAsync<CardFaceRowWithId2Extended>(sql, parameters);
        
        // Load ImageUris for all faces
        var faceIds = rows.Select(r => r.Id).ToList();
        var imageUrisMap = new Dictionary<int, ImageUris>();
        foreach (var faceId in faceIds)
        {
            var imageUris = await LoadCardFacesImageUrisByFaceIdAsync(conn, faceId, ct);
            if (imageUris != null)
                imageUrisMap[faceId] = imageUris;
        }

        var grouped = rows.GroupBy(r => r.CardId).ToList();
        var result = new Dictionary<Guid, CardFace[]>(grouped.Count);
        foreach (var group in grouped)
        {
            result[group.Key] = group.Select(r => MapToCardFace(r, imageUrisMap.GetValueOrDefault(r.Id))).ToArray();
        }

        return result;
    }

    /// <summary>
    /// Saves all_parts (related cards) to the database using a three-way sync:
    ///   - UPDATE existing rows (matching PartIndex)
    ///   - INSERT new rows (PartIndex not in DB)
    ///   - DELETE removed rows (PartIndex in DB but not in incoming data)
    /// If the card has no related parts, only DELETE is executed — cleaning up stale data.
    /// </summary>
    private static async Task SaveAllPartsAsync(SqliteConnection conn, MagicCard card)
    {
        var cardId = card.Id;

        // Load existing parts from DB keyed by PartIndex
        var existingRows = await conn.QueryAsync<RelatedCardRow>(
            @"SELECT PartIndex FROM CardAllParts WHERE CardId = @CardId ORDER BY PartIndex",
            new { CardId = cardId });
        var existingIndices = existingRows.ToDictionary(r => r.PartIndex);

        // Incoming parts keyed by index
        var incomingParts = card.AllParts ?? new RelatedCard[0];
        var incomingIndices = Enumerable.Range(0, incomingParts.Length).ToHashSet();

        // 1) UPDATE existing rows whose PartIndex still exists in incoming data
        foreach (var kvp in existingIndices)
        {
            if (!incomingIndices.Contains(kvp.Key))
                continue; // will be deleted below

            var part = incomingParts[kvp.Key];
            await conn.ExecuteAsync(@"
                UPDATE CardAllParts SET
                    RelatedCardId = @RelatedCardId, Component = @Component,
                    Name = @Name, TypeLine = @TypeLine, Uri = @Uri
                WHERE CardId = @CardId AND PartIndex = @PartIndex",
                new
                {
                    CardId = cardId,
                    PartIndex = kvp.Key,
                    RelatedCardId = part.Id,
                    Component = part.Component,
                    Name = part.Name,
                    TypeLine = part.TypeLine,
                    Uri = part.Uri
                });
        }

        // 2) INSERT new rows (PartIndex not in DB)
        var insertSql = @"
            INSERT INTO CardAllParts (CardId, PartIndex, RelatedCardId, Component, Name, TypeLine, Uri)
            VALUES (@CardId, @PartIndex, @RelatedCardId, @Component, @Name, @TypeLine, @Uri)";

        foreach (var idx in incomingIndices)
        {
            if (existingIndices.ContainsKey(idx))
                continue; // already updated above

            var part = incomingParts[idx];
            await conn.ExecuteAsync(insertSql, new
            {
                CardId = cardId,
                PartIndex = idx,
                RelatedCardId = part.Id,
                Component = part.Component,
                Name = part.Name,
                TypeLine = part.TypeLine,
                Uri = part.Uri
            });
        }

        // 3) DELETE rows whose PartIndex is no longer in incoming data
        foreach (var kvp in existingIndices)
        {
            if (!incomingIndices.Contains(kvp.Key))
            {
                await conn.ExecuteAsync(
                    @"DELETE FROM CardAllParts WHERE CardId = @CardId AND PartIndex = @PartIndex",
                    new { CardId = cardId, PartIndex = kvp.Key });
            }
        }
    }

    /// <summary>
    /// Saves ImageUris for a card to CardsImageUris table.
    /// Uses INSERT OR REPLACE for upsert semantics.
    /// </summary>
    private static async Task SaveCardsImageUrisAsync(SqliteConnection conn, MagicCard card)
    {
        if (card.ImageUris == null)
        {
            // Delete existing ImageUris if card has none
            await conn.ExecuteAsync(
                "DELETE FROM CardsImageUris WHERE CardId = @CardId",
                new { CardId = card.Id });
            return;
        }

        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO CardsImageUris (CardId, Small, Normal, Large, Png)
            VALUES (@CardId, @Small, @Normal, @Large, @Png)",
            new
            {
                CardId = card.Id,
                card.ImageUris.Small,
                card.ImageUris.Normal,
                card.ImageUris.Large,
                card.ImageUris.Png
            });
    }

    /// <summary>
    /// Loads ImageUris for a card from CardsImageUris table.
    /// </summary>
    private static async Task<ImageUris?> LoadCardsImageUrisAsync(SqliteConnection conn, Guid cardId, CancellationToken ct = default)
    {
        var row = await conn.QuerySingleOrDefaultAsync<ImageUrisRow>(
            "SELECT Small, Normal, Large, Png FROM CardsImageUris WHERE CardId = @CardId",
            new { CardId = cardId });

        if (row == null) return null;
        return new ImageUris(row.Small, row.Normal, row.Large, row.Png);
    }

    /// <summary>
    /// Loads ImageUris for multiple cards from CardsImageUris table.
    /// </summary>
    private static async Task<Dictionary<Guid, ImageUris>> LoadCardsImageUrisForIdsAsync(SqliteConnection conn, ICollection<Guid> cardIds, CancellationToken ct = default)
    {
        if (cardIds == null || !cardIds.Any())
            return new Dictionary<Guid, ImageUris>();

        var parameters = new DynamicParameters();
        for (var i = 0; i < cardIds.Count; i++)
            parameters.Add($"@id{i}", cardIds.ElementAt(i));

        var placeholders = string.Join(", ", cardIds.Select((_, i) => $"@id{i}"));
        var sql = $"SELECT CardId, Small, Normal, Large, Png FROM CardsImageUris WHERE CardId IN ({placeholders})";

        var rows = await conn.QueryAsync<ImageUrisRowWithId>(sql, parameters);
        return rows.ToDictionary(
            r => r.CardId,
            r => new ImageUris(r.Small, r.Normal, r.Large, r.Png));
    }

    /// <summary>
    /// Saves ImageUris for a card face to CardFacesImageUris table.
    /// Uses INSERT OR REPLACE for upsert semantics.
    /// </summary>
    private static async Task SaveCardFacesImageUrisAsync(SqliteConnection conn, int faceId, CardFace face)
    {
        if (face.ImageUris == null)
        {
            // Delete existing ImageUris if face has none
            await conn.ExecuteAsync(
                "DELETE FROM CardFacesImageUris WHERE FaceId = @FaceId",
                new { FaceId = faceId });
            return;
        }

        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO CardFacesImageUris (FaceId, Small, Normal, Large, Png)
            VALUES (@FaceId, @Small, @Normal, @Large, @Png)",
            new
            {
                FaceId = faceId,
                face.ImageUris.Small,
                face.ImageUris.Normal,
                face.ImageUris.Large,
                face.ImageUris.Png
            });
    }

    /// <summary>
    /// Loads ImageUris for card faces from CardFacesImageUris table.
    /// </summary>
    private static async Task<Dictionary<int, ImageUris>> LoadCardFacesImageUrisAsync(SqliteConnection conn, Guid cardId, CancellationToken ct = default)
    {
        var rows = await conn.QueryAsync<CardFaceImageUrisRow>(@"
            SELECT cf.FaceIndex, cui.Small, cui.Normal, cui.Large, cui.Png
            FROM CardFaces cf
            INNER JOIN CardFacesImageUris cui ON cf.Id = cui.FaceId
            WHERE cf.CardId = @CardId
            ORDER BY cf.FaceIndex",
            new { CardId = cardId });

        return rows.ToDictionary(
            r => r.FaceIndex,
            r => new ImageUris(r.Small, r.Normal, r.Large, r.Png));
    }

    /// <summary>
    /// Loads ImageUris for a single card face by its Id.
    /// </summary>
    private static async Task<ImageUris?> LoadCardFacesImageUrisByFaceIdAsync(SqliteConnection conn, int faceId, CancellationToken ct = default)
    {
        var row = await conn.QuerySingleOrDefaultAsync<ImageUrisRow>(
            "SELECT Small, Normal, Large, Png FROM CardFacesImageUris WHERE FaceId = @FaceId",
            new { FaceId = faceId });

        if (row == null) return null;
        return new ImageUris(row.Small, row.Normal, row.Large, row.Png);
    }

    /// <summary>
    /// Loads all_parts (related cards) from the database for a single card.
    /// </summary>
    private static async Task<RelatedCard[]> LoadAllPartsAsync(SqliteConnection conn, Guid cardId, CancellationToken ct = default)
    {
        var sql = @"
            SELECT RelatedCardId, Component, Name, TypeLine, Uri
            FROM CardAllParts
            WHERE CardId = @CardId
            ORDER BY PartIndex";

        var rows = await conn.QueryAsync<RelatedCardRow>(sql, new { CardId = cardId });
        return rows.Select(MapToRelatedCard).ToArray();
    }

    /// <summary>
    /// Loads all_parts (related cards) from the database for multiple cards.
    /// </summary>
    private static async Task<Dictionary<Guid, RelatedCard[]>> LoadAllPartsForIdsAsync(SqliteConnection conn, ICollection<Guid> cardIds, CancellationToken ct = default)
    {
        if (cardIds == null || !cardIds.Any())
            return new Dictionary<Guid, RelatedCard[]>();

        var parameters = new DynamicParameters();
        for (var i = 0; i < cardIds.Count; i++)
            parameters.Add($"@id{i}", cardIds.ElementAt(i));

        var placeholders = string.Join(", ", cardIds.Select((_, i) => $"@id{i}"));
        var sql = $@"
            SELECT CardId, RelatedCardId, Component, Name, TypeLine, Uri
            FROM CardAllParts
            WHERE CardId IN ({placeholders})
            ORDER BY CardId, PartIndex";

        var rows = await conn.QueryAsync<RelatedCardRow>(sql, parameters);
        var grouped = rows.GroupBy(r => r.CardId).ToList();
        var result = new Dictionary<Guid, RelatedCard[]>(grouped.Count);
        foreach (var group in grouped)
        {
            result[group.Key] = group.Select(MapToRelatedCard).ToArray();
        }

        return result;
    }

    private static RelatedCard MapToRelatedCard(RelatedCardRow row)
    {
        return new RelatedCard
        {
            Id = row.RelatedCardId,
            Component = row.Component,
            Name = row.Name,
            TypeLine = row.TypeLine,
            Uri = row.Uri
        };
    }

    private static CardFace MapToCardFace(CardFaceRowWithId2 row, ImageUris? imageUris = null)
    {
        var face = new CardFace
        {
            Name = row.Name,
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
            
            ImageUris = imageUris,
            FlavorText = row.FlavorText,
            PrintedName = row.PrintedName,
            PrintedText = row.PrintedText,
            PrintedTypeLine = row.PrintedTypeLine,
        };

        // Parse CSV for Colors and ColorIndicator
        if (!string.IsNullOrEmpty(row.Colors))
            face.Colors = row.Colors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!string.IsNullOrEmpty(row.ColorIndicator))
            face.ColorIndicator = row.ColorIndicator.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        face.ColorBits = row.ColorBits;
        face.IdentityBits = row.IdentityBits;

        return face;
    }

    private static MagicCard MapToMagicCard(MagicCardRow row, OwnCountStats? ownCounts = null)
    {
        var card = new MagicCard
        {
            Id = row.Id,
            OracleId = row.OracleId,
            Name = row.Name,
            ManaCost = row.ManaCost,
            Layout = (LayoutEnum)row.Layout,
            Type = row.Type,
            Power = row.Power,
            PowerNumber = row.PowerNumber,
            Toughness = row.Toughness,
            ToughnessNumber = row.ToughnessNumber,
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
            DbPrice = row.DbPrice,
            CommunityRating = row.CommunityRating,
        };

        // Restore LegalityMap from comma-separated formats
        if (!string.IsNullOrEmpty(row.LegalityFormats))
        {
            var formats = row.LegalityFormats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var fmt in formats)
            {
                if (Enum.TryParse<Format>(fmt, true, out var format))
                    card.LegalityMap.SetLegal(format);
            }
        }

        // Restore properties
        // Restore Colors from comma-separated string
        if (!string.IsNullOrEmpty(row.Colors))
            card.Colors = row.Colors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Restore ColorIdentity from comma-separated string
        if (!string.IsNullOrEmpty(row.ColorIdentity))
            card.ColorIdentity = row.ColorIdentity.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Restore ColorIndicator from comma-separated string
        if (!string.IsNullOrEmpty(row.ColorIndicator))
            card.ColorIndicator = row.ColorIndicator.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Restore GameChanger
        card.GameChanger = row.GameChanger;

        // Restore Keywords from semicolon-separated string
        if (!string.IsNullOrEmpty(row.Keywords))
            card.Keywords = row.Keywords.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Restore ProducedMana from comma-separated string
        if (!string.IsNullOrEmpty(row.ProducedMana))
            card.ProducedMana = row.ProducedMana.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Restore legality strings
        card.Legal = row.Legal;
        card.NotLegal = row.NotLegal;
        card.Restricted = row.Restricted;
        card.Banned = row.Banned;

        // Restore price info
        card.Usd = row.Usd;
        card.UsdFoil = row.UsdFoil;

        // Restore color bitmasks
        card.ColorBits = row.ColorBits;
        card.IdentityBits = row.IdentityBits;

        // Attach ownership stats from OwnCounts table (if available)
        if (ownCounts is { } stats)
        {
            AttachOwnCountStats(card, stats);
        }

        return card;
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
        // OwnTotalAll == TotalCount for deck-only storage (no separate library tracking)
        if (stats.TotalCount > 0)
            card.OwnTotalAll = stats.TotalCount;
        if (stats.OwnUnique > 0)
            card.OwnUnique = stats.OwnUnique;
    }

    /// <summary>
    /// Invalidate the in-memory cache so that subsequent reads fetch fresh data from the database.
    /// Used by ReconciliationService after bulk property updates to ensure consistency.
    /// The <paramref name="fields"/> parameter is kept for API compatibility but not used —
    /// the entire cache is invalidated since partial invalidation is unreliable.
    /// </summary>
    public async Task UpdateAllFieldsAsync(ICollection<MagicCardField> fields, CancellationToken ct = default)
    {
        // Invalidate L1 cache so next reads hit the DB and pick up any external changes.
        _cache.Clear();
    }

    /// <summary>
    /// Bulk update OwnCount/OwnTotal/OwnUnique properties on base cards in KB.
    /// Uses a temp table approach: inserts stats, then updates Cards via JOIN.
    /// This is the fastest way to bulk-update properties in SQLite.
    /// </summary>
    public async Task BulkUpdateOwnCountAsync(ICollection<OwnCountStats> stats, CancellationToken ct = default)
    {
        if (stats == null || !stats.Any())
            return;

        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);

        // Create a temporary table for the stats
        await conn.ExecuteAsync(@"
            CREATE TEMP TABLE IF NOT EXISTS OwnCountTemp (
                CardId uniqueidentifier PRIMARY KEY,
                OwnCount INTEGER,
                TotalCount INTEGER,
                OwnUnique INTEGER
            )");
        await conn.ExecuteAsync("DELETE FROM OwnCountTemp");

        // Bulk insert stats into temp table
        var list = stats.ToList();
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO OwnCountTemp (CardId, OwnCount, TotalCount, OwnUnique)
            VALUES (@CardId, @OwnCount, @TotalCount, @OwnUnique)",
            list.Select(s => new { s.CardId, s.OwnCount, s.TotalCount, s.OwnUnique }));

        // Persist to OwnCounts table (created by InitializeDb.EnsureCreatedAsync).
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO OwnCounts (CardId, OwnCount, TotalCount, OwnUnique)
            SELECT t.CardId, t.OwnCount, t.TotalCount, t.OwnUnique
            FROM OwnCountTemp t
            WHERE EXISTS (SELECT 1 FROM Cards c WHERE c.Id = t.CardId)");

        // Clean up temp table
        await conn.ExecuteAsync("DROP TABLE IF EXISTS OwnCountTemp");

        // Invalidate cache for affected cards
        foreach (var s in list)
            _cache.TryRemove(s.CardId, out _);
    }

    /// <summary>
    /// Update a single card's property column in KB without replacing the whole row.
    /// </summary>
    public async Task UpdateCardPropertyAsync(Guid cardId, string columnName, string? value, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);

        var sql = $"UPDATE Cards SET [{columnName}] = @Value WHERE Id = @Id";
        var result = await conn.ExecuteAsync(sql, new { Value = value, Id = cardId });

        if (result > 0)
            _cache.TryRemove(cardId, out _);
    }

    /// <summary>
    /// Batch-fetch multiple cards by their IDs in a single SQL query.
    /// Returns a dictionary keyed by CardId for O(1) lookup.
    /// First checks the cache for each ID, then queries DB only for missing ones.
    /// </summary>
    public async Task<Dictionary<Guid, MagicCard>> GetCardsByIdsAsync(ICollection<Guid> ids, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, MagicCard>();

        if (ids == null || !ids.Any())
            return result;

        // First pass: collect from cache
        var missingIds = new List<Guid>();
        foreach (var id in ids)
        {
            if (_cache.TryGetValue(id, out var cached))
                result[id] = cached;
            else
                missingIds.Add(id);
        }

        // Second pass: query DB for missing cards
        if (missingIds.Count > 0)
        {
            using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
            await conn.OpenAsync(ct);

            // Build parameterized IN clause
            var parameters = new DynamicParameters();
            for (var i = 0; i < missingIds.Count; i++)
                parameters.Add($"@id{i}", missingIds[i]);

            var placeholders = string.Join(", ", missingIds.Select((_, i) => $"@id{i}"));
            var sql = $"SELECT * FROM Cards WHERE Id IN ({placeholders})";

            var rows = (await conn.QueryAsync<MagicCardRow>(sql, parameters)).ToList();
            var ownCountsMap = await LoadAllOwnCountsAsync(conn, ct);

            // Load card faces for all missing cards
            var facesMap = await LoadCardFacesForIdsAsync(conn, missingIds, ct);

            // Load all_parts (related cards) for all missing cards
            var allPartsMap = await LoadAllPartsForIdsAsync(conn, missingIds, ct);

            // Load ImageUris for all missing cards
            var imageUrisMap = await LoadCardsImageUrisForIdsAsync(conn, missingIds, ct);

            foreach (var row in rows)
            {
                var card = MapToMagicCard(row, ownCountsMap.GetValueOrDefault(row.Id));

                if (facesMap.TryGetValue(card.Id, out var faces))
                    card.CardFaces = faces;

                if (allPartsMap.TryGetValue(card.Id, out var parts))
                    card.AllParts = parts;

                if (imageUrisMap.TryGetValue(card.Id, out var imageUris))
                    card.ImageUris = imageUris;

                result[card.Id] = card;
                _cache[card.Id] = card;
            }
        }

        return result;
    }

    /// <summary>
    /// Collector progress for all sets — single SQL query using CTEs.
    /// 
    /// Total cards: unique card names per set in the knowledge base (Cards table).
    /// Owned cards: unique card names per set that exist in user's decks (Deck JOIN Cards).
    /// 
    /// Replaces: GetCardSetsAsync + N×GetBySetAsync + recursive tree walk with M×GetDeckCardsAsync.
    /// </summary>
    public async Task<ICollection<CollectorProgressRaw>> GetCollectorProgressRawAsync(CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);

        // Single query: CTE for unique KB cards per set + collected/owned cards per set.
        // LEFT JOIN Editions to include sets with 0 cards in KB.
        var sql = @"
            WITH UniqueKbCards AS (
                SELECT SetCode, COUNT(DISTINCT Name) AS TotalCards
                FROM Cards
                GROUP BY SetCode
            ),
            CollectedCards AS (
                SELECT c.SetCode, COUNT(DISTINCT c.Name) AS CollectedCards
                FROM Deck dc
                INNER JOIN Cards c ON dc.CardId = c.Id
                GROUP BY c.SetCode
            ),
            OwnedCards AS (
                SELECT c.SetCode, COUNT(DISTINCT c.Name) AS OwnedCards
                FROM Deck dc
                INNER JOIN Cards c ON dc.CardId = c.Id
                WHERE dc.IsOwn = 1
                GROUP BY c.SetCode
            )
            SELECT s.SetCode,
                   s.Name AS SetName,
                   COALESCE(u.TotalCards, 0) AS TotalCards,
                   COALESCE(col.CollectedCards, 0) AS CollectedCards,
                   COALESCE(o.OwnedCards, 0) AS OwnedCards
            FROM CardSet s
            LEFT JOIN UniqueKbCards u ON u.SetCode = s.SetCode
            LEFT JOIN CollectedCards col ON col.SetCode = s.SetCode
            LEFT JOIN OwnedCards o ON o.SetCode = s.SetCode
            ORDER BY s.Name";

        // Use dynamic to avoid Dapper type caching issues with empty result sets
        var dynamicRows = await conn.QueryAsync(sql);
        var rows = dynamicRows.Select(r => new CollectorProgressRaw(
            (string)r.SetCode,
            (string?)r.SetName,
            Convert.ToInt64(r.TotalCards),
            Convert.ToInt64(r.CollectedCards),
            Convert.ToInt64(r.OwnedCards))).ToList();
        return rows;
    }

    public ValueTask DisposeAsync()
    {
        _cache.Clear();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Loads ownership stats for a single card from the OwnCounts table.
    /// </summary>
    private static async Task<OwnCountStats?> LoadOwnCountForCardAsync(SqliteConnection conn, Guid cardId, CancellationToken ct = default)
    {
        var row = await conn.QuerySingleOrDefaultAsync<OwnCountRow>(
            "SELECT CardId, OwnCount, TotalCount, OwnUnique FROM OwnCounts WHERE CardId = @Id",
            new { Id = cardId });

        return row != null ? new OwnCountStats(row.CardId, row.OwnCount, row.TotalCount, row.OwnUnique) : null;
    }

    /// <summary>
    /// Loads all ownership stats from the OwnCounts table into a dictionary keyed by CardId.
    /// </summary>
    private static async Task<Dictionary<Guid, OwnCountStats>> LoadAllOwnCountsAsync(
        SqliteConnection conn, CancellationToken ct = default)
    {
        var rows = await conn.QueryAsync<OwnCountRow>(
            "SELECT CardId, OwnCount, TotalCount, OwnUnique FROM OwnCounts");

        return rows.ToDictionary(r => r.CardId, r => new OwnCountStats(r.CardId, r.OwnCount, r.TotalCount, r.OwnUnique));
    }

    private class OwnCountRow
    {
        public Guid CardId { get; set; }
        public int OwnCount { get; set; }
        public int TotalCount { get; set; }
        public int OwnUnique { get; set; }
    }

    // ── Batch resolution for old-format import (Gatherer ID + full set name) ──

    public async Task<Dictionary<string, string>> ResolveSetNamesAsync(ICollection<string> fullNames, CancellationToken ct = default)
    {
        if (fullNames.Count == 0) return new Dictionary<string, string>();

        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);

        // Expand IN-clause manually — Dapper + SQLite don't support collection params in IN().
        var namesList = fullNames.ToList();
        var placeholders = string.Join(", ", namesList.Select((_, i) => $"@N{i}"));
        // COLLATE NOCASE for case-insensitive matching of full set names
        var sql = $"SELECT SetCode, Name FROM CardSet WHERE Name COLLATE NOCASE IN ({placeholders})";

        var paramObj = new System.Dynamic.ExpandoObject() as IDictionary<string, object?>;
        for (int i = 0; i < namesList.Count; i++)
            paramObj[$"N{i}"] = namesList[i];

        var rows = await conn.QueryAsync<CardSetRow>(sql, paramObj);

        return rows.ToDictionary(
            r => r.Name,
            r => r.SetCode,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, MagicCard>> ResolveCardsByNameAndSetAsync(
        ICollection<(string Name, string? Set)> pairs, CancellationToken ct = default)
    {
        if (pairs.Count == 0) return new Dictionary<string, MagicCard>();

        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);

        // Build OR-conditions for (Name, Set) pairs.
        // Each pair gets a parameterized condition: (Name = @N0 AND Set = @S0) OR (Name = @N1 AND Set = @S1)
        // This is a single query that returns all matching cards at once.
        var pairsList = pairs.ToList();
        var conditions = new List<string>();
        var paramObj = new System.Dynamic.ExpandoObject() as IDictionary<string, object?>;

        for (int i = 0; i < pairsList.Count; i++)
        {
            var (name, set) = pairsList[i];
            var nKey = $"N{i}";
            var sKey = $"S{i}";
            paramObj[nKey] = name;
            paramObj[sKey] = (object?)set ?? DBNull.Value;

            if (set == null)
                // Match cards where Set is NULL
                conditions.Add("(Name = @" + nKey + " AND SetCode IS NULL)");
            else
                conditions.Add("(Name = @" + nKey + " AND SetCode = @" + sKey + ")");
        }

        var sql = $"SELECT * FROM Cards WHERE {string.Join(" OR ", conditions)}";
        var rows = await conn.QueryAsync<MagicCardRow>(sql, paramObj);
        var ownCountsMap = await LoadAllOwnCountsAsync(conn, ct);

        // Build composite key → card mapping
        var result = new Dictionary<string, MagicCard>();
        foreach (var row in rows)
        {
            var key = $"{row.Name}|{row.SetCode}";
            if (!result.ContainsKey(key))
                result[key] = MapToMagicCard(row, ownCountsMap.GetValueOrDefault(row.Id));
        }

        return result;
    }

    /// <summary>
    /// Get distinct (Language, Id) pairs for all cards sharing the same OracleId.
    /// </summary>
    public async Task<(string Language, Guid Id)[]> GetAvailableLanguagesAsync(Guid oracleId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(string.Format(ConnectionStringTemplate, _dbPath));
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT DISTINCT Language, Id FROM Cards
            WHERE OracleId = @OracleId
              AND Language IS NOT NULL AND Language != ''
            ORDER BY Language";

        var rows = await conn.QueryAsync<LangIdRow>(sql, new { OracleId = oracleId });
        return rows.Select(r => (r.Language!, r.Id)).ToArray();
    }

    private class LangIdRow
    {
        public string? Language { get; set; }
        public Guid Id { get; set; }
    }

    private class MagicCardRow
    {
        public Guid Id { get; set; }
        public Guid OracleId { get; set; }
        public string Name { get; set; } = "";
        public string? ManaCost { get; set; }
        public int Layout { get; set; }
        public string? Type { get; set; }
        public string? Power { get; set; }
        public int? PowerNumber { get; set; }
        public string? Toughness { get; set; }
        public int? ToughnessNumber { get; set; }
        public string SetCode { get; set; } = "";
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
        public float DbPrice { get; set; }
        public float CommunityRating { get; set; }
        public string? LegalityFormats { get; set; }
        public string? Colors { get; set; }
        public string? ColorIdentity { get; set; }
        public string? ColorIndicator { get; set; }
        public bool? GameChanger { get; set; }
        public string? Keywords { get; set; }
        public string? ProducedMana { get; set; }
        public string? Legal { get; set; }
        public string? NotLegal { get; set; }
        public string? Restricted { get; set; }
        public string? Banned { get; set; }
        public string? Usd { get; set; }
        public string? UsdFoil { get; set; }
        public int? ColorBits { get; set; }
        public int? IdentityBits { get; set; }
    }

    private class CardSetRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string SetCode { get; set; }
        public string? Uri { get; set; }
        public string? ScryfallUri { get; set; }
        public string? SearchUri { get; set; }
        public string? SetType { get; set; } = "?";
        public int CardCount { get; set; }
        public string? ParentSetCode { get; set; }
        public bool Digital { get; set; }
        public bool NonfoilOnly { get; set; }
        public bool FoilOnly { get; set; }
        public string? IconSvgUri { get; set; }
        public DateTime? ReleaseDate { get; set; }
    }

    private class CardFaceRow
    {
        public Guid CardId { get; set; }
        public int FaceIndex { get; set; }
        public string Name { get; set; } = "";
        public string? ManaCost { get; set; }
        public string? TypeLine { get; set; }
        public string? OracleText { get; set; }
        public string? Power { get; set; }
        public string? Toughness { get; set; }
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
    }

    private class CardFaceRowWithId
    {
        public int Id { get; set; }
        public int FaceIndex { get; set; }
    }

    private class CardFaceRowWithId2
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
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

    private class CardFaceRowWithId2Extended : CardFaceRowWithId2
    {
        public Guid CardId { get; set; }
    }

    private class RelatedCardRow
    {
        public Guid CardId { get; set; }
        public int PartIndex { get; set; }
        public Guid RelatedCardId { get; set; }
        public string? Component { get; set; }
        public string Name { get; set; } = "";
        public string? TypeLine { get; set; }
        public string? Uri { get; set; }
    }

    private class ImageUrisRow
    {
        public string? Small { get; set; }
        public string? Normal { get; set; }
        public string? Large { get; set; }
        public string? Png { get; set; }
    }

    private class ImageUrisRowWithId
    {
        public Guid CardId { get; set; }
        public string? Small { get; set; }
        public string? Normal { get; set; }
        public string? Large { get; set; }
        public string? Png { get; set; }
    }

    private class CardFaceImageUrisRow
    {
        public int FaceIndex { get; set; }
        public string? Small { get; set; }
        public string? Normal { get; set; }
        public string? Large { get; set; }
        public string? Png { get; set; }
    }
}
