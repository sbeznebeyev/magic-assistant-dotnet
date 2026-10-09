using Dapper;
using Microsoft.Data.Sqlite;

namespace MagicAssistant.Data.Db;

/// <summary>
/// Creates all tables and indexes for the MagicAssistant database.
/// Call once at application startup — idempotent (CREATE TABLE IF NOT EXISTS).
///
/// Schema:
///   Cards        — knowledge base cards (PK: Id)
///   CardSet      — set reference data (PK: Id)
///   Collection   — navigation nodes (PK: Id autoincrement, FK: ParentId → Collection). NodeType: 0=Folder, 1=Collection, 2=Deck, 3=Sideboard.
///   Deck         — physical cards inside collections/decks (PK: CollectionId+CardId, FK: CardId → Cards, FK: CollectionId → Collection).
///   CardsFts     — FTS5 virtual table for full-text search on Name, Type, OracleText, Artist
/// </summary>
public static class InitializeDb
{
    // Ensure Dapper's Guid type handler is registered before any DB operations.
    // SQLite stores GUIDs as TEXT; this handler converts them back to Guid automatically.
    // We must actually instantiate the type (not just typeof) to trigger its static constructor.
    static InitializeDb() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(SqliteGuidHandler).TypeHandle);

    /// <summary>
    /// Creates the database file (if needed) and all tables + indexes.
    /// </summary>
    public static async Task EnsureCreatedAsync(string dbPath, CancellationToken ct = default)
    {
        var directory = System.IO.Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);

        await CreateCardsTable(conn);
        await CreateFtsTable(conn);
        await CreateSetTable(conn);
        await CreateCollectionTable(conn);
        await CreateDeckTable(conn);
        await CreateCardFacesTable(conn);
        await CreateCardAllPartsTable(conn);
        await CreateCardsImageUrisTable(conn);
        await CreateCardFacesImageUrisTable(conn);
        await CreateUserSettingsTable(conn);
        await CreateIndexes(conn);
    }

    private static async Task CreateCardsTable(SqliteConnection conn)
    {
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Cards (
                Id              uniqueidentifier PRIMARY KEY,
                OracleId        uniqueidentifier,
                Name            TEXT NOT NULL,
                ManaCost        TEXT,
                Layout          INTEGER DEFAULT 0,
                Type            TEXT,
                Power           TEXT,
                PowerNumber     INTEGER,
                Toughness       TEXT,
                ToughnessNumber INTEGER,
                SetCode         TEXT NOT NULL,
                Rarity          TEXT,
                OracleText      TEXT,
                Rulings         TEXT,
                Artist          TEXT,
                Language        TEXT,
                CollectorNumber TEXT,
                PrintedName     TEXT,
                PrintedText     TEXT,
                PrintedTypeLine TEXT,
                CmcNumber       INTEGER,
                CmcText         TEXT,
                ColorType       TEXT,
                LegalityFormats TEXT,
                Colors          TEXT,
                ColorIdentity   TEXT,
                GameChanger     INTEGER,
                Legal           TEXT,
                Usd             TEXT,
                UsdFoil         TEXT,
                ColorBits       INTEGER,
                IdentityBits    INTEGER
            )");
    }

    private static async Task CreateSetTable(SqliteConnection conn)
    {
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS CardSet (
                Id             uniqueidentifier PRIMARY KEY,
                Name           TEXT NOT NULL,
                SetCode        TEXT,
                Uri            TEXT,
                ScryfallUri    TEXT,
                SearchUri      TEXT,
                SetType        TEXT DEFAULT '?',
                CardCount      INTEGER DEFAULT 0,
                ParentSetCode  TEXT,
                Digital        INTEGER NOT NULL DEFAULT 0,
                NonfoilOnly    INTEGER NOT NULL DEFAULT 0,
                FoilOnly       INTEGER NOT NULL DEFAULT 0,
                IconSvgUri     TEXT,
                ReleaseDate    DateTime
            )");
    }

    private static async Task CreateCollectionTable(SqliteConnection conn)
    {
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Collection (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                Name       TEXT NOT NULL,
                ParentId   INTEGER,
                NodeType   INTEGER NOT NULL DEFAULT 0,
                Locked     BOOLEAN NOT NULL DEFAULT 0,
                FOREIGN KEY (ParentId) REFERENCES Collection(Id) ON DELETE CASCADE
            )");
    }

    private static async Task CreateDeckTable(SqliteConnection conn)
    {
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Deck (
                CollectionId INTEGER NOT NULL,
                CardId       uniqueidentifier NOT NULL,
                Count        INTEGER NOT NULL DEFAULT 1,
                IsOwn        BOOLEAN NOT NULL DEFAULT 1,
                Comment      TEXT,
                DateAdded    DateTime,
                ForTradeCount INTEGER,
                MyPrice       REAL DEFAULT 0,
                Grade         INTEGER,
                PRIMARY KEY (CollectionId, CardId),
                FOREIGN KEY (CollectionId) REFERENCES Collection(Id) ON DELETE CASCADE,
                FOREIGN KEY (CardId) REFERENCES Cards(Id) ON DELETE CASCADE
            )");
    }

    private static async Task CreateCardFacesTable(SqliteConnection conn)
    {
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS CardFaces (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                CardId          uniqueidentifier NOT NULL,
                FaceIndex       INTEGER NOT NULL DEFAULT 0,
                Name            TEXT NOT NULL,
                ManaCost        TEXT,
                TypeLine        TEXT,
                OracleText      TEXT,
                Power           TEXT,
                PowerNumber     INTEGER,
                Toughness       TEXT,
                ToughnessNumber INTEGER,
                CmcNumber       INTEGER,
                CmcText         TEXT,
                Colors          TEXT,
                ColorIndicator  TEXT,
                Loyalty         TEXT,
                Artist          TEXT,
                IllustrationId  uniqueidentifier,
                FlavorText      TEXT,
                PrintedName     TEXT,
                PrintedText     TEXT,
                PrintedTypeLine TEXT,
                ColorBits       INTEGER NOT NULL DEFAULT 0,
                IdentityBits    INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (CardId) REFERENCES Cards(Id) ON DELETE CASCADE
            )");
    }

    private static async Task CreateCardAllPartsTable(SqliteConnection conn)
    {
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS CardAllParts (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                CardId          uniqueidentifier NOT NULL,
                PartIndex       INTEGER NOT NULL DEFAULT 0,
                RelatedCardId   uniqueidentifier NOT NULL,
                Component       TEXT,
                Name            TEXT NOT NULL,
                TypeLine        TEXT,
                Uri             TEXT,
                FOREIGN KEY (CardId) REFERENCES Cards(Id) ON DELETE CASCADE
            )");
    }

    private static async Task CreateCardsImageUrisTable(SqliteConnection conn)
    {
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS CardsImageUris (
                CardId          uniqueidentifier PRIMARY KEY,
                Small           TEXT,
                Normal          TEXT,
                Large           TEXT,
                Png             TEXT,
                FOREIGN KEY (CardId) REFERENCES Cards(Id) ON DELETE CASCADE
            )");
    }

    private static async Task CreateCardFacesImageUrisTable(SqliteConnection conn)
    {
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS CardFacesImageUris (
                FaceId          INTEGER PRIMARY KEY,
                Small           TEXT,
                Normal          TEXT,
                Large           TEXT,
                Png             TEXT,
                FOREIGN KEY (FaceId) REFERENCES CardFaces(Id) ON DELETE CASCADE
            )");
    }

    private static async Task CreateUserSettingsTable(SqliteConnection conn)
    {
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS UserSettings (
                Key   TEXT PRIMARY KEY,
                Value TEXT
            )");
    }

    private static async Task CreateIndexes(SqliteConnection conn)
    {
        await conn.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS idx_cards_name ON Cards(Name)");
        await conn.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS idx_cards_set ON Cards(SetCode)");
        await conn.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS idx_cards_rarity ON Cards(Rarity)");
        await conn.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS idx_collection_parent ON Collection(ParentId)");
        await conn.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS idx_deck_cardid ON Deck(CardId)");
        await conn.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS idx_cardfaces_cardid ON CardFaces(CardId)");
        await conn.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS idx_cardallparts_cardid ON CardAllParts(CardId)");
        await conn.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS idx_cards_colorbits ON Cards(ColorBits)");
        await conn.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS idx_cards_identitybits ON Cards(IdentityBits)");
        await conn.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS idx_cards_powernumber ON Cards(PowerNumber)");
        await conn.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS idx_cards_toughnessnumber ON Cards(ToughnessNumber)");
        await conn.ExecuteAsync(@"CREATE INDEX IF NOT EXISTS idx_cards_cmcnumber ON Cards(CmcNumber)");
    }

    /// <summary>
    /// Creates an FTS5 virtual table for full-text search over card Name, Type, OracleText, Artist.
    /// Standard (non-external) mode — data is stored in the FTS5 index itself.
    /// Triggers keep the index in sync on INSERT/UPDATE/DELETE from Cards.
    /// </summary>
    private static async Task CreateFtsTable(SqliteConnection conn)
    {
        // Drop existing triggers first (they can't be created IF NOT EXISTS in SQLite)
        await conn.ExecuteAsync(@"DROP TRIGGER IF EXISTS cards_fts_ai");
        await conn.ExecuteAsync(@"DROP TRIGGER IF EXISTS cards_fts_ad");
        await conn.ExecuteAsync(@"DROP TRIGGER IF EXISTS cards_fts_ud");

        // FTS5 virtual table — stores searchable data directly.
        // CardId column stores the Scryfall card ID (TEXT) for joining back to Cards.
        // rowid is auto-assigned INTEGER by FTS5 internally.
        await conn.ExecuteAsync(@"
            CREATE VIRTUAL TABLE IF NOT EXISTS CardsFts USING fts5(
                CardId UNINDEXED, Name, Type, OracleText, Artist
            )");

        // AFTER INSERT — add new card to FTS index
        await conn.ExecuteAsync(@"
            CREATE TRIGGER cards_fts_ai AFTER INSERT ON Cards
            BEGIN
                INSERT INTO CardsFts(CardId, Name, Type, OracleText, Artist)
                VALUES (new.Id, new.Name, new.Type, new.OracleText, new.Artist);
            END");

        // AFTER DELETE — remove from FTS index
        await conn.ExecuteAsync(@"
            CREATE TRIGGER cards_fts_ad AFTER DELETE ON Cards
            BEGIN
                DELETE FROM CardsFts WHERE CardId = old.Id;
            END");

        // AFTER UPDATE — update FTS index (delete + insert)
        await conn.ExecuteAsync(@"
            CREATE TRIGGER cards_fts_ud AFTER UPDATE ON Cards
            BEGIN
                DELETE FROM CardsFts WHERE CardId = old.Id;
                INSERT INTO CardsFts(CardId, Name, Type, OracleText, Artist)
                VALUES (new.Id, new.Name, new.Type, new.OracleText, new.Artist);
            END");
    }
}
