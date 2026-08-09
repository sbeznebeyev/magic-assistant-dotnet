-- Migration 002: Add OracleId column to Cards table
-- Date: 2026-07-28
-- Description: Add OracleId (uniqueidentifier) for storing Scryfall oracle identity ID.
--              This value is consistent across reprinted card editions and unique among
--              different cards with the same name (tokens, Unstable variants, etc).

ALTER TABLE Cards ADD COLUMN OracleId uniqueidentifier;
