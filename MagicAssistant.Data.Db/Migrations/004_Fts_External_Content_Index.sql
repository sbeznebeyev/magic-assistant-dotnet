-- Migration 004: FTS5 index → external content, diacritics folding, prefix search
-- Date: 2026-11-22
-- Description: Rebuilds CardsFts from the legacy shape (CardId UNINDEXED + duplicated text)
--              to external-content mode (content='Cards', rowid join — no text duplication,
--              O(log n) deletes), enables remove_diacritics=2 ("jose" finds "José") and
--              prefix='2 3 4' (backs the app's bare-word fly* prefix search).
--              The app REFUSES to start against the legacy index until this file is applied.
--
-- Usage:  sqlite3 cards.db ".read Migrations/004_Fts_External_Content_Index.sql"

DROP TRIGGER IF EXISTS cards_fts_ai;
DROP TRIGGER IF EXISTS cards_fts_ad;
DROP TRIGGER IF EXISTS cards_fts_ud;

DROP TABLE IF EXISTS CardsFts;

CREATE VIRTUAL TABLE CardsFts USING fts5(
    Name, Type, OracleText, Artist,
    content='Cards', content_rowid='rowid',
    tokenize='unicode61 remove_diacritics 2',
    prefix='2 3 4'
);

-- Fill the index from Cards (works on a populated DB; harmless on an empty one).
INSERT INTO CardsFts(CardsFts) VALUES('rebuild');

-- AFTER INSERT — mirror the new row into the index by rowid
CREATE TRIGGER cards_fts_ai AFTER INSERT ON Cards
BEGIN
    INSERT INTO CardsFts(rowid, Name, Type, OracleText, Artist)
    VALUES (new.rowid, new.Name, new.Type, new.OracleText, new.Artist);
END;

-- AFTER DELETE — the 'delete' command keeps external content consistent
CREATE TRIGGER cards_fts_ad AFTER DELETE ON Cards
BEGIN
    INSERT INTO CardsFts(CardsFts, rowid, Name, Type, OracleText, Artist)
    VALUES ('delete', old.rowid, old.Name, old.Type, old.OracleText, old.Artist);
END;

-- AFTER UPDATE — delete old entry, insert new one (both by rowid)
CREATE TRIGGER cards_fts_ud AFTER UPDATE ON Cards
BEGIN
    INSERT INTO CardsFts(CardsFts, rowid, Name, Type, OracleText, Artist)
    VALUES ('delete', old.rowid, old.Name, old.Type, old.OracleText, old.Artist);
    INSERT INTO CardsFts(rowid, Name, Type, OracleText, Artist)
    VALUES (new.rowid, new.Name, new.Type, new.OracleText, new.Artist);
END;
