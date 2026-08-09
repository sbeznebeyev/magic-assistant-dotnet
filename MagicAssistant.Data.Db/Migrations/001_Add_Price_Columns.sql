-- Migration 001: Add Usd and UsdFoil columns to Cards table
-- Date: 2026-07-28
-- Description: Add price columns for storing Scryfall USD prices (usd, usd_foil)

-- Add Usd column if not exists
ALTER TABLE Cards ADD COLUMN Usd TEXT;

-- Add UsdFoil column if not exists
ALTER TABLE Cards ADD COLUMN UsdFoil TEXT;
