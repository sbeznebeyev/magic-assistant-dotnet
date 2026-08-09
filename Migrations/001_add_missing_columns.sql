-- Migration 001: Add all missing columns to Cards table for existing databases.
-- Run manually against existing cards.db:
--   sqlite3 /path/to/cards.db < Migrations/001_add_missing_columns.sql
--
-- Columns added in order they appear in the schema to match InitializeDb.cs.

-- Usd / UsdFoil — price info from Scryfall (was missing from CREATE TABLE)
ALTER TABLE Cards ADD COLUMN Usd TEXT;
ALTER TABLE Cards ADD COLUMN UsdFoil TEXT;

-- Colors — card colors from Scryfall (W, U, B, R, G), comma-separated
ALTER TABLE Cards ADD COLUMN Colors TEXT;

-- ColorIdentity — color identity from Scryfall, comma-separated
ALTER TABLE Cards ADD COLUMN ColorIdentity TEXT;
