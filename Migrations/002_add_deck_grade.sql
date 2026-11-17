-- Migration 002: Add Grade column to the Deck table (physical card condition grade).
-- Run manually against existing cards.db:
--   sqlite3 /path/to/cards.db < Migrations/002_add_deck_grade.sql
--
-- Stores CardGrade enum values as integers (MagicAssistant.Data.Model.CardGrade):
--   0 = M   (GEM MINT / MINT)
--   1 = NM  (NEAR MINT)
--   2 = SP  (SLIGHTLY PLAYED)
--   3 = MP  (MODERATELY PLAYED)
--   4 = HP  (HEAVILY PLAYED)
--   5 = DMG (DAMAGED)
-- NULL = not graded.

ALTER TABLE Deck ADD COLUMN Grade INTEGER;
