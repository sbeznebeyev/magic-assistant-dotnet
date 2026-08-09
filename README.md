# MagicAssistant

A modern **Magic: The Gathering** deck and collection manager, built as a spiritual successor to the original Java-based [Magic Assistant](https://github.com/reflexit/magic).

---

## What It Is

MagicAssistant is a full-featured desktop-class application for managing MTG card collections and decks. Sync your knowledge base from Scryfall, import/export decks in Magic Assistant XML format, analyze mana curves, color distribution, creature types, keyword abilities, and format legality — all through a responsive Blazor Server UI.

---

## Architecture at a Glance

| Layer | Project | Role |
|-------|---------|------|
| **UI** | `MagicAssistant.App` | ASP.NET Core Blazor Server — single-page navigation |
| **Domain** | `MagicAssistant.Core` | domain layer |
| **Data Contracts** | `MagicAssistant.Data` | model + contracts |
| **Data Impl** | `MagicAssistant.Data.Db` | SQLite + Dapper — single `cards.db` file, FTS5 full-text search |
| **I/O** | `MagicAssistant.Data.IO` | XML import/export |
| **HTTP Contracts** | `MagicAssistant.Http` | `IScrayfallClient` interface + DTOs |
| **HTTP Impl** | `MagicAssistant.Http.Scryfall` | Scryfall API client |

**Stack:** .NET 10 · Blazor Server (SignalR) · SQLite · Dapper · FTS5

---

## Key Features

- 🔄 **Knowledge Base Sync** — sync from Scryfall
- 📚 **Deck & Collection Management** — create, rename, delete folders/collections/decks with sideboards
- 🔍 **Card Search** — FTS5 full-text search, AST-based expression filters (colors, types, rarity, regex)
- 📊 **Deck Analysis** — mana curve, color distribution, creature subtypes, keyword abilities, format legality
- 📥 **XML Import/Export** — export/import collection and decks
- 🗂️ **User settings** — store settings in the DB

---

## Data

Everything lives in a single SQLite file: `{dataPath}/db/cards.db`. No external database server required.

---

*Built by a MTG player, for MTG players.*
