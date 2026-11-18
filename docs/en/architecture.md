# MagicAssistant — ASP.NET Core Application

*ASP.NET Core Blazor Server · .NET 10 · Blazor Web App (MapRazorComponents + InteractiveServerRenderMode, prerender: false)*

## Vertical Responsibility Architecture

The application is built on the **vertical slice** principle — each business domain is encapsulated in an independent service that cuts through all layers, from UI down to the Data/Http contracts.

> **Note:** business logic is distributed across vertical-responsibility domain services:
> `IDeckManagementService`, `IDeckCardService`, `ICardResolveService`,
> `IKnowledgeBaseSyncService`, `IDeckImportExportService`, `ICardSearchService`, `IDeckAnalysisService`.

### Vertical Slice Structure

```
MagicAssistant.Core/Services/
├── DeckManagement/           CRUD for decks and folders (create, delete, rename, navigation)
│   ├── IDeckManagementService.cs
│   └── DeckManagementService.cs
│
├── DeckCard/                 Cards inside a deck (add/remove/move/split/update)
│   ├── IDeckCardService.cs
│   └── DeckCardService.cs
│
├── CardResolve/              Resolve stubs → KB cards, materialize borrowed cards
│   ├── ICardResolveService.cs
│   └── CardResolveService.cs
│
├── KnowledgeBaseSync/        Knowledge base sync from Scryfall
│   ├── IKnowledgeBaseSyncService.cs
│   └── KnowledgeBaseSyncService.cs
│
├── DeckImportExport/         Deck import/export (native JSON + legacy XML)
│   ├── IDeckImportExportService.cs
│   └── DeckImportExportService.cs
│
├── CardSearch/               FTS5 search, filtering, printings, instances
│   ├── ICardSearchService.cs
│   └── CardSearchService.cs
│
└── DeckAnalysis/             Deck analysis (mana curve, colors, legality) + collector progress
    ├── IDeckAnalysisService.cs
    └── DeckAnalysisService.cs
```

### Dependencies Between Slices

```
DeckCardService        → CardResolveService (for MaterializeFromIdsAsync, ExpandGroups)
KnowledgeBaseSync      → (depends on no other slice, only Data/Http contracts)
DeckManagement         → (depends on no other slice, uses CQRS commands/queries)
DeckImportExport       → (depends on no other slice; ICollectionStore + JsonImport/JsonExport (native) and XmlImport (legacy))
CardSearch             → (depends on no other slice, only IDbCardStore + ICollectionStore)
DeckAnalysis           → IAnalysisService (existing deck analysis service)
```

### Data Flow

```
┌─────────────────────────────────────────────────────────────┐
│              Blazor Components (UI)                         │
│  DecksNavigator     → IDeckManagementService               │
│  CardsListControl   → ICardSearchService + IDeckCardService │
│           (KB mode and deck mode via CurrentCollectionId)   │
│  CardDescriptionView → ICardSearchService                  │
│  PrintingsView     → ICardSearchService                    │
│  InstancesView     → ICardStore<MyCard>                    │
│  Analysis pages    → IAnalysisService + ICollectionStore   │
│  CollectorView     → IDeckAnalysisService                  │
│  SyncKnowledgeBase → IKnowledgeBaseSyncService             │
│  DeckImportWizard  → IDeckManagement + ImportExport        │
│  DeckExportWizard  → IDeckManagement + ImportExport        │
├─────────────────────────────────────────────────────────────┤
│              Core Layer (Vertical Slices)                   │
│  DeckManagement │ DeckCard │ CardResolve                   │
│  KnowledgeBaseSync │ DeckImportExport │ CardSearch │ DeckAnalysis│
├─────────────────────────────────────────────────────────────┤
│              Data / Http Layer (Contracts)                  │
│  IDbCardStore │ ICollectionStore │ IScrayfallClient        │
└─────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
MagicAssistant.slnx (net10.0)
├── MagicAssistant.App              ← ASP.NET Core, DI, Blazor Server UI (net10.0)
│   ├── App.razor / Routes.razor    ← root: one shared InteractiveServerRenderMode(prerender: false)
│   ├── Program.cs                  ← DI, SignalR circuit, `InitializeDb.EnsureCreatedAsync`
│   ├── Controllers/                ← ImagesController: `GET /api/images/{set}/{lang}/front|back/{uid}.png` + set icons (`/api/images/sets/{setCode}.svg`)
│   ├── Services/                   ← CardImageLoader (lazy image loading), KbRefreshService
│   ├── Shared/                     ← MainLayout, TabBar, ContextStrip, FilterViewModel, FilteredCardSetsCollection, LastActionsQueue/LastActionItem (+ CardOperationRecord), LockableCollection, FilterInputSanitizer, MultiSelectViewModel/Item
│   └── Components/                 ← Pages/, Views/, Dialogs/, Wizards/, Analysis/, Controls/, Shared/
│       └── Blazor components call the vertical services from MagicAssistant.Core directly via DI
│
├── MagicAssistant.Core             ← Business logic (net10.0)
│   ├── Services/                   ← Vertical slices (domain services)
│   │   ├── DeckManagement/         ← CRUD for decks and folders
│   │   ├── DeckCard/               ← Cards inside a deck
│   │   ├── CardResolve/            ← Resolve + Materialize
│   │   ├── KnowledgeBaseSync/      ← Scryfall sync
│   │   ├── DeckImportExport/       ← XML import/export
│   │   ├── CardSearch/             ← FTS5 search + filter
│   │   └── DeckAnalysis/           ← Mana curve, colors, legality
│   ├── Commands/                   ← CQRS: mutating operations
│   │   ├── CreateNodeCommand.cs      ← unified node creation (Folder/Collection/Deck; Deck → + sideboard)
│   │   ├── CreateDeckCommand.cs      ← create deck (NodeType=2) + sideboard
│   │   ├── CreateCollectionCommand.cs← create collection (NodeType=1)
│   │   ├── CreateFolderCommand.cs    ← create folder (NodeType=0)
│   │   ├── DeleteNodeCommand.cs      ← delete node + sideboard children
│   │   ├── RenameNodeCommand.cs      ← rename node + sideboard children
│   │   ├── LockNodeCommand.cs        ← lock node (Locked=1); for a deck the state propagates to the sideboard
│   │   ├── UnlockNodeCommand.cs      ← unlock node (Locked=0); for a deck it clears the sideboard lock
│   │   ├── AddCardsToDeckCommand.cs  ← add cards to a deck
│   │   ├── RemoveCardsFromDeckCommand.cs ← pure delegate into `ICollectionStore.RemoveCardsAsync` (1 SQL query): non-empty `CardIds` = delete ALL copies of the cards, empty = wipe every card in the node
│   │   ├── MoveCardsCommand.cs       ← move cards between collections (merge semantics)
│   │   ├── UpdateCardCommand.cs      ← update card properties
│   │   ├── SyncKBCommand.cs          ← delegates to KnowledgeBaseSyncService
│   │   ├── SyncCardSetsCommand.cs    ← sync set metadata
│   │   ├── ImportResolvedCardsCommand.cs ← atomic commit of a fully resolved import (new: node+sideboard+cards in one transaction; existing: merge)
│   │   └── SaveUserSettingCommand.cs ← save user settings
│   ├── Queries/                    ← CQRS: data reads
│   │   ├── GetScryfallAvailableSetsQuery.cs ← available sets from Scryfall
│   │   ├── GetKBAvailableSetsQuery.cs       ← sets present in the knowledge base
│   │   ├── GetDeckCardsQuery.cs             ← deck cards with a SQL JOIN to Cards
│   │   ├── SearchCardsQuery.cs              ← card search in the database
│   │   ├── GetSyncProgressQuery.cs          ← sync progress (SyncSetStateRegistry)
│   │   ├── GetNodesTreeQuery.cs             ← the whole navigation tree in one query (dictionary keys are root nodes; deck nodes carry a filled Sideboard)
│   │   ├── GetNodeByIdQuery.cs              ← a single node by ID
│   │   ├── GetNodesBranchQuery.cs           ← subtree rooted at a given node (recursive SQL)
│   │   ├── GetCardByIdQuery.cs              ← card by ID
│   │   ├── GetCardPrintingsQuery.cs         ← printings of a card
│   │   ├── GetCardLanguagesQuery.cs         ← language versions of a card
│   │   ├── GetCardInstancesQuery.cs         ← physical instances of a card
│   │   ├── GetCardSetQuery.cs               ← set metadata
│   │   ├── GetDeckAnalysisQuery.cs          ← deck analysis
│   │   ├── GetSetSyncStatusByCodesQuery.cs  ← sync status for set codes in one batch SQL — pre-check of the native JSON import (the file carries codes; a code with CardCount == 0 requires a sync)
│   │   ├── GetUserSettingQuery.cs           ← read user settings
│   │   ├── GetSetSyncStatusQuery.cs         ← sync status for full set names in one SQL (in CardSet? how many cards?)
│   │   ├── ResolveLegacyCardsQuery.cs       ← batch resolve of legacy cards: strip the face suffix, name→code, (name,set)→UUID across all name fields (Name/PrintedName + CardFaces); {Resolved, Unresolved}
│   │   ├── ValidateCardIdsQuery.cs          ← Known/Unknown against the KB — pre-check of the modern import
│   │   └── NodesHelper.cs                   ← Collection → NavigatorNode* mapping (tree building)
│   ├── Expressions/                ← Filter and expression system
│   │   ├── Expr.cs                 ← Base class: True, False, Empty, And, Or, Not
│   │   ├── BinaryExpr.cs           ← AND, OR, EQUALS, MATCHES, >=, <=, == + factories
│   │   ├── CardFieldExpr.cs        ← MagicCardField → ICardField wrapper
│   │   ├── CardMatcher.cs          ← Matching a card field against a TextValue (regex, wordBoundary, caseSensitive)
│   │   ├── Value.cs / TextValue.cs ← Constant values (regex, wordBoundary)
│   │   ├── NotExpr.cs              ← Unary NOT
│   │   ├── SearchStringTokenizer.cs← Search-string parser (!, quotes, \regex:)
│   │   └── Abilities.cs            ← MTG abilities with regex patterns
│   ├── MagicCardFilter.cs          ← Main filter class: AST, sorting, grouping
│   ├── ScryfallMapper.cs           ← DTO → Card mapping
│   ├── ImageCacheService.cs        ← Card image download + caching
│   └── SyncSetState.cs             ← Thread-safe sync progress tracker
│
├── MagicAssistant.Data             ← Local data access contracts + Model (net10.0)
│   ├── Caches/                     ← CardSetsCache, AllCardSetsCache (singleton set-metadata caches, MemoryCache subclasses)
│   ├── Contracts/                  ← store interfaces + card hierarchy (ICardSimple → ICard → ICardModifiable → IMyCard)
│   │   ├── ICardStore.cs           ← ICardStore<T>, IDbCardStore, ICollectionStore
│   │   ├── IUserSettingsStore.cs   ← UserSettings key/value
│   │   └── ISearchableProperty.cs  ← group-filtering contract
│   ├── Extensions/                 ← extension methods (CardType/Format/Rarity/MagicCardField)
│   └── Model/                      ← domain models
│       ├── Enums/                  ← RarityEnum, CardTypeEnum, LayoutEnum, LanguageCodeEnum
│       ├── Legality/               ← Format, LegalityMap
│       ├── Card.cs                 ← Base card from the DB (implements ICard)
│       ├── MyCard.cs               ← Physical card instance (reference to the base Card)
│       ├── CardFace.cs             ← Face of a multifaced card (CardFaces table)
│       ├── RelatedCard.cs          ← Part of a composite card (CardAllParts table)
│       ├── CardGrade.cs            ← Card condition (M/NM/SP/MP/HP/DMG)
│       ├── MagicCardField.cs       ← Enum + extension methods for fields
│       ├── ColorBitFlags.cs        ← Bitwise color flags (C/W/U/B/R/G + MULTI/MONO/HYBRID)
│       ├── ColorFilter.cs          ← Color filter: SQL masks, DFC support
│       ├── CardSet.cs              ← Set/edition (STH, M19)
│       ├── CardSetType.cs / CardSetTypes.cs ← Set type + static list
│       ├── Collection.cs           ← Collection tree node (deck/sideboard/collection/folder)
│       ├── CardGroup.cs            ← Card group
│       ├── ColorTypes.cs           ← Static color class
│       ├── CardListFilter.cs       ← Server-side pagination filter (CardListResult)
│       ├── ImageUris.cs            ← Scryfall image URIs of a card
│       ├── SetSyncStatus.cs        ← Set sync status (in CardSet? how many cards?)
│       ├── SetProgressDetails.cs / CollectorProgressRaw.cs ← Collector progress data
│       ├── NavigatorNodes/         ← NavigatorNodeBase/Folder/Collection/Deck/Sideboard + NodeType
│       └── SearchableProperties/   ← ISearchableProperty: ColorTypesSearch, Rarity, Locations, Editions
│
├── MagicAssistant.Data.Db          ← Infrastructure layer: SQLite + Dapper (net10.0)
│   ├── InitializeDb.cs             ← Static method creating all tables and indexes
│   ├── SqliteCardStore.cs          ← IDbCardStore implementation (card knowledge base)
│   ├── SqliteCollectionStore.cs    ← ICollectionStore + ICardStore<MyCard> implementation
│   │                                   ← JOIN queries against Cards to pull full information
│   ├── SqliteUserSettingsStore.cs  ← IUserSettingsStore implementation (UserSettings table)
│   ├── SqliteGuidHandler.cs        ← Dapper Guid handler (self-registering)
│   └── Migrations/README.md        ← Manual SQL migrations (scripts: root-level Migrations/)
│
├── MagicAssistant.Data.IO          ← Export/import formats (net10.0)
│   ├── CardCollectionImportResult.cs ← Shared parse result (XML and JSON)
│   ├── json/                       ← Native Magic Assistant JSON format (v1)
│   │   ├── JsonImport.cs           ← JSON parsing → List<ICard> (reference core id/name/set/count required)
│   │   └── JsonExport.cs           ← MyCard[] → JSON (omit-by-default, pretty)
│   └── xml/                        ← Legacy Magic Assistant XML format
│       └── XmlImport.cs            ← Legacy XML parsing (Gatherer ID + full set names) → List<ICard>
│
├── MagicAssistant.Http             ← External data access contracts + DTOs (net10.0)
│   ├── Contracts/                  ← IScrayfallClient
│   ├── Dtos/                       ← ScryfallCardDto, ScryfallSetDto, etc.
│   └── JsonConverters.cs           ← Custom JSON converters
│
├── MagicAssistant.Http.Scryfall    ← Scryfall API implementation of the contracts (net10.0)
│   └── ScryfallClient.cs           ← HTTP client for the Scryfall API
│
├── MagicAssistant.Tests            ← Tests (NUnit): Core, Data.Db, Data.IO, Http, Analysis, UI
└── Migrations/                     ← Manual SQL scripts for cards.db (001, 002; ordered by number; see Data.Db/Migrations/README.md)
```

## Project Dependencies

```
MagicAssistant.App
  ├── MagicAssistant.Core
  │     ├── MagicAssistant.Data
  │     ├── MagicAssistant.Data.IO   ← for the Import/Export commands
  │     └── MagicAssistant.Http
  ├── MagicAssistant.Data.Db         ← both store implementations
  │     ├── MagicAssistant.Data
  │     └── MagicAssistant.Http      ← ImageUrisDto for ImageCacheService
  ├── MagicAssistant.Data.IO
  │     └── MagicAssistant.Data
  └── MagicAssistant.Http.Scryfall
        └── MagicAssistant.Http

MagicAssistant.Core (business logic)
  ├── MagicAssistant.Data (local data contracts + Model)
  ├── MagicAssistant.Data.IO (JsonImport/JsonExport — native format; XmlImport — legacy)
  └── MagicAssistant.Http (external data contracts + DTOs)

MagicAssistant.Data.Db (database implementation)
  ├── MagicAssistant.Data (contracts)
  └── MagicAssistant.Http (DTOs — ImageUrisDto for ImageCacheService)

MagicAssistant.Data.IO (format import/export)
  └── MagicAssistant.Data (Model)

MagicAssistant.Http.Scryfall (Scryfall client implementation)
  └── MagicAssistant.Http (contracts + DTOs)

MagicAssistant.Data (contracts + Model)
  └── no dependencies on other projects

MagicAssistant.Http (contracts + DTOs)
  └── no dependencies on other projects
```

### Key Architecture Rules
1. The Core project works only with the Data and Http contracts. It knows nothing about the Http.Scryfall, Data.Db, Data.IO implementations.
2. Http.Scryfall, Data.Db and Data.IO know nothing about each other. Data.Db and Data.IO know only the Data contracts. Http.Scryfall knows only the Http contracts.


## Single Database — `cards.db`

All data lives in one SQLite file: `{dataPath}/db/cards.db`.

### Tables

| Table | PK | Description |
|---------|----|----------|
| **Cards** | `Id` (uniqueidentifier) | Card knowledge base (synchronized from Scryfall). 40+ columns: Name, ManaCost, Type, OracleText, Rulings, Artist, SetCode, Rarity, Cmc, ColorType, LegalityFormats, etc. |
| **CardsFts** | FTS5 rowid | Virtual FTS5 table for full-text search (CardId UNINDEXED, Name, Type, OracleText, Artist). The `cards_fts_ai/ad/ud` triggers keep the index in sync on INSERT/UPDATE/DELETE in Cards. |
| **CardSet** | `Id` (uniqueidentifier) | Set reference: Name, SetCode, Uri, CardCount, ReleaseDate, SetType, Digital/NonfoilOnly/FoilOnly flags. |
| **Collection** | `Id` (INTEGER AUTOINCREMENT) | Navigation tree nodes. NodeType: 0=Folder, 1=Collection, 2=Deck, 3=Sideboard. Locked: BOOLEAN DEFAULT 0. FK ParentId → Collection(Id) ON DELETE CASCADE. A sideboard is a child node of its deck (ParentId = deckId). |
| **Deck** | `CollectionId` + `CardId` (composite PK) | Physical cards inside decks/folders. Count, IsOwn, Comment, DateAdded, ForTradeCount, MyPrice, Grade. FK CollectionId → Collection(Id), FK CardId → Cards(Id), both ON DELETE CASCADE. |
| **UserSettings** | `Key` (TEXT) | User UI settings: Key/Value pairs (splitter, view mode, page size, set-type filter). |
| **CardFaces** | `Id` (INTEGER AUTOINCREMENT) | Faces of multifaced cards (transform/split/flip/mdfc): FaceIndex, Name, ManaCost, TypeLine, OracleText, Power/Toughness, Cmc, Colors, ColorBits, IdentityBits. FK CardId → Cards(Id). |
| **CardAllParts** | `Id` (INTEGER AUTOINCREMENT) | Parts of composite cards (split/meld): PartIndex, RelatedCardId, Component, Name, TypeLine, Uri. FK CardId → Cards(Id) ON DELETE CASCADE |
| **CardsImageUris** | `CardId` (uniqueidentifier) | Scryfall image URIs for cards: Small, Normal, Large, Png. FK CardId → Cards(Id). |
| **CardFacesImageUris** | `FaceId` (INTEGER) | Scryfall image URIs for card faces: Small, Normal, Large, Png. FK FaceId → CardFaces(Id).


**Indexes:**
- `idx_cards_name` — `Cards(Name)` — card name search
- `idx_cards_set` — `Cards(SetCode)` — filtering by set
- `idx_cards_rarity` — `Cards(Rarity)` — filtering by rarity
- `idx_collection_parent` — `Collection(ParentId)` — tree navigation
- `idx_deck_cardid` — `Deck(CardId)` — finding card instances
- `idx_cardfaces_cardid` — `CardFaces(CardId)` — card faces
- `idx_cardallparts_cardid` — `CardAllParts(CardId)` — card parts
- `idx_cards_colorbits` — `Cards(ColorBits)` — color filters
- `idx_cards_identitybits` — `Cards(IdentityBits)` — identity filters
- `idx_cards_powernumber` — `Cards(PowerNumber)` — P/T/CMC filters
- `idx_cards_toughnessnumber` — `Cards(ToughnessNumber)` — P/T/CMC filters
- `idx_cards_cmcnumber` — `Cards(CmcNumber)` — P/T/CMC filters

### Initialization

`InitializeDb.EnsureCreatedAsync(dbPath)` — a static method in `MagicAssistant.Data.Db` that creates the **tables**, the FTS5 virtual table with triggers, and the indexes. Idempotent (`CREATE TABLE IF NOT EXISTS`).

`SqliteCardStore.InitializeAsync()` and `SqliteCollectionStore.InitializeAsync()` — no-ops (to satisfy the interface contracts).

In `Program.cs` initialization runs after `Build()`:

```csharp
// Initialize DB — create all tables and indexes (idempotent)
await InitializeDb.EnsureCreatedAsync(cardsDb);
```

## Data Model

### Card — base card from the DB

One instance = one unique card (a specific printing). `Id` = Scryfall card ID.

### MyCard — physical card instance in a collection/deck

References the base `Card` (the `Card` property). `Collection` (nullable) — a reference to the collection/deck/sideboard node; `null` = an unresolved card, not yet bound to a node. `IsSideboard` is derived from `Collection.NodeType == NodeType.Sideboard`. Instance properties: `Count`, `IsOwn`, `DateAdded`, `Comment`, `MyPrice` (per-instance price), `Grade` (`CardGrade?` — card condition); `OwnCount` is computed (`IsOwn ? Count : 0`).


### MagicCardField — enumeration of all card fields

Extension methods:
- `field.Get(card)` → read any field value from an `ICard`
- `field.SetM(card, value)` → set it on a `Card`
- `field.SetM(physical, value)` → set it on a `MyCard` (non-physical fields delegate to the base card)
- `field.IsPhysical()` → check whether a field is physical
- `field.IsTransient()` → check whether a field is transient (not persisted)
- `MagicCardFieldExtensions.AllNonTransientFields(bool physical)` → field list for iteration
- `MagicCardFieldExtensions.FieldByName(string name)` → lookup by name with aliases

### ColorBitFlags — bitwise color flags

`[Flags] enum` for efficient color filtering via bitwise operations in SQLite:
- Bits 0-5: C(0x001), W(0x002), U(0x004), B(0x008), R(0x010), G(0x020)
- Bit 6: MULTI(0x040) — multicolor (2+ colored bits)
- Bit 7: MONO(0x080) — monocolored (exactly 1 colored bit)
- Bit 8: HYBRID(0x100) — hybrid mana ({W/U})
- `ColorBitBuilder.Build(colors, manaCost, identityColors?)` computes the bit mask
- Stored in the `ColorBits` (INTEGER) and `IdentityBits` (INTEGER) columns of the `Cards` table
- For double-faced cards (transform/modal_dfc/meld/reversible_card/double_faced_token): `ColorBits` and `IdentityBits` in `Cards` are NULL; bits are computed per face and stored in `CardFaces.ColorBits` / `CardFaces.IdentityBits`

### Collection — collection tree node

Node POCO (table `Collection`, PK: `Id` autoincrement, FK: `ParentId`):
- `NodeType`: 0=Folder, 1=Collection, 2=Deck, 3=Sideboard; a sideboard is the hidden child of exactly one Deck
- `CardCount`, `Locked` — node bookkeeping fields
- A physical card instance holds a reference to its node (`MyCard.Collection`, nullable); "unresolved" cards during import carry `null` and get stamped with the node only at commit time

## Data Access Contracts

### ICardStore<T> — CRUD card store

### IDbCardStore — specialized card knowledge-base store

### ICollectionStore — collection and deck management

## Store Implementations

### SqliteCardStore — card knowledge base

`IDbCardStore` implementation on SQLite + Dapper.  
Stores MTG cards and sets in the `Cards` and `CardSet` tables.

**Notable details:**
- `ConcurrentDictionary<Guid, Card>` — L1 cache for `GetCardAsync()`
- `INSERT OR REPLACE` — UPSERT during synchronization
- Transactions for batch operations (`AddAllAsync`)
- `SearchAsync` — FTS5 full-text search with relevance ranking
- `GetCardsByIdsAsync` — batch query `WHERE Id IN (@ids)` for efficient card resolution
- `RemoveAsync/RemoveAllAsync` — NotSupportedException (DB cards are never deleted directly)


### SqliteCollectionStore — collections and decks

`ICollectionStore` + `ICardStore<MyCard>` implementation on SQLite + Dapper.  
Works with the same database (`cards.db`), tables `Collection` and `Deck`.

**Notable details:**
- The `DeckCardColumns` constant — the single source of the column list for every read SELECT (GetCardAsync, GetCardsAsync, GetDeckCardsAsync, multi-collection, reconciliation)
- `AttachFacesAsync(conn, physicals, ct)` — batched CardFaces pull in one query after every read that returns physicals; deliberately not called in `GetAllDeckCardsAsync` (reconciliation)
- `ON CONFLICT(CollectionId, CardId) DO UPDATE SET Count = Count + excluded.Count` — automatic merge when duplicate cards are added
- Transactions for batch operations (`AddAllAsync`, `CreateDeckAsync`)
- **JOIN queries** in all read methods: `GetDeckCardsAsync()`, `GetAllAsync()`, `GetCardAsync()` perform a `LEFT JOIN Cards` and return a `MyCard` with full base-card information (name, mana cost, type, legality, properties)
- `GetDeckWithSideboardAsync(deckId)` — includes the sideboard (finds the NodeType=3 child node via SQL)
- `CreateDeckAsync()` — creates the main deck (NodeType=2) + sideboard (NodeType=3, ParentId=deckId) in one transaction

## Scryfall Integration

### IScrayfallClient (contract)

### ScryfallClient (implementation)

HTTP client for `https://api.scryfall.com`.  
Automatic pagination via `has_more` + `next_page` (not the Link header).

### ScryfallMapper (business logic in Core)

Maps Scryfall DTOs → Card:
- Parses collector numbers in formats: `"1"`, `"1/234"`, `"42a"`, `"A5"`
- Maps legality from Scryfall legalities → LegalityMap (via the `FormatMap` dictionary)
- Stores image URIs in `Card.ImageUris` (persisted to the CardsImageUris table)

## ImageCacheService — card image caching

Downloads and stores card images locally.

### Directory Structure

**Template:** `{imagesDir}/{set-abbreviation}/{language}/{front|back}/{card-uid}.png`

| Card | Language | Side | UID | Path |
|-------|------|---------|-----|------|
| Amonkhet #1 | `en` | `front` | `D89203BA-...` | `afr/en/front/D89203BA-8EBA-41F5-8CC3-DF3327F577A9.png` |
| Flip #42 (side A) | `en` | `front` | `AAAA1111-...` | `m25/en/front/AAAA1111-BBBB-CCCC-DDDD-EEEEFFFF0000.png` |
| Flip #42 (side B) | `en` | `back` | `BBBB2222-...` | `m25/en/back/BBBB2222-CCCC-DDDD-EEEE-FFFF00001111.png` |

**Directory preparation:** `CreateSetLanguageDirs(setCode, languages[])` creates `{set}/{lang}/front` and `{set}/{lang}/back` for each language.

**URL priority:** `normal` → `large` → `png` → `small`

**Multifaced cards:** side detection from the URL (`/front/` or `/back/`). If the URL carries no marker — skipped.

**Existing-file check:** `GetExistingCardUidsForSet(setCode)` recursively scans the `{set}/` folder and extracts the UID from the file name (`.png` → Guid.TryParse). During synchronization, if `card.CardId` ∈ existing UIDs → the download is skipped.

**Front/back split:** 
- Regular cards → `front/{uid}.png`
- Multifaced cards → `front/{uid}.png` for the front, `back/{uid}.png` for the back side

### Lazy image loading (on-demand)

The UI never requires pre-downloaded images: if the file is not on disk, it is downloaded individually at the moment the user sees the card.

- **Render points** (all via `ImageHelper.GetImageUrl` → `GET /api/images/{set}/{lang}/front|back/{uid}.png`): `CardDescriptionView`, `CardGalleryView`, `LastActions`, `PrintingsView`, `OldDeckImportWizard` (hover preview).
- **Gap detection** — server-side checks `IImageCacheService.FrontImageExists/BackImageExists` (the same condition under which the controller answers 404). The `<img>` is rendered only when the file exists on disk; otherwise a placeholder (⏳ while downloading, "no image" after a failure).
- **`DownloadCardImagesCommand(CardId)`** — individual command: `IDbCardStore.GetCardAsync` re-reads the card from the KB **with** `ImageUris`/`CardFaces`, then `IImageCacheService.DownloadAsync(card)` downloads all faces with the same code as the "Images" phase of a set sync. No calls to the Scryfall API — image files only.
- **`ICardImageLoader`** (App) — UI wrapper: `FrontExists/BackExists/Failed` + `EnsureAsync(card)`. Concurrent requests for one card are serialized through a `ConcurrentDictionary<Guid, SemaphoreSlim>`: the semaphore is created once per card and lives in the dictionary for the loader's lifetime (each contender waits on the same instance — every Release matches an acquire). The first one takes it and downloads; the rest wait for release; upon entering the section each first checks whether the file exists — if a previous contender already downloaded it, exit without re-downloading. Failures are remembered (a card without images does not trigger a re-download on every re-render).
- **Flow:** the component detects missing files → fire-and-forget batch `EnsureAsync` (parallel, one per card) → after each completion `StateHasChanged()` → the image appears in the grid/view without a page reload.

## CQRS — Commands and Queries

### Commands (mutating operations)

| Command | Description | Delegates to service |
|---------|----------|---------------------|
| `CreateNodeCommand` | Unified node creation (Folder/Collection/Deck; Deck → + sideboard in one transaction) | — |
| `AddCardsToDeckCommand` | `(int DeckId, ICollection<ICard> Cards)` — add cards to a deck (ICard → MyCard conversion) | — |
| `RemoveCardsFromDeckCommand` | `(int DeckId, ICollection<Guid> CardIds)` — DeckId = the node's own ID; delegates to `ICollectionStore.RemoveCardsAsync`: 1 SQL query (`DELETE ... RETURNING Count`, result = sum of returned copies, not the row count); non-empty list: delete ALL copies of the cards, empty: wipe every card in the node (the node remains). Used by `DeckCardService.RemoveCardsAsync` / `ClearNodeAsync(nodeId)` (CQRS) | — |
| `MoveCardsCommand` | Move cards between collections: remove from source + add to target (merge semantics via ON CONFLICT) | — |
| `UpdateCardCommand` | `(int DeckId, Guid CardId, Dictionary<MagicCardField, object?> Updates)` — update instance properties: count, ownership, price (`MyPrice`), comment, grade, forTradeCount | — |
| `ImportResolvedCardsCommand` | Atomic commit of a fully resolved import: new mode → `CreateDeckWithCardsAsync` (one transaction), existing mode → merge. CardOverrides — choices from the unresolved-cards table |
| `SyncKBCommand` | Full set sync from Scryfall (orchestrating command) | `IKnowledgeBaseSyncService` |
| `SaveUserSettingCommand` | Save user settings (splitter, view mode, page size) | — |

### Queries (reads)

| Query | Description |
|-------|----------|
| `GetDeckCardsQuery` | Get all deck cards via `ICollectionStore.GetDeckCardsAsync()` with a SQL JOIN to Cards |
| `SearchCardsQuery` | Search cards in the database (LIKE over name/type/oracleText/artist) |
| `GetScryfallAvailableSetsQuery` | List of available sets from Scryfall (30-minute cache) |
| `GetKBAvailableSetsQuery` | Sets present in the knowledge base (set-selection forms of SyncKnowledgeBase / SyncSelectedSetCards; the CardsListControl sidebar uses `FilteredCardSetsCollection` → `IDbCardStore.GetCardSetsAsync`) |
| `HasCardsQuery` | Whether the DB holds at least one card of a set (`IDbCardStore.HasCardsForSetAsync` = `SELECT EXISTS`) — drives the empty state of CardsListControl: the sync button appears only when the clicked set is genuinely empty in the DB |
| `GetSyncProgressQuery` | Polling sync progress through SyncSetStateRegistry |
| `GetNodesTreeQuery` | **One SQL query**: all tree nodes at once. Dictionary keys are root nodes; nested nodes live in folders' `Children`. Deck nodes carry a filled `Sideboard`. Sorting on the .NET side: Collection → Deck → Folder |
| `GetNodeByIdQuery` | A single node by ID |
| `GetNodesBranchQuery` | Subtree rooted at a given node (recursive SQL). Used to resolve the sideboard during Move-cards |
| `GetCardByIdQuery` | Knowledge-base card by ID |
| `GetCardPrintingsQuery` | All printings of a card (by name) |
| `GetCardLanguagesQuery` | Language versions of a card (switching in CardDescriptionView) |
| `GetCardInstancesQuery` | Physical instances of a card across all collections |
| `GetCardSetQuery` | Set metadata (full name, release date) |
| `GetDeckAnalysisQuery` | Single-pass deck analysis via `IAnalysisService` |
| `GetSetSyncStatusByCodesQuery` | Sync status for set codes in one batch SQL (in CardSet? how many cards in Cards) — pre-check of the native JSON import: the file carries set codes, a code with CardCount == 0 requires a sync from Scryfall |
| (collector progress) | Collector progress — not a CQRS query but `IDeckAnalysisService.GetCollectorProgressAsync(setTypes)`: one SQL (Total — `CardSet.CardCount`; Collected/Owned — distinct `CollectorNumber`, language duplicates collapsed; an active set-type filter adds `WHERE s.SetType IN (...)`) |
| `GetUserSettingQuery` | Read user settings (UI parameters) |
| `GetSetSyncStatusQuery` | Sync status for full set names in one SQL: in CardSet? how many cards in Cards — "set without cards" detection for the legacy pre-check |
| `ResolveLegacyCardsQuery` | Batch resolve of legacy cards (read-only): strip `(Face)` after `//`, set names → codes, (Name, Set) → Scryfall UUID — exact match across all name fields (Cards/CardsFaces: Name + PrintedName), local names work; result {Resolved: physicals with real IDs, Unresolved: rows for manual mapping} |
| `ValidateCardIdsQuery` | Known/Unknown against the KB in one batch query — the modern-import invariant: every card must exist in the KB before the node is created |

## SyncKBCommand — Knowledge Base Synchronization

An orchestrating command delegating to `IKnowledgeBaseSyncService`.

### Execution Phases

1. **Phase 0: Resolve set** — look up the set information in Scryfall
2. **Phase 1: Download** — download the set's card list via `IScrayfallClient.GetCardsAsync(setCode, englishOnly)`
3. **Phase 2: Map** — map DTO → Card via `ScryfallMapper`
4. **Phase 3: Images** — download images via `IImageCacheService.DownloadAsync()`, checking existing files by UID
5. **Phase 4: Save** — save into the Knowledge Base via `IDbCardStore.AddAllAsync()`
6. **Phase 5: Done** — completion, report

### Progress Tracking

`SyncSetState` (thread-safe) + `SyncSetStateRegistry` (singleton):

Polling via `GET /api/sync/progress?set={abbrev}` → JSON `SyncProgressResponse`.

### Result

## Import/Export Formats (Data.IO)

Two formats: **native JSON (v1)** — the primary one for UI import/export, and **legacy XML** — OldDeckImportWizard only (Gatherer ID + full set names). Both return the shared `CardCollectionImportResult`.

### JsonImport / JsonExport (native format)
- Wrapper: `format: "magic-assistant/deck"`, `version: 1`, `name`, `comment`, `type: "deck"`, `exportedAt` (ISO 8601) — always written
- Card: reference core `id` (Scryfall UUID), `name`, `set`, `count` — always; import requires all four (missing = FormatException, no default is applied to count)
- Optional fields are omitted by default: `own` only when false, `price` ≠0 (short round-trip float representation), non-empty `comment`, `forTrade` >0
- Pretty-print with 2-space indent; unknown fields are ignored on import (forward-compat); legacy bridge: the `fortrade` tag in the `special` field maps to forTrade; foreign format/mismatched version → FormatException

### XmlImport (legacy)
- Parses legacy XML: `<cards>` → `<list>` → `<mcp>` (MyCard); Gatherer ID + full set names
- Supports physical cards only (`<mcp>`); base cards `<mc>` are not imported
- `ReadElementValue()` — distinguishes simple elements from elements with child nodes

## DI Configuration (Program.cs)

**Singleton:**

| Service | Implementation |
|--------|-----------|
| `IDbCardStore` | `SqliteCardStore` (`cards.db`) |
| `ICollectionStore` | `SqliteCollectionStore` (`cards.db`) |
| `ICardStore<MyCard>` | `SqliteCollectionStore` (`cards.db`) |
| `IUserSettingsStore` | `SqliteUserSettingsStore` (`cards.db`) |
| `IImageCacheService` | ImageCacheService (factory) |
| `ISetIconsCacheService` | SetIconsCacheService (factory) |
| `CardSetsCache`, `AllCardSetsCache` | Set metadata caches |
| `SyncSetStateRegistry` | Thread-safe sync progress tracker |
| `IAnalysisService` | `AnalysisService` (single-pass analysis) |

**Scoped:**

- All Command/Query handlers: `AddCardsToDeckCommandHandler`, `GetDeckCardsQueryHandler`, `SearchCardsQueryHandler`, `HasCardsQueryHandler`, `SyncKBCommandHandler`, `GetScryfallAvailableSetsQueryHandler`, `GetSyncProgressQueryHandler`, `GetSetSyncStatusQueryHandler`, `GetSetSyncStatusByCodesQueryHandler`, `DownloadCardImagesCommandHandler`, `ResolveLegacyCardsQueryHandler`, `ValidateCardIdsQueryHandler`, `ImportResolvedCardsCommandHandler`, `RemoveCardsFromDeckCommandHandler`, `MoveCardsCommandHandler`, `UpdateCardCommandHandler`, `CreateNodeCommandHandler`, `DeleteNodeCommandHandler`, `RenameNodeCommandHandler`, `LockNodeCommandHandler`, `UnlockNodeCommandHandler`, `GetDeckAnalysisQueryHandler`, `GetNodesTreeQueryHandler`, `GetNodeByIdQueryHandler`, `GetNodesBranchQueryHandler`, `GetCardByIdQueryHandler`, `GetCardPrintingsQueryHandler`, `GetCardLanguagesQueryHandler`, `GetCardInstancesQueryHandler`, `GetKBAvailableSetsQueryHandler`, `GetCardSetQueryHandler`, `SyncCardSetsCommandHandler`, `SaveUserSettingCommandHandler`, `GetUserSettingQueryHandler`
- Vertical services: `IDeckManagementService`, `IDeckCardService`, `ICardResolveService`, `IKnowledgeBaseSyncService`, `IDeckImportExportService`, `ICardSearchService`, `IDeckAnalysisService`
- `KbRefreshService` — subscribe/notify the UI when a knowledge-base sync completes

## UI — Blazor Server

### MainLayout.razor
Three-panel layout (left — navigator, center — content with tabs, right — card description + printings + instances).

Layout mode: `LayoutMode.Management` (three-panel layout) / `LayoutMode.Showcase` (CollectorView only, no management UI); switched via the Showcase/Management buttons in the header, persisted in user settings (`MainLayout.Mode`).

At the top of the center panel — `ContextStrip` (`Shared/ContextStrip.razor`): a three-section strip — selected on the left, a gold 24×24 SVG arrow in the middle (pointing right), pinned on the right. Selected is always filled: either the selected collection or the KB variant "KnowledgeBase Cards" without stats. Pinned — deck/collection/sideboard only; empty → "? (pin to set)" in bold gray 24px. Names are bold gold 24px, pressed toward the center (selected to the right, pinned to the left), so in the middle "Selected Name → Pinned Name" fuse together; the "(x/y)" stats with the "unique: x, total: y" tooltip are pushed to the outer edges (selected to the left, pinned to the right). Stats: `IDeckCardService.GetDeckCardsAsync(collectionId)` (unique = number of unique cards, total = sum of copies); reloaded when the contexts change and via `_stripRefresh++` (`RecordLastActionAsync` — +/−, KB add, Move; `OnEditCardSaved`; `OnSplitDone`; `OnImportCompleted`; `OnLegacyImportCompleted`). SearchControl was moved out of the center panel into the top of the CardFilterDialog window.

Header actions: `Legacy Import`, `Update DB` (Import was moved into the navigator's add-node menu with the parent context — `ParentNodeId` → DeckImportWizard; Export — into the ⋯ context menu, exporting the clicked node via `_exportTargetId`). Overlay dialogs are driven by state flags (`_showXxx`) and render only when visible; they close only via the ✕ button (and/or Cancel + Escape where present) — clicking the overlay does nothing. Status bar at the bottom of the center panel.

Content area (`@key="_currentCollectionId"`): deck mode — TabBar (Cards + analysis pages) via the `RenderDeckContent()` RenderFragment; KB mode — `RenderKBContent()`. Mode switching: `_currentCollectionId` + `FilterViewModel.CurrentCollectionId`.

Current/pinned model: two independent contexts — `_currentCollection` (`LockableCollection`: what the center panel shows) and `_pinnedCollection` (the pinned operation target: LastActions, KB "+", quick Move). The only writer of pinned is the navigator's Pin buttons; selecting a node (`OnDeckSelected`) sets only current, and the LastActions queue is never cleared — each entry carries its own Location label. Jumping from InstancesView (`OnDeckSelectedFromInstance`) changes only current; `OnShowKB` resets only current (pinned and the queue live on). Ghost validation: `OnNodeDeleted` checks nodes via `IDeckManagementService.GetByIdAsync` and resets vanished contexts (covering both direct deletion and the cascade of "deleted a folder containing the pinned deck"). Lock state: `IsLocked` is stored in both `LockableCollection` instances (a snapshot taken at selection time) and refreshed via the `OnNodeLockToggled` event from DecksNavigator; locking a node unpins it. Locked collections accept no card operations — the −/+, (+), Move, ✎, ⫯ buttons are hidden in the UI.

### CardsListControl.razor
Unified card browser for both modes: KB (`CurrentCollectionId == null`) and deck/location (`CurrentCollectionId` set).

- Set sidebar with a resizable splitter (JS ES Module `/js/splitter.js`, 160–500px, position persisted in user settings).
- Toolbar: name filter (300ms debounce, 3+ characters), view mode (list/gallery/tree, persisted), multi-select Type/Rarity, Filters button (CardFilterDialog), `📦 Move` — only when the displayed collection differs from the pinned one and neither is locked (`CurrentCollectionId ≠ null && PinnedCollectionId.HasValue && CurrentCollectionId ≠ PinnedCollectionId && !CurrentCollectionLocked && !PinnedCollectionLocked`).
- Sorting — via CardListView column headers (Name/CMC/Rarity/Set/Type): click = switch sorting to that column (ascending unless already on it), clicking the same header again = flip direction; the active header is highlighted (▼/▲). State lives in `FilterViewModel.SortField`/`SortAsc`, same channel (`Apply()` → `Changed`).
- Server-side pagination: `ICardSearchService.GetPagedAsync(CardListFilter)`; page size 1–150 (persisted).
- Deck mode: physical instances via `IDeckCardService.GetDeckCardsAsync` — count −/+, ✎ edit, ⫯ split (Count > 1), card selection → right panel.
- Quick Move (`📦 Move`): `OnMoveToPinned(physical)` → MainLayout calls `IDeckCardService.MoveCardsAsync(currentCollectionId, [cardId], pinnedCollectionId)` (the card leaves with all its copies) + an entry in `LastActionsQueue` (Add, number of moved copies) + `_kbRefreshTrigger++` (list reload).
- KB mode with a pinned collection set: rows show only "+" (`KbAddMode`, hidden when the pinned node is locked) → `IDeckCardService.MaterializeFromIdsAsync(pinnedCollectionId, [cardId])` (merge semantics) + a queue entry. The left **Total** column: in KB mode with pinned — the number of copies of the card in the pinned collection (one GROUP BY via `IDeckCardService.GetDeckCardCountsAsync`; 0 → blank, fixed width; updated locally on "+", reloaded when pinned changes); in deck mode it replaces the old count column — the −/+ stepper (read-only when locked), always > 0 since a card with zero copies disappears from the deck; the # column is hidden in deck mode. −/+ operations in deck mode are always recorded in the queue — with a location label (the current node's name); when current is locked the −/+, ✎, ⫯ buttons are hidden (count shown read-only); the queue is never cleared, entries from different collections coexist.
- Sync is available only in KB mode — an explicit `IsLocationMode` guard (`CurrentCollectionId.HasValue`): the "Sync from Scryfall" button in the empty state renders only when `!IsLocationMode` **and** the clicked set is genuinely empty in the DB (a direct `HasCardsQuery` request, not inferred from filters: an empty list while the set has cards = "hidden by filters", the button is not shown). `OnSyncClick()` is inert in location mode, and `RenderDeckContent()` does not pass `OnOpenSync` at all (collection/deck/sb — display cards from the KB only). The "Update DB" header action stays visible in all modes (global chrome — Ron's decision).
- The set list is a lazy DB view `FilteredCardSetsCollection` (Shared/): cached by the scope composition `FilterViewModel.FilteredSetCodes`, first DB query on first render / scope composition change, `Invalidate()` after a KB sync. Location mode: when a node loads, the distinct `SetCode`s of its cards go into `FilterViewModel.SetNodeScope(codes)` (auto-scope); the explicit Sets filter in Card Filter is available only in KB mode (`SetFilteredSetCodes()`); when navigating into a location node it is trimmed down to the node's cards (nothing left → fall back to the node's full scope). Clicking a sidebar set — `SetActiveCardSets(code)` (exactly one code, null = reset); the search text narrows only the visible set list, not the cards. The query takes `FilterViewModel.GetEffectiveSetCodes()` (click ∩ scope; an empty intersection = empty list = zero cards). Header counters: location — `sets: _filteredSets.Count`, `cards:` the number of unique cards in the node (keys of `_physicalByCardId`), narrowed by the selected set when one is active, `total:` the sum of copies (`SUM(Count)`); KB — `1` when a set is selected, otherwise the number of sets in the scope; cards — the selected set's `CardCount` or `_totalCount`.

### SyncKnowledgeBase.razor
Knowledge-base sync component: a set dropdown, a "Download images" checkbox, a Sync button, a progress bar, a detailed report.

### Blazor components
All components are implemented. Status and detailed description — in [`ui.md`](./ui.md).

## FilterViewModel — the Single Filtering and Sorting Channel

*File:* `MagicAssistant.App/Shared/FilterViewModel.cs`

`FilterViewModel` is the central POCO class encapsulating all filtering and sorting state for both KB and deck modes. All UI components involved in filtering (SearchControl, CardsListControl, CardFilterDialog, CardFilterAdvancedDialog) mutate one instance of this class and trigger the `Changed` event to synchronize. CardFilterAdvancedDialog is a helper dialog opened by the gold Advanced button inside CardFilterDialog (an overlay above it, z-index 1100); its fields (Artist/Layout/Legality/GameChanger + Collection) are committed by the main dialog's Apply, while Close dismisses without changes.

### Properties

| Property | Type | Description |
|----------|-----|----------|
| `FilteredSetCodes` | `List<string>` | Scope of the set list: location-mode autofill (the node's card codes, `SetNodeScope()`) or the explicit Sets filter in Card Filter (KB only, `SetFilteredSetCodes()`). Empty = all sets. `Clear()` resets it only in KB mode |
| `ActiveCardSets` | `List<string>?` | The clicked sidebar set — exactly one code or null. `SetActiveCardSets(code)`; "All Sets"/✕/`Clear()` → null. The query takes `GetEffectiveSetCodes()`: click ∩ scope (OrdinalIgnoreCase); an empty intersection = empty list = "no candidate sets" (zero cards in SQL), distinguishable from null ("no set filtering applied") |
| `SetSearchText` | `string` | Text of the "Filter sets..." field. Narrows only the visible sidebar set list — does not affect cards. Survives component recreation (`@key` change on navigation). Reset by `Clear()` |
| `Rarity` | `List<string>` | Rarities (multi-select). OR expression: "common OR rare". Methods: `SetNewRarityFilter()`, `GetRarityOrExpression()` |
| `NameFilter` | `string` | Name filter (3+ characters, debounced) |
| `SearchQuery` | `string` | FTS5 query from SearchControl (`"phrase"`, `name:term`, OR/AND/NOT) |
| `TypeFilter` | `List<string>` | Card types (multi-select). OR expression: "creature OR land". Methods: `SetNewTypeFilter()`, `GetTypesOrExpression()` |
| `Artist` | `string` | Artist name |
| `ColorType` | `string` | Color-type filter value |
| `ColorFilters` | `Dictionary<ColorBitFlags, bool>` | 9 checkboxes: C/W/U/B/R/G/MULTI/MONO/HYBRID. `ColorFilterHelper.GetFilter()` builds the SQL mask |
| `ColorAndMode` / `ColorOnlyMode` / `ColorIdentityMode` | `bool` | Color filter modifiers |
| `PowerOp`/`PowerValue`, `ToughnessOp`/`ToughnessValue`, `CmcOp`/`CmcValue` | `string` | P/T/CMC properties: operator (`>=`, `=`, `<=`) + value |
| `LayoutFilter` | `string` | Layout filter (e.g. double-faced cards) |
| `LegalityFilter` | `string` | Legality filter |
| `GameChangerFilter` | `bool` | Card game-changer flag |
| `Ownership` | `string` | Collection filter: "all"/"owned"/"borrowed" → `Deck.IsOwn`. Deck mode only |
| `MinCount` / `MaxCount` | `string` | Collection filter: card count range in the location (parsed when building the query) → `Deck.Count`. Deck mode only |
| `MinPrice` / `MaxPrice` | `string` | Collection filter: USD price range (parsed when building the query) → `CAST(Cards.Usd AS REAL)`. Deck mode only |
| `IsFoil` | `bool` | Collection filter: passed to the Data layer, not yet used in SQL (no foil-flag column) |
| `HasForTrade` | `bool` | Collection filter: only cards with `Deck.ForTradeCount > 0`. Deck mode only |
| `HasComment` | `bool` | Collection filter: only cards with a non-empty `Deck.Comment`. Deck mode only |
| `CurrentCollectionId` | `int?` | ID of the displayed navigation node (deck/collection/sideboard). Navigation state, not a filter — excluded from `Clear()`. Set by MainLayout on navigator selection / InstancesView jump, reset when switching to KB. Switches CardsListControl between KB and deck modes |
| `CurrentCollectionNodeType` | `NodeType?` | Type of the displayed node; set/reset together with `CurrentCollectionId` — drives the "All sets of collection/deck/sideboard" label and the location-mode empty-state wording |
| `SortField` | `string` | Sort field: Name/CmcNumber/Type/SetCode/Rarity (matches `CardListFilter.SortFields`) |
| `SortAsc` | `bool` | Sort direction |
| `ClearGeneration` | `int` | Incremented on every `Clear()` — subscribers use it to detect an external reset and synchronize their local inputs (name debounce buffer, set search) |

### Event and Methods

| Member | Signature | Description |
|------|---------|----------|
| `Changed` | `event Action?` | Fires on `Apply()` — subscribers re-query via `InvokeAsync()` |
| `Apply()` | `void` | Triggers `Changed?.Invoke()` |
| `ClearAndApply()` | `void` | Calls `Clear()` → `Apply()` |
| `Clear()` | `void` | Resets all fields to defaults without notifying |

### Data Flow Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        MainLayout                                   │
│  private readonly FilterViewModel _filterViewModel = new();        │
│                                                                     │
│  <ContextStrip        Selected/Pinned/RefreshTrigger ... />        │
│  <CardsListControl    FilterViewModel="@_filterViewModel" ... />   │
│  <CardFilterDialog    FilterViewModel="@_filterViewModel" ... />   │
└─────────────────────────────────────────────────────────────────────┘
         │                │                                │
         ▼                ▼                                ▼
┌──────────────┐  ┌──────────────────┐          ┌─────────────────────────┐
│ SearchControl│  │ CardsListControl │          │  CardFilterDialog       │
│              │  │                  │          │                         │
│ FTS5 input   │  │ Type dropdown    ┼──┐       │ Type MultiSelectList ───┼──┐
│ Enter/🔍     │  │ (multi-value)    │  │       │ Rarity MultiSelectList ─┼──┤
│ → SearchQuery│  │ Rarity dropdown──┼──┤       │ Artist input ───────────┼──┤
│              │  │ (multi-value)    │  │       │ Color checkboxes (9) ───┼──┤
│              │  │ Name input ──────┼──┤       │ Color modifiers (3) ────┼──┤
│              │  │ Set sidebar      ┼──┤       │ Properties (P/T/CMC) ───┼──┤
│              │  │ (click select)   │  │       │ Advanced → adv dialog      │
│              │  │                  │          │ Sets picker (KB only)   │
└──────────────┘  └──────────────────┘          └─────────────────────────┘
                      ▼                                    ▼
              FilterViewModel properties ←───────←─────── mutations
                      │
              .Apply() / .ClearAndApply()
                      │
              event Changed?.Invoke()
                      │
              ┌───────▼───────┐
              │ OnFilterChanged│ (CardsListControl)
              │ _currentPage=1 │
              │ Re-query DB    │
              │ StateHasChanged│
              └────────────────┘
```

### Sources of Change (all through one channel)

| Source | ViewModel property | Mechanism |
|----------|-------------------|----------|
| SearchControl (FTS5, at the top of the CardFilterDialog window) | `SearchQuery` | Enter / 🔍 → `SearchQuery = query` → `Apply()`; ✕ / Escape → clear → `Apply()` |
| Type dropdown (toolbar, multi-value) | `TypeFilter` (List<string>) | `onchange` → `SetNewTypeFilter()` / `SetTypeFilters()` → `Apply()` |
| Rarity dropdown (toolbar, multi-value) | `Rarity` (List<string>) | `onchange` → `SetNewRarityFilter()` → `Apply()` |
| Name input (debounced 300ms) | `NameFilter` | timer / Enter / Clear → `Apply()` |
| Set sidebar text input | `SetSearchText` | Typing (2+ characters) narrows the visible set list — no card re-query; the text survives remount via `FilterViewModel.SetSearchText` |
| Set sidebar selection | `ActiveCardSets` (List<string>?) | Click a set → `SetActiveCardSets(code)` + `Apply()`; "All Sets"/✕ → `SetActiveCardSets(null)` + `Apply()` |
| CardListView column headers (list view) | `SortField`, `SortAsc` | Click = new column (asc), click again = flip direction → `Apply()` |
| MainLayout navigation | `CurrentCollectionId` | `OnDeckSelected(LockableCollection)` → `= CollectionId` (pinned untouched; queue not cleared) → `Apply()`; `OnDeckSelectedFromInstance(deckId)` → `= deckId` (current only) → `Apply()`; `OnShowKB()` → `= null` (pinned lives on) → `Apply()` |
| CardFilterDialog → Apply | all fields (Type/Rarity via MultiSelectList, ColorFilters dict, Properties; Advanced dialog: Artist/Layout/Legality/GameChanger + Collection: Ownership/Count/Price/Tags — only when CurrentCollectionId) | `Apply()` + close dialog |
| CardFilterDialog → Clear All | all fields, including the Advanced dialog's fields | `ClearAndApply()` + close dialog |

### Exceptions (direct calls, not through the channel)

| Source | Reason |
|----------|---------|
| Pagination (Prev/Next) | Navigation within the results, not a filter change |
| `RefreshTrigger` parameter | External trigger after a KB sync completes |

### Migration from the Old Pattern

**Before:** `FilterResult` → `ActiveFilter` parameter → `OnParametersSetAsync` → client-side filtering of the current page.

**Now:** `FilterViewModel` → `event Changed` → server-side re-query via `CardListFilter` → full data reload.

---

## Color Filters (ColorFilter)

Color filtering via bitwise masks in SQL:
- `ColorFilter.RequiredMask` — bits that MUST be present
- `ColorFilter.ExcludedMask` — bits that must NOT be present (Only/Identity mode)
- `AndMode` — all selected colors must be present (AND), otherwise any one (OR)
- `IdentityMode` — filter by `IdentityBits` instead of `ColorBits`, implies Only

SQL translation:
- OR mode: `(ColorBits & requiredMask) != 0`
- AND mode: `(ColorBits & requiredMask) = requiredMask`
- Excluded: `(ColorBits & excludedMask) == 0`

**DFC support** (`ToSqlWhereWithDfcSupport()`, the production method — used in `SqliteCardStore.GetPagedAsync`): double-faced cards have `ColorBits` NULL in `Cards`, so the fragment takes the shape `((cardCondition) OR (Id IN (SELECT CardId FROM CardFaces WHERE faceCondition)))`. Generation invariant: any fragment whose top level is an OR must be wrapped in outer parentheses, because `GetPagedAsync` joins whereParts with AND. The identical wrapper applies in `NumericPropertyFilter.ToSqlWhereWithDfcSupport()` (Power/Toughness/CMC: instead of an IN subquery — `cardNumberColumn IS NULL AND EXISTS(...)`).

Roles of the SQL methods: `ToSqlWhereWithDfcSupport()` — production path (GetPagedAsync); `ToSqlWhere()` — the test variant of generation (used only in ColorFilterHelperTests / ColorFilterRoundTripTests / ColorFilterSqlIntegrationTests).

`ColorFilterHelper.GetFilter(checkboxes, andMode, onlyMode, identityMode)` builds the filter from UI state.

## Filters and Expressions (MagicCardFilter, Expr)

The system is built on an AST of expressions supporting logical operations, comparisons, and regex search.

### Architecture

```
Expr (abstract base class)
├── TrueExpr / FalseExpr / EmptyExpr  ← optimized constants
│   └── And/Or/Not — short-circuit: True.And(b)=b, False.Or(b)=b
├── BinaryExpr  ← AND, OR, EQUALS, MATCHES, GE(>=), LE(<=), EQ(==)
├── NotExpr     ← unary NOT (wrapper over Expr)
├── CardFieldExpr  ← MagicCardField → ICardField wrapper
└── Value / TextValue  ← constant values (TextValue: wordBoundary, caseSensitive, regex)
```

### Key Files

| File | Role |
|------|------|
| `Expressions/Expr.cs` | Base class: `True`, `False`, `Empty`, `And()`, `Or()`, `Not()` methods |
| `Expressions/BinaryExpr.cs` | Binary expressions + static factories `FieldEquals`, `FieldMatches`, `FieldOp`, `FieldInt`, `TextSearch` |
| `Expressions/CardFieldExpr.cs` | `MagicCardField` → `ICardField` wrapper for extracting a field value from a card |
| `Expressions/Value.cs` / `TextValue.cs` | Constant values. `TextValue` supports word boundary, case sensitivity, regex |
| `Expressions/NotExpr.cs` | Unary negation |
| `Expressions/SearchStringTokenizer.cs` | Search-string parser: text tokens, `"quoted"`, `\regex:...`, `!NOT` |
| `Expressions/Abilities.cs` | Known MTG abilities (keyword + mined) with regex patterns |
| `MagicCardFilter.cs` | Main filter class: holds the AST (`root`), sorting, grouping. `FilterCards()`, `Update()` |

### Processing Pipeline

```
Raw text → SearchStringTokenizer → Token[] → BinaryExpr (MATCHES/AND) → Expr root
     ↓
MagicCardFilter.FilterCards(cards) → root.Evaluate(card) for each card
```

- `True`/`False`/`Empty` — optimization: `True.And(b)` → `b`, `False.Or(b)` → `b` (short-circuit)
- `MATCHES` delegates to `CardMatcher.Matches(card, field, textValue)`: the Type field in non-regex mode checks the primary type (the word before " — "), other fields use the regex from `TextValue` with wordBoundary/caseSensitive/isRegex flags
- `EQUALS` compares field values (string equality + numeric with a float tolerance of `0.001f`)
- `>=`, `<=`, `==` parse floats and compare numerically
- `TextSearch` tokenizes the input: `!word` → NOT, `\regex:...` → regex mode, spaces → AND

### ISearchableProperty — Group Filtering

```csharp
public interface ISearchableProperty
{
    string GetFilterField();
    IEnumerable<string> GetIds();
    string GetNameById(string id);
}
```

Implementations: `ColorTypesSearch` (W/U/B/R/G/Multicolor/Colorless), `Editions`, `Rarity`, `Locations`. Used in `MagicCardFilter.CreateGroup()` to build OR/AND groups from a parameter map.

### CardMatcher

A static class in Core (`Expressions/CardMatcher.cs`) — matching a card field against a text pattern:

```csharp
bool CardMatcher.Matches(ICard card, MagicCardField field, TextValue textValue);   // full flag set
bool CardMatcher.MatchesSimple(ICard card, MagicCardField field, string pattern);  // legacy: case-insensitive contains
```

- The Type field in non-regex mode checks the primary type; other fields use the regex from `TextValue` (wordBoundary/caseSensitive/isRegex).
- Lives in Core because it depends on `TextValue` (the expression system); the Data layer knows nothing about matching semantics.

## Aggregators (Analysis)

### Architecture

A single service `IAnalysisService` computes all metrics in **one pass**.

```
IAnalysisService
├── Analyze(IEnumerable<MyCard>) → DeckAnalysisResult
│   ├── ManaCurve (int[8]) — CMC 0-7+ for non-lands
│   ├── ColorDistribution (Dictionary<string,int>) — W/U/B/R/G/Colorless
│   ├── TypeDistribution — Land / Creature / NonCreature
│   ├── CreatureSubtypes (Dictionary<string,int>) — creature subtypes
│   ├── AbilityStats (Dictionary<string,int>) — keyword abilities
│   └── BasicStats — TotalCards, TotalManaCost, MaxRepeats
└── ComputeManaCurve(IEnumerable<MyCard>) → int[8]
```

### Key Files

| File | Role |
|------|------|
| `Analysis/IAnalysisService.cs` | Analysis service interface |
| `Analysis/AnalysisService.cs` | Single-pass implementation |
| `Analysis/DeckAnalysisResult.cs` | Immutable result object |
| `Queries/GetDeckAnalysisQuery.cs` | Query + Handler for deck analysis |

### Blazor Components

All analysis components get their data via DI: `IAnalysisService` (single-pass metrics) + `ICollectionStore` (deck cards). `DeckLegalityPage` uses only `ICollectionStore`:

| Component | What it shows |
|-----------|---------------|
| `ManaCurvePage.razor` | Bar chart of CMC 0-7+ + average CMC |
| `SpellColourPage.razor` | Progress bars of the color distribution |
| `CreaturePage.razor` | Creature subtype table |
| `TypePage.razor` | Land/Creature/Other distribution |
| `AbilityPage.razor` | Keyword ability table |
| `InfoPage.razor` | 8 cards: total cards, mana cost, max repeats, colors, lands, creatures |
| `DeckLegalityPage.razor` | Deck legality by format |

The `TabBar` in MainLayout switches analysis components; each receives a `DeckId`.

## Current State

### ✅ Implemented: vertical slices

**Vertical slice architecture:**
- Domain services: `IDeckManagementService`, `IDeckCardService`, `ICardResolveService`, `IKnowledgeBaseSyncService`, `IDeckImportExportService`, `ICardSearchService`, `IDeckAnalysisService`
- Blazor components use only the vertical services (not Data contracts directly)

**Contracts:**
- `IDbCardStore.HasCardsForSetAsync(setCode)` — `SELECT EXISTS` over Cards (drives the empty-state sync button)
- `IDbCardStore.UpdateCardPropertyAsync()` — update a single property
- `ICollectionStore.CreateCollectionAsync()` — create a collection (NodeType=1) without a sideboard
- `ICollectionStore.CreateFolderAsync()` — create a folder (NodeType=0) without a sideboard
- `ICollectionStore.GetCardsAsync()` / `GetAllAsync()` — read physical cards across all collections
- `ICollectionStore.GetAllPhysicalCardsAsync()` — every physical card (`MyCard`)
- `IDbCardStore.GetCardsByIdsAsync(ids)` — batch query for efficient card resolution
- `ICollectionStore.GetCardCountsAsync(collectionIds, cardIds)` + `IDeckCardService.GetDeckCardCountsAsync(collectionIds, cardIds)` — targeted counter sums `(CollectionId, CardId) → SUM(Count)` for a set of cards across a set of collections (one GROUP BY over Deck without a JOIN to Cards; used by LastActions for totals instead of a full deck load)

**Database tables:**
- `UserSettings` — user UI settings (Key/Value)

---

### ✅ Implemented: Core Layer and infrastructure
- **Data model** — Card, MyCard, MagicCardField, CardSet, Collection, CardGroup, ColorTypes, LegalityMap, Format
- **Contracts** — hierarchy ICardSimple → ICard → ICardModifiable → IMyCard, ICardStore<T>, IDbCardStore (HasCardsForSetAsync, GetCardsByIdsAsync, GetPagedAsync), ICollectionStore (CreateCollectionAsync, CreateFolderAsync, LockAsync, UnlockAsync, GetCardCountsAsync), IScrayfallClient
- **Filters and expressions** — AST: Expr → BinaryExpr/NotExpr/CardFieldExpr/TextValue, MagicCardFilter with sorting/grouping, SearchStringTokenizer, Abilities (regex), ISearchableProperty
- **Aggregators** — `IAnalysisService` single-pass analysis: mana curve, colors, types, creatures, abilities, basic stats. Analysis Blazor components.
- **Collector** — `IDeckAnalysisService.GetCollectorProgressAsync(setTypes)` (one SQL, set-type filter)
- **Single database `cards.db`** — tables (Cards, CardsFts, CardSet, Collection, Deck, UserSettings, CardFaces, CardsImageUris, CardFacesImageUris) + CardAllParts, FTS5 triggers, indexes, FKs with ON DELETE CASCADE
- **SqliteCardStore** — CRUD, transactions, FTS5 search, ConcurrentDictionary cache, batch query GetCardsByIdsAsync
- **SqliteCollectionStore** — SQLite + Dapper, ON CONFLICT merge, JOIN queries to Cards
- **ScryfallClient + ScryfallMapper** — auto-pagination, legality mapping
- **ImageCacheService** — `{set}/{lang}/{front|back}/{uid}.png`, per-language directory creation, UID check
- **SyncKBCommand** — phases (resolve → download → map → images → save → done), progress via SyncSetStateRegistry
- **SyncCardSetsCommand** — sync the Scryfall set list into the DB (FullUpdate flag); invoked from the SyncKnowledgeBase dialog
- **Import/export** — native JSON (JsonImport/JsonExport from Data.IO); legacy XML import — XmlImport (OldDeckImportWizard)
- **All Commands**: CreateNode, CreateDeck, CreateCollection, CreateFolder, DeleteNode, RenameNode, LockNode, UnlockNode, AddCards, RemoveCards, MoveCards, UpdateCard, SyncKB, SyncCardSets, ImportResolvedCards, SaveUserSetting
- **All Queries**: SearchCards, GetDeckCards, GetScryfallAvailableSets, GetKBAvailableSets, HasCards, GetSyncProgress, GetNodesTree, GetNodeById, GetNodesBranch, GetCardById, GetCardPrintings, GetCardLanguages, GetCardInstances, GetCardSet, GetDeckAnalysis, GetUserSetting, GetSetSyncStatus, GetSetSyncStatusByCodes, ResolveLegacyCards, ValidateCardIds
- **Import invariant**: a new node (deck/collection) is never created before every card is fully resolved. The commit happens only after 100% resolution (+ manual mapping of unresolved ones); new mode is atomic (`CreateDeckWithCardsAsync`, one transaction). The interactive orchestration lives in `DeckImportExportService` (the domain module for all import actions), business logic in the atomic commands/queries
- **MainLayout** — 3 panels, header actions, TabBar, dialog rendering (closing only via ✕/Cancel + Escape, clicking the overlay does not close), status bar; CardsListControl — unified card browser for KB and deck modes
- **SyncKnowledgeBase.razor** — working sync component
- **Analysis components** — ManaCurve, SpellColour, Creature, Type, Ability, Info, DeckLegality
- **DI**: Commands/Queries → Scoped, vertical services → Scoped, infrastructure services → Singleton
- **Lock mechanics (UI)**: a lock button in the navigator's Deck/Collection rows (visibility model like Pin's); locking a deck propagates to the sideboard (Core, `LockAsync`/`UnlockAsync`); locked nodes cannot be pinned, and locking a pinned node unpins it; card operations (−/+, (+), Move, ✎, ⫯) are hidden for locked collections (count shown read-only)
