-- Migration 003: Add PrintedName, PrintedText, PrintedTypeLine columns to Cards table
-- Date: 2026-07-28
-- Description: Store printed variants of card name, text, and type line from Scryfall.
--              These fields may differ from Name/OracleText/Type for cards with alternate names or rules text.

ALTER TABLE Cards ADD COLUMN PrintedName TEXT;
ALTER TABLE Cards ADD COLUMN PrintedText TEXT;
ALTER TABLE Cards ADD COLUMN PrintedTypeLine TEXT;
