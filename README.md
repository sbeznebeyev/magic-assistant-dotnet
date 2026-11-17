# MagicAssistant

> **Disclaimer.** MagicAssistant is an **independent development**, inspired by — but not affiliated with, endorsed by, or derived from — the original Java-based [Magic Assistant](https://sourceforge.net/projects/mtgbrowser/).
>
> The project was built as a **hobby**. There are no concrete plans for its future development, no roadmap, and no commitments. New features may appear over time at the author's sole discretion — consider what ships a bonus, not a promise.
>
> Magic: The Gathering card data and images are the property of **Wizards of the Coast**, served via [Scryfall](https://scryfall.com). This project is not affiliated with or endorsed by Wizards of the Coast or Scryfall.

A modern **Magic: The Gathering** deck and collection manager, built as a spiritual successor to the original Java-based [Magic Assistant](https://sourceforge.net/projects/mtgbrowser/).

---

## What It Is

MagicAssistant is a full-featured desktop-class application for managing MTG card collections and decks. Sync your knowledge base from Scryfall, import/export decks in Magic Assistant XML format, analyze mana curves, color distribution, creature types, keyword abilities, and format legality — all through a responsive Blazor Server UI.

It is a **single-user** application: there are no user accounts or profiles, and no way to keep separate collections and decks for different people. One installation, one knowledge base, one collection — the way a personal organizer should be.

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
- 📜 **Legacy Import** — bring in old Magic Assistant XML files (see [Legacy Import](#-legacy-import))
- 🗂️ **User settings** — store settings in the DB

---

## Requirements

| For | You need |
|-----|----------|
| **Running a release build** | [.NET 10 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) — release builds are framework-dependent, the runtime is the only install |
| **Building from source** | [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) |

No database server, no native dependencies — SQLite is embedded.

---

## Building from Source

```bash
git clone <repo-url> && cd MagicAssistant
dotnet build MagicAssistant.slnx          # build everything
dotnet test MagicAssistant.Tests          # run the test suite
dotnet publish MagicAssistant.App -c Release -o publish   # produce a deployable folder
```

`dotnet publish` emits a self-contained *folder* (not a single file): the `MagicAssistant.App` executable, `MagicAssistant.App.dll`, all dependency DLLs, `wwwroot/`, and `appsettings.json`. Copy that folder anywhere you like — it is the whole application.

---

## Running

The app serves a web UI, but you never open a browser at a dev server — start the executable and point a browser at it.

### Windows

```bat
MagicAssistant.App.exe
```

or, equivalently:

```bat
dotnet MagicAssistant.App.dll
```

### Linux

```bash
dotnet MagicAssistant.App.dll
```

The executable is produced for the publishing platform, so on Linux run the DLL through `dotnet`.

### First launch

Open `http://localhost:43210`, hit **Sync** in the header, and let the knowledge base download from Scryfall. Everything else — collections, decks, analysis — works offline from that point on; only card images and further syncs need the network.

---

## Configuration

All configuration lives in `appsettings.json` next to the executable. Every key can also be set via environment variable or a command-line argument (`--Key Value`); later sources win.

### Port

Out of the box the app listens on **`http://localhost:43210`** — set by the `Urls` key in `appsettings.json`:

```json
{
  "Urls": "http://localhost:43210"
}
```

Change the value and restart. Two overrides need no file edit at all:

```bash
./MagicAssistant.App.exe --urls http://localhost:8080        # CLI argument
ASPNETCORE_URLS=http://localhost:8080 dotnet MagicAssistant.App.dll   # environment variable
```

To reach the app from other devices on your network, bind to all interfaces:

```json
{ "Urls": "http://0.0.0.0:43210" }
```

### DataPath

All persistent state lives under one data directory. The `DataPath` key (default `./data`) decides where:

```json
{
  "DataPath": "./data"
}
```

- A **relative** path is resolved against the application folder (where the executable lives).
- An **absolute** path is used as-is — point it at another disk if your collection grows large:

```json
{ "DataPath": "D:/mtg-data" }
```

Inside the data directory the app creates and owns:

```
<dataPath>/
├── db/cards.db        # the entire database: KB, collections, decks, settings
├── images/            # cached card art (downloaded from Scryfall on demand)
└── set_icons/         # cached set symbols
```

Back up or migrate an installation by copying `db/cards.db` — the cache folders regenerate themselves.

---

## 📜 Legacy Import

The original Java Magic Assistant stored decks as XML with **Gatherer IDs** and **full set names** (`"Core Set 2019"` instead of the modern three-letter set code). The Legacy Import wizard understands exactly those files.

**How it works:** pick a deck/collection node in the navigator, click **Legacy Import** in the header, select your `.xml` file. The wizard parses the file, resolves every card against your knowledge base, and shows anything it could not match one-to-one — you pick the right card once, and it remembers the decision for identical rows. Import merges into the selected node; nothing outside it is touched.

Notes:

- The header action is **gold** when the current selection can receive cards, **gray** when the Knowledge Base or a locked node is selected — the tooltip tells you to select an unlocked target.
- Cards are matched against the synced knowledge base, so sync before importing a legacy file that references recent sets.
- Files are capped at 25 MB — far beyond any real deck list.

---

## Data

Everything lives in a single SQLite file: `{dataPath}/db/cards.db`. No external database server required.

---

*Built by an MTG player, for MTG players.*
