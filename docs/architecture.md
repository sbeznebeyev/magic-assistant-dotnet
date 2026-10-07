# MagicAssistant — ASP.NET Core приложение

*ASP.NET Core Blazor Server · .NET 10 · Blazor Web App (MapRazorComponents + InteractiveServerRenderMode, prerender: false)*

## Архитектура вертикальной ответственности

Приложение построено на принципе **вертикальных срезов** — каждый бизнес-домен инкапсулирован в независимый сервис, который проходит сквозь все слои от UI до Data/Http контрактов.

> **Примечание:** бизнес-логика распределена по доменным сервисам вертикальной ответственности:
> `IDeckManagementService`, `IDeckCardService`, `ICardResolveService`,
> `IKnowledgeBaseSyncService`, `IDeckImportExportService`, `ICardSearchService`, `IDeckAnalysisService`.

### Структура вертикальных срезов

```
MagicAssistant.Core/Services/
├── DeckManagement/           CRUD колод и папок (создание, удаление, переименование, навигация)
│   ├── IDeckManagementService.cs
│   └── DeckManagementService.cs
│
├── DeckCard/                 Карты внутри колоды (add/remove/move/split/update)
│   ├── IDeckCardService.cs
│   └── DeckCardService.cs
│
├── CardResolve/              Resolve stubs → KB карты, материализация заимствованных карт
│   ├── ICardResolveService.cs
│   └── CardResolveService.cs
│
├── KnowledgeBaseSync/        Синхронизация БЗ из Scryfall
│   ├── IKnowledgeBaseSyncService.cs
│   └── KnowledgeBaseSyncService.cs
│
├── DeckImportExport/         импорт/экспорт колод (нативный JSON + legacy XML)
│   ├── IDeckImportExportService.cs
│   └── DeckImportExportService.cs
│
├── CardSearch/               FTS5-поиск, фильтрация, печатей, экземпляры
│   ├── ICardSearchService.cs
│   └── CardSearchService.cs
│
└── DeckAnalysis/             Анализ колод (mana curve, colors, legality) + collector progress
    ├── IDeckAnalysisService.cs
    └── DeckAnalysisService.cs
```

### Зависимости между срезами

```
DeckCardService        → CardResolveService (для MaterializeFromIdsAsync, ExpandGroups)
KnowledgeBaseSync      → (не зависит от других срезов, только Data/Http контракты)
DeckManagement         → (не зависит от других срезов, использует CQRS команды/queries)
DeckImportExport       → (не зависит от других срезов; ICollectionStore + JsonImport/JsonExport (нативный) и XmlImport (legacy))
CardSearch             → (не зависит от других срезов, только IDbCardStore + ICollectionStore)
DeckAnalysis           → IAnalysisService (существующий сервис анализа колод)
```

### Поток данных

```
┌─────────────────────────────────────────────────────────────┐
│              Blazor Components (UI)                         │
│  DecksNavigator     → IDeckManagementService               │
│  CardsListControl   → ICardSearchService + IDeckCardService │
│           (KB-режим и deck-режим через CurrentCollectionId) │
│  CardDescriptionView → ICardSearchService                  │
│  PrintingsView     → ICardSearchService                    │
│  InstancesView     → ICardStore<MagicCardPhysical>         │
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

## Структура проекта

```
MagicAssistant.slnx (net10.0)
├── MagicAssistant.App              ← ASP.NET Core, DI, Blazor Server UI (net10.0)
│   └── Blazor-компоненты вызывают вертикальные сервисы из MagicAssistant.Core напрямую через DI
│
├── MagicAssistant.Core             ← Бизнес-логика (net10.0)
│   ├── Services/                   ← Вертикальные срезы (доменные сервисы)
│   │   ├── DeckManagement/         ← CRUD колод и папок
│   │   ├── DeckCard/               ← Карты внутри колоды
│   │   ├── CardResolve/            ← Resolve + Materialize
│   │   ├── KnowledgeBaseSync/      ← Scryfall sync
│   │   ├── DeckImportExport/       ← XML import/export
│   │   ├── CardSearch/             ← FTS5 search + filter
│   │   └── DeckAnalysis/           ← Mana curve, colors, legality
│   ├── Commands/                   ← CQRS: модифицирующие операции
│   │   ├── CreateNodeCommand.cs      ← унифицированное создание узла (Folder/Collection/Deck; Deck → + sideboard)
│   │   ├── CreateDeckCommand.cs      ← создание колоды (NodeType=2) + sideboard
│   │   ├── CreateCollectionCommand.cs← создание коллекции (NodeType=1)
│   │   ├── CreateFolderCommand.cs    ← создание папки (NodeType=0)
│   │   ├── DeleteNodeCommand.cs      ← удаление узла + sideboard-детей
│   │   ├── RenameNodeCommand.cs      ← переименование узла + sideboard-детей
│   │   ├── LockNodeCommand.cs        ← блокировка узла (Locked=1); для deck — состояние распространяется на sideboard
│   │   ├── UnlockNodeCommand.cs      ← разблокировка узла (Locked=0); для deck — снимает lock со sideboard
│   │   ├── AddCardsToDeckCommand.cs  ← добавление карт в колоду
│   │   ├── RemoveCardsFromDeckCommand.cs ← чистый делегат в `ICollectionStore.RemoveCardsAsync` (1 SQL-запрос): непустой `CardIds` = удаление ВСЕХ копий карт, пустой = чистка всех карт узла
│   │   ├── MoveCardsCommand.cs       ← перемещение карт между коллекциями (merge-семантика)
│   │   ├── UpdateCardCommand.cs      ← обновление свойств карты
│   │   ├── SyncKBCommand.cs          ← делегирует в KnowledgeBaseSyncService
│   │   ├── SyncCardSetsCommand.cs    ← синхронизация метаданных сетов
│   │   ├── ImportResolvedCardsCommand.cs ← атомарный коммит полностью зарезолленного импорта (new: узел+sideboard+карты в одной транзакции; existing: merge)
│   │   └── SaveUserSettingCommand.cs ← сохранение пользовательских настроек
│   ├── Queries/                    ← CQRS: чтение данных
│   │   ├── GetScryfallAvailableSetsQuery.cs ← доступные сеты из Scryfall
│   │   ├── GetKBAvailableSetsQuery.cs       ← сеты, присутствующие в БЗ
│   │   ├── GetDeckCardsQuery.cs             ← карты колоды с SQL JOIN к Cards
│   │   ├── SearchCardsQuery.cs              ← поиск карт в БД
│   │   ├── GetSyncProgressQuery.cs          ← прогресс синхронизации (SyncSetStateRegistry)
│   │   ├── GetNodesTreeQuery.cs             ← всё дерево навигации одним запросом (ключи словаря — root-узлы; у deck-узлов заполнен Sideboard)
│   │   ├── GetNodeByIdQuery.cs              ← один узел по ID
│   │   ├── GetNodesBranchQuery.cs           ← поддерево с корнем в заданном узле (рекурсивный SQL)
│   │   ├── GetCardByIdQuery.cs              ← карта по ID
│   │   ├── GetCardPrintingsQuery.cs         ← печати карты
│   │   ├── GetCardLanguagesQuery.cs         ← языковые версии карты
│   │   ├── GetCardInstancesQuery.cs         ← физические экземпляры карты
│   │   ├── GetCardSetQuery.cs               ← метаданные сета
│   │   ├── GetDeckAnalysisQuery.cs          ← анализ колоды
│   │   ├── GetCollectorProgressQuery.cs     ← прогресс коллекционера по сетам (Total из CardSet.CardCount, Collected/Owned — distinct CollectorNumber)
│   │   ├── GetUserSettingQuery.cs           ← чтение пользовательских настроек
│   │   ├── GetSetSyncStatusQuery.cs         ← статус синка полных имён сетов одним SQL (в CardSet? сколько карт?)
│   │   ├── ResolveLegacyCardsQuery.cs       ← батч-резолв legacy-карт: стрип face-суффикса, name→code, (name,set)→UUID по всем полям имени (Name/PrintedName + CardFaces); {Resolved, Unresolved}
│   │   ├── ValidateCardIdsQuery.cs          ← Known/Unknown по БЗ — pre-check modern-импорта
│   │   └── NodesHelper.cs                   ← маппинг Collection → NavigatorNode* (построение дерева)
│   ├── Expressions/                ← Система фильтров и выражений
│   │   ├── Expr.cs                 ← Базовый класс: True, False, Empty, And, Or, Not
│   │   ├── BinaryExpr.cs           ← AND, OR, EQUALS, MATCHES, >=, <=, == + фабрики
│   │   ├── CardFieldExpr.cs        ← Обёртка MagicCardField → ICardField
│   │   ├── CardMatcher.cs          ← Сопоставление поля карты с TextValue (regex, wordBoundary, caseSensitive)
│   │   ├── Value.cs / TextValue.cs ← Константные значения (regex, wordBoundary)
│   │   ├── NotExpr.cs              ← Унарное NOT
│   │   ├── SearchStringTokenizer.cs← Парсер строки поиска (!, quotes, \regex:)
│   │   └── Abilities.cs            ← способности MTG с regex-паттернами
│   ├── MagicCardFilter.cs          ← Главный класс фильтра: AST, сортировка, группировка
│   ├── ScryfallMapper.cs           ← DTO → MagicCard маппинг
│   ├── ImageCacheService.cs        ← Скачивание + кэширование картинок карт
│   └── SyncSetState.cs             ← Thread-safe прогресс-трекер синхронизации
│
├── MagicAssistant.Data             ← Контракты доступа к локальным данным + Model (net10.0)
│   ├── Contracts/                  ← интерфейсы хранилищ
│   │   └── ICardStore.cs           ← ICardStore<T>, IDbCardStore, ICollectionStore
│   └── Model/                      ← доменные модели
│       ├── Interfaces/             ← ICard, IMagicCard, ICardModifiable, IMagicCardPhysical
│       ├── Enums/                  ← Rarity, CardType
│       ├── Legality/               ← Format, LegalityMap
│       ├── MagicCard.cs            ← Базовая карта из БД
│       ├── MagicCardPhysical.cs    ← Физический экземпляр карты
│       ├── MagicCardField.cs       ← Enum + extension методы для полей
│       ├── Edition.cs              ← Сет/издание (STH, M19)
│       ├── Location.cs             ← Расположение карты
│       ├── CardGroup.cs            ← Группа карт
│       └── ColorTypes.cs           ← Статический класс цветов
│
├── MagicAssistant.Data.Db          ← Инфраструктурный слой: SQLite + Dapper (net10.0)
│   ├── InitializeDb.cs             ← Статический метод создания всех таблиц и индексов
│   ├── SqliteCardStore.cs          ← Реализация IDbCardStore (База знаний карт)
│   └── SqliteCollectionStore.cs    ← Реализация ICollectionStore + ICardStore<MagicCardPhysical>
│                                   ← JOIN-запросы к Cards для подтягивания полной информации
│
├── MagicAssistant.Data.IO          ← Экспорт/импорт форматов (net10.0)
│   ├── CardCollectionImportResult.cs ← Общий результат парсинга (XML и JSON)
│   ├── json/                       ← Нативный формат Magic Assistant JSON (v1)
│   │   ├── JsonImport.cs           ← Парсинг JSON → IMagicCard[] (reference-ядро id/name/set/count обязательно)
│   │   └── JsonExport.cs           ← IMagicCard[] → JSON (omit-по-умолчанию, pretty)
│   └── xml/                        ← Legacy XML формат Magic Assistant
│       └── XmlImport.cs            ← Парсинг legacy XML (Gatherer ID + полные имена сетов) → IMagicCard[]
│
├── MagicAssistant.Http             ← Контракты доступа к внешним данным + DTOs (net10.0)
│   ├── Contracts/                  ← IScrayfallClient
│   ├── Dtos/                       ← ScryfallCardDto, ScryfallSetDto, и т.д.
│   └── JsonConverters.cs           ← Кастомные JSON-конвертеры
│
└── MagicAssistant.Http.Scryfall    ← Реализация контрактов под Scryfall API (net10.0)
    └── ScryfallClient.cs           ← HTTP-клиент для Scryfall API
```

## Зависимости между проектами

```
MagicAssistant.App
  ├── MagicAssistant.Core
  │     ├── MagicAssistant.Data
  │     ├── MagicAssistant.Data.IO   ← для Import/Export команд
  │     └── MagicAssistant.Http
  ├── MagicAssistant.Data.Db         ← обе реализации хранилищ
  │     ├── MagicAssistant.Data
  │     └── MagicAssistant.Http      ← ImageUrisDto для ImageCacheService
  ├── MagicAssistant.Data.IO
  │     └── MagicAssistant.Data
  └── MagicAssistant.Http.Scryfall
        └── MagicAssistant.Http

MagicAssistant.Core (бизнес-логика)
  ├── MagicAssistant.Data (контракты локальных данных + Model)
  ├── MagicAssistant.Data.IO (JsonImport/JsonExport — нативный формат; XmlImport — legacy)
  └── MagicAssistant.Http (контракты внешних данных + DTOs)

MagicAssistant.Data.Db (реализация БД)
  ├── MagicAssistant.Data (контракты)
  └── MagicAssistant.Http (DTOs — ImageUrisDto для ImageCacheService)

MagicAssistant.Data.IO (импорт/экспорт форматов)
  └── MagicAssistant.Data (Model)

MagicAssistant.Http.Scryfall (реализация Scryfall клиента)
  └── MagicAssistant.Http (контракты + DTOs)

MagicAssistant.Data (контракты + Model)
  └── нет зависимостей от других проектов

MagicAssistant.Http (контракты + DTOs)
  └── нет зависимостей от других проектов
```

### Ключевые правила архитектуры
1. Проект Core работает только с контрактами Data и Http. Ничего не знает о реализациях Http.Scryfall, Data.Db, Data.IO.
2. Http.Scryfall, Data.Db и Data.IO не знают друг о друге. Data.Db и Data.IO знают только о контрактах Data. Http.Scryfall знает только о контрактах Http.


## Единая база данных — `cards.db`

Все данные хранятся в одном SQLite-файле: `{dataPath}/db/cards.db`.

### Таблицы

| Таблица | PK | Описание |
|---------|----|----------|
| **Cards** | `Id` (uniqueidentifier) | База знаний карт (синхронизируется из Scryfall). 30+ колонок: Name, ManaCost, Type, OracleText, Rulings, Artist, SetCode, Rarity, Cmc, ColorType, LegalityFormats и др. |
| **CardsFts** | FTS5 rowid | Виртуальная FTS5 таблица для полнотекстового поиска (CardId UNINDEXED, Name, Type, OracleText, Artist). Триггеры `cards_fts_ai/ad/ud` синхронизируют индекс при INSERT/UPDATE/DELETE в Cards. |
| **CardSet** | `Id` (uniqueidentifier) | Справочник сетов: Name, SetCode, Uri, CardCount, ReleaseDate, SetType, Digital/NonfoilOnly/FoilOnly флаги. |
| **Collection** | `Id` (INTEGER AUTOINCREMENT) | Узлы навигации дерева. NodeType: 0=Folder, 1=Collection, 2=Deck, 3=Sideboard. Locked: BOOLEAN DEFAULT 0. FK ParentId → Collection(Id) ON DELETE CASCADE. Sideboard — дочерний узел колоды (ParentId = deckId). |
| **Deck** | `CollectionId` + `CardId` (composite PK) | Физические карты внутри колод/папок. Count, IsOwn, Comment, DateAdded, ForTradeCount. FK CollectionId → Collection(Id), FK CardId → Cards(Id), оба ON DELETE CASCADE. |
| **OwnCounts** | `CardId` (uniqueidentifier) | Ownership stats: OwnCount, TotalCount, OwnUnique. FK CardId → Cards(Id) ON DELETE CASCADE. |
| **CardFaces** | `Id` (INTEGER AUTOINCREMENT) | Лица многогранных карт (transform/split/flip/mdfc): FaceIndex, Name, ManaCost, TypeLine, OracleText, Power/Toughness, Cmc, Colors, ColorBits, IdentityBits. FK CardId → Cards(Id). |
| **CardAllParts** | `Id` (INTEGER AUTOINCREMENT) | Части составных карт (split/meld): PartIndex, RelatedCardId, Component, Name, TypeLine, Uri. FK CardId → Cards(Id) ON DELETE CASCADE |
| **CardsImageUris** | `CardId` (uniqueidentifier) | Scryfall image URIs для карт: Small, Normal, Large, Png. FK CardId → Cards(Id). |
| **CardFacesImageUris** | `FaceId` (INTEGER) | Scryfall image URIs для лиц карт: Small, Normal, Large, Png. FK FaceId → CardFaces(Id).


**Индексы:**
- `idx_cards_name` — `Cards(Name)` — поиск по имени карты
- `idx_cards_set` — `Cards(SetCode)` — фильтрация по сету
- `idx_cards_rarity` — `Cards(Rarity)` — фильтрация по редкости
- `idx_collection_parent` — `Collection(ParentId)` — навигация по дереву
- `idx_deck_cardid` — `Deck(CardId)` — поиск экземпляров карты
- `idx_cardfaces_cardid` — `CardFaces(CardId)` — лица карт
- `idx_cardallparts_cardid` — `CardAllParts(CardId)` — части карт

### Инициализация

`InitializeDb.EnsureCreatedAsync(dbPath)` — статический метод в `MagicAssistant.Data.Db`, создаёт **таблицы**, FTS5 виртуальную таблицу с триггерами и индексы. Идиомпотентный (`CREATE TABLE IF NOT EXISTS`).

`SqliteCardStore.InitializeAsync()` и `SqliteCollectionStore.InitializeAsync()` — no-op (для соответствия контрактам интерфейсов).

В `Program.cs` инициализация выполняется после `Build()`:

```csharp
// Initialize DB — create all tables and indexes (idempotent)
await InitializeDb.EnsureCreatedAsync(cardsDb);
```

## Модель данных

### MagicCard — базовая карта из БД

Один экземпляр = одна уникальная карта (конкретная печать). `CardId` = Scryfall card ID.

### MagicCardPhysical — физический экземпляр карты в коллекции/колоде

Ссылается на базовую MagicCard. `Location.RawId` = `CollectionId` из таблицы `Deck`.


### MagicCardField — перечисление всех полей карты

Extension методы:
- `field.Get(card)` → получение значения любого поля из `IMagicCard`
- `field.SetM(card, value)` → установка на `MagicCard`
- `field.SetM(physical, value)` → установка на `MagicCardPhysical` (не-physical поля делегируются в базовую карту)
- `field.IsPhysical()` → проверка physical-поля
- `field.IsTransient()` → проверка временного поля (не сохраняется)
- `MagicCardFieldExtensions.AllNonTransientFields(bool physical)` → список полей для итерации
- `MagicCardFieldExtensions.FieldByName(string name)` → поиск по имени с алиасами

### ColorBitFlags — битовые флаги цветов

`[Flags] enum` для эффективной фильтрации по цветам через bitwise операции в SQLite:
- Биты 0-5: C(0x001), W(0x002), U(0x004), B(0x008), R(0x010), G(0x020)
- Бит 6: MULTI(0x040) — мультицвет (2+ цветных бита)
- Бит 7: MONO(0x080) — одноцвет (ровно 1 цветной бит)
- Бит 8: HYBRID(0x100) — гибридная мана ({W/U})
- `ColorBitBuilder.Build(colors, manaCost, identityColors?)` вычисляет битовую маску
- Хранится в колонках `ColorBits` (INTEGER) и `IdentityBits` (INTEGER) таблицы `Cards`
- Для двусторонних карт (transform/modal_dfc/meld/reversible_card/double_faced_token): `ColorBits` и `IdentityBits` в `Cards` = NULL, биты вычисляются для каждого лица и хранятся в `CardFaces.ColorBits` / `CardFaces.IdentityBits`

### Location — расположение карты

Поддерживает два формата ID:
- Integer: `Location.FromId(int id)` → `Id = "123"`, `RawId = 123`
- Legacy string: `Location.FromPath("deck-123")` → `Id = "deck-123"`, `RawId = 0`
- `FromPath` распознаёт суффикс `_sb` для sideboard
- `Equals/GetHashCode` сравнивают по строковому `Id` + `IsSideboard`

## Контракты доступа к данным

### ICardStore<T> — CRUD хранилище карт

### IDbCardStore — специализированное хранилище БЗ карт

### ICollectionStore — управление коллекциями и колодами

## Реализации хранилищ

### SqliteCardStore — База знаний карт

Реализация `IDbCardStore` на SQLite + Dapper.  
Хранит карты MTG и сеты в таблицах `Cards` и `CardSet`.

**Особенности:**
- `ConcurrentDictionary<string, MagicCard>` — L1-кэш для `GetCardAsync()`
- `INSERT OR REPLACE` — UPSERT при синхронизации
- Транзакции для batch-операций (`AddAllAsync`)
- `SearchAsync` — FTS5 full-text search с ранжированием по релевантности
- `GetCardsByIdsAsync` — batch-запрос `WHERE Id IN (@ids)` для эффективного разрешения карт
- `RemoveAsync/RemoveAllAsync` — NotSupportedException (карты БД не удаляются напрямую)

### SqliteCollectionStore — Коллекции и колоды

Реализация `ICollectionStore` + `ICardStore<MagicCardPhysical>` на SQLite + Dapper.  
Работает с той же БД (`cards.db`), таблицы `Collection` и `Deck`.

**Особенности:**
- Константа `DeckCardColumns` — единственный источник списка колонок для всех read-SELECT (GetCardAsync, GetCardsAsync, GetDeckCardsAsync, multi-collection, reconciliation)
- `AttachFacesAsync(conn, physicals, ct)` — батч-подтягивание CardFaces одним запросом после каждого чтения, возвращающего physicals; в `GetAllDeckCardsAsync` (reconciliation) намеренно не вызывается
- `ON CONFLICT(CollectionId, CardId) DO UPDATE SET Count = Count + excluded.Count` — автоматический merge при добавлении дублей
- Транзакции для batch-операций (`AddAllAsync`, `CreateDeckAsync`)
- **JOIN-запросы** ко всем методам чтения: `GetDeckCardsAsync()`, `GetAllAsync()`, `GetCardAsync()` делают `LEFT JOIN Cards` и возвращают `MagicCardPhysical` с полной информацией о базовой карте (имя, манакост, тип, легальность, свойства)
- `GetDeckWithSideboardAsync(deckId)` — включает sideboard (находит дочерний узел NodeType=3 через SQL)
- `CreateDeckAsync()` — создаёт основную колоду (NodeType=2) + sideboard (NodeType=3, ParentId=deckId) в одной транзакции

## Scryfall интеграция

### IScrayfallClient (контракт)

### ScryfallClient (реализация)

HTTP-клиент для `https://api.scryfall.com`.  
Автоматическая пагинация через `has_more` + `next_page` (не Link header).

### ScryfallMapper (бизнес-логика в Core)

Маппит Scryfall DTOs → MagicCard:
- Парсит коллекторские номера из форматов: `"1"`, `"1/234"`, `"42a"`, `"A5"`
- Маппит легальность из Scryfall legalities → LegalityMap (через `FormatMap` dictionary)
- Сохраняет URL изображения в `MagicCard.ImageUrl` свойство

## ImageCacheService — кэширование картинок карт

Скачивает и сохраняет картинки карт локально.

### Структура директорий

**Шаблон:** `{imagesDir}/{set-abbreviation}/{language}/{front|back}/{card-uid}.png`

| Карта | Язык | Сторона | UID | Путь |
|-------|------|---------|-----|------|
| Amonkhet #1 | `en` | `front` | `D89203BA-...` | `afr/en/front/D89203BA-8EBA-41F5-8CC3-DF3327F577A9.png` |
| Flip #42 (сторона A) | `en` | `front` | `AAAA1111-...` | `m25/en/front/AAAA1111-BBBB-CCCC-DDDD-EEEEFFFF0000.png` |
| Flip #42 (сторона B) | `en` | `back` | `BBBB2222-...` | `m25/en/back/BBBB2222-CCCC-DDDD-EEEE-FFFF00001111.png` |

**Подготовка директорий:** `CreateSetLanguageDirs(setCode, languages[])` создаёт `{set}/{lang}/front` и `{set}/{lang}/back` для каждого языка.

**Приоритет URL:** `normal` → `large` → `png` → `small`

**Многогранные карты:** определение стороны по URL (`/front/` или `/back/`). Если URL не содержит маркера — пропускается.

**Проверка существующих:** `GetExistingCardUidsForSet(setCode)` рекурсивно сканирует `{set}/` папку, извлекает UID из имени файла (`.png` → Guid.TryParse). При синхронизации `card.CardId` ∈ existing UIDs → скачивание пропускается.

**Front/Back разделение:** 
- Обычные карты → `front/{uid}.png`
- Многогранные карты → `front/{uid}.png` для передней, `back/{uid}.png` для задней стороны

### Ленивая загрузка картинок (on-demand)

UI не требует заранее скачанных картинок: если файла нет на диске, он скачивается индивидуально в момент, когда пользователь видит карточку.

- **Точки рендера** (все через `ImageHelper.GetImageUrl` → `GET /api/images/{set}/{lang}/front|back/{uid}.png`): `CardDescriptionView`, `CardGalleryView`, `LastActions`, `PrintingsView`, `OldDeckImportWizard` (ховер-превью).
- **Обнаружение «пустоты»** — серверная проверка `IImageCacheService.FrontImageExists/BackImageExists` (то же условие, при котором контроллер отвечает 404). `<img>` рендерится только когда файл на диске; иначе — плейсхолдер (⏳ во время скачивания, «нет картинки» после провала).
- **`DownloadCardImagesCommand(CardId)`** — индивидуальная команда: `IDbCardStore.GetCardAsync` перечитывает карту из KB **с** `ImageUris`/`CardFaces`, затем `IImageCacheService.DownloadAsync(card)` скачивает все грани тем же кодом, что и фаза «Images» синка сета. Обращений к Scryfall API нет — только файлы изображений.
- **`ICardImageLoader`** (App) — обёртка для UI: `FrontExists/BackExists/Failed` + `EnsureAsync(card)`. Конкурентные запросы на одну карту сериализуются через `ConcurrentDictionary<Guid, SemaphoreSlim>`: семафор создаётся один на карту и живёт в словаре до жизни лоадера (каждый конкурент ждёт тот же экземпляр — каждый Release соответствует acquire). Первый захватывает его и скачивает, остальные ждут разблокировки; войдя в секцию, каждый сначала проверяет наличие файла — если предыдущий конкурент успел скачать, выход без повторного скачивания. Провалы запоминаются (карта без картинок не запускает повторный даунлоад на каждый ререндер).
- **Построение:** компонент обнаруживает отсутствующие файлы → fire-and-forget батч `EnsureAsync` (параллельно, по одному на карту) → после каждого завершения `StateHasChanged()` → картинка появляется в сетке/вью без перезагрузки страницы.

## CQRS — Commands и Queries

### Commands (модифицирующие операции)

| Команда | Описание | Делегирует в сервис |
|---------|----------|---------------------|
| `CreateNodeCommand` | Унифицированное создание узла (Folder/Collection/Deck; Deck → + sideboard в одной транзакции) | — |
| `AddCardsToDeckCommand` | Добавление карт в колоду (конвертация IMagicCard → MagicCardPhysical) | — |
| `RemoveCardsFromDeckCommand` | `(int DeckId, ICollection<Guid> CardIds)` — DeckId = ID самого узла; делегирует в `ICollectionStore.RemoveCardsAsync`: 1 SQL-запрос (`DELETE ... RETURNING Count`, результат = сумма возвращённых копий, а не число строк), непустой список: удаление ВСЕХ копий карт, пустой: чистка всех карт узла (узел остаётся). Используется `DeckCardService.RemoveCardsAsync` / `ClearNodeAsync(nodeId)` (CQRS) | — |
| `MoveCardsCommand` | Перемещение карт между коллекциями: удаление из источника + добавление в цель (merge-семантика ON CONFLICT) | — |
| `UpdateCardCommand` | Обновление свойств карты (count, ownership, price, comment, tags) | — |
| `ImportResolvedCardsCommand` | Атомарный коммит полностью зарезолленного импорта: new-режим → `CreateDeckWithCardsAsync` (одна транзакция), existing-режим → merge. CardOverrides — выборы из таблицы нераспознанных карт |
| `SyncKBCommand` | Полная синхронизация сета из Scryfall (оркестрирующая команда) | `IKnowledgeBaseSyncService` |
| `SaveUserSettingCommand` | Сохранение пользовательских настроек (splitter, view mode, page size) | — |

### Queries (чтение)

| Query | Описание |
|-------|----------|
| `GetDeckCardsQuery` | Получить все карты колоды через `ICollectionStore.GetDeckCardsAsync()` с SQL JOIN к Cards |
| `SearchCardsQuery` | Поиск карт в БД (LIKE по name/type/oracleText/artist) |
| `GetScryfallAvailableSetsQuery` | Список доступных сетов из Scryfall (кэш 30 минут) |
| `GetKBAvailableSetsQuery` | Сеты, присутствующие в БЗ (формы выбора сета SyncKnowledgeBase / SyncSelectedSetCards; сайдбар CardsListControl использует `FilteredCardSetsCollection` → `IDbCardStore.GetCardSetsAsync`) |
| `HasCardsQuery` | Есть ли в БД хотя бы одна карта сета (`IDbCardStore.HasCardsForSetAsync` = `SELECT EXISTS`) — ведёт пустое состояние CardsListControl: синк-кнопка только когда кликнутый сет реально пуст в БД |
| `GetSyncProgressQuery` | Polling прогресса синхронизации через SyncSetStateRegistry |
| `GetNodesTreeQuery` | **Один SQL-запрос**: все узлы дерева за раз. Ключи словаря — root-узлы; вложенные узлы — в `Children` папок. У deck-узлов заполнен `Sideboard`. Сортировка на .NET: Collection → Deck → Folder |
| `GetNodeByIdQuery` | Один узел по ID |
| `GetNodesBranchQuery` | Поддерево с корнем в заданном узле (рекурсивный SQL). Используется для резолва sideboard при Move-cards |
| `GetCardByIdQuery` | Карта БЗ по ID |
| `GetCardPrintingsQuery` | Все печати карты (по имени) |
| `GetCardLanguagesQuery` | Языковые версии карты (переключение в CardDescriptionView) |
| `GetCardInstancesQuery` | Физические экземпляры карты по всем коллекциям |
| `GetCardSetQuery` | Метаданные сета (полное имя, дата релиза) |
| `GetDeckAnalysisQuery` | Однопроходный анализ колоды через `IAnalysisService` |
| `GetCollectorProgressQuery` | Прогресс коллекционера по сетам (Total — `CardSet.CardCount`; Collected/Owned — distinct `CollectorNumber`, языковые дубли схлопнуты) |
| `GetUserSettingQuery` | Чтение пользовательских настроек (UI-параметры) |
| `GetSetSyncStatusQuery` | Статус синка полных имён сетов одним SQL: в CardSet? сколько карт в Cards — детект «сет без карт» для legacy-предпроверки |
| `ResolveLegacyCardsQuery` | Батч-резолв legacy-карт (read-only): стрип `(Face)` после `//`, имена сетов → коды, (Name, Set) → Scryfall UUID — точное совпадение по всем полям имени (Cards/CardsFaces: Name + PrintedName), локальные имена работают; результат {Resolved: physicals с реальными ID, Unresolved: строки для ручного сопоставления} |
| `ValidateCardIdsQuery` | Known/Unknown по БЗ одним батч-запросом — инвариант modern-импорта: все карты должны быть в БЗ до создания узла |

## SyncKBCommand — Синхронизация базы знаний

Оркестрирующая команда, делегирующая в `IKnowledgeBaseSyncService`.

### Этапы выполнения

1. **Phase 0: Resolve set** — поиск информации о сете в Scryfall
2. **Phase 1: Download** — загрузка списка карт сета через `IScrayfallClient.GetCardsAsync(setCode, englishOnly)`
3. **Phase 2: Map** — маппинг DTO → MagicCard через `ScryfallMapper`
4. **Phase 3: Images** — скачивание картинок через `IImageCacheService.DownloadAsync()`, проверка существующих по UID
5. **Phase 4: Save** — сохранение в KnowledgeBase через `IDbCardStore.AddAllAsync()`
6. **Phase 5: Done** — завершение, отчёт

### Прогресс-трекинг

`SyncSetState` (thread-safe) + `SyncSetStateRegistry` (singleton):

Поллинг через `GET /api/sync/progress?set={abbrev}` → JSON `SyncProgressResponse`.

### Результат

## Форматы импорта/экпорта (Data.IO)

Два формата: **нативный JSON (v1)** — основной для UI-импорта/экспорта, и **legacy XML** — только OldDeckImportWizard (Gatherer ID + полные имена сетов). Оба возвращают общий `CardCollectionImportResult`.

### JsonImport / JsonExport (нативный формат)
- Обёртка: `format: "magic-assistant/deck"`, `version: 1`, `name`, `comment`, `type: "deck"`, `exportedAt` (ISO 8601) — всегда пишутся
- Карта: reference-ядро `id` (Scryfall UUID), `name`, `set`, `count` — всегда; импорт требует все четыре (отсутствие = FormatException, дефолт для count не ставится)
- Опциональные поля omit-по-умолчанию: `own` только false, `price` ≠0 (короткое round-trip представление float), `comment`/`custom`/`special` непустые, `forTrade` >0 и без тега fortrade
- Pretty-print с отступом 2; неизвестные поля при импорте игнорируются (forward-compat); чужой format/несоответствующий version → FormatException

### XmlImport (legacy)
- Парсит legacy XML: `<cards>` → `<list>` → `<mcp>` (MagicCardPhysical); Gatherer ID + полные имена сетов
- Поддерживает только физические карты (`<mcp>`); базовые карты `<mc>` не импортируются
- `ReadElementValue()` — различает простые элементы и элементы с дочерними узлами

## DI-конфигурация (Program.cs)

**Singleton:**

| Сервис | Реализация |
|--------|-----------|
| `IDbCardStore` | `SqliteCardStore` (`cards.db`) |
| `ICollectionStore` | `SqliteCollectionStore` (`cards.db`) |
| `ICardStore<MagicCardPhysical>` | `SqliteCollectionStore` (`cards.db`) |
| `IUserSettingsStore` | `SqliteUserSettingsStore` (`cards.db`) |
| `IImageCacheService` | ImageCacheService (factory) |
| `ISetIconsCacheService` | SetIconsCacheService (factory) |
| `CardSetsCache`, `AllCardSetsCache` | Кэши метаданных сетов |
| `SyncSetStateRegistry` | Thread-safe прогресс-трекер синхронизации |
| `IAnalysisService` | `AnalysisService` (однопроходный анализ) |

**Scoped:**

- Все Command/Query handlers: `AddCardsToDeckCommandHandler`, `GetDeckCardsQueryHandler`, `SearchCardsQueryHandler`, `HasCardsQueryHandler`, `SyncKBCommandHandler`, `GetScryfallAvailableSetsQueryHandler`, `GetSyncProgressQueryHandler`, `GetSetSyncStatusQueryHandler`, `ResolveLegacyCardsQueryHandler`, `ValidateCardIdsQueryHandler`, `ImportResolvedCardsCommandHandler`, `RemoveCardsFromDeckCommandHandler`, `MoveCardsCommandHandler`, `UpdateCardCommandHandler`, `CreateNodeCommandHandler`, `DeleteNodeCommandHandler`, `RenameNodeCommandHandler`, `LockNodeCommandHandler`, `UnlockNodeCommandHandler`, `GetDeckAnalysisQueryHandler`, `GetNodesTreeQueryHandler`, `GetNodeByIdQueryHandler`, `GetNodesBranchQueryHandler`, `GetCardByIdQueryHandler`, `GetCardPrintingsQueryHandler`, `GetCardLanguagesQueryHandler`, `GetCardInstancesQueryHandler`, `GetKBAvailableSetsQueryHandler`, `GetCardSetQueryHandler`, `SyncCardSetsCommandHandler`, `GetCollectorProgressQueryHandler`, `SaveUserSettingCommandHandler`, `GetUserSettingQueryHandler`
- Вертикальные сервисы: `IDeckManagementService`, `IDeckCardService`, `ICardResolveService`, `IKnowledgeBaseSyncService`, `IDeckImportExportService`, `ICardSearchService`, `IDeckAnalysisService`
- `KbRefreshService` — подписка/уведомление UI о завершении синхронизации БЗ

## UI — Blazor Server

### MainLayout.razor
Трёхпанельная раскладка (левая — навигатор, центральная — контент с табами, правая — описание карты + печати + экземпляры).

Вверху центральной панели — `ContextStrip` (`Shared/ContextStrip.razor`): полоса из трёх секций — слева selected, в центре золотая стрелка SVG 24×24 (указывает вправо), справа pinned. Selected всегда заполнен: selected-коллекция либо KB-вариант «KnowledgeBase Cards» без статистики. Pinned — только deck/collection/sideboard; пусто → «? (pin to set)» жирным серым 24px. Названия — жирные золотые 24px, прижаты к центру (у selected вправо, у pinned влево), так что в центре склеиваются «Selected Name → Pinned Name»; статистика «(x/y)» с тултипом «unique: x, total: y» разнесена по внешним краям (у selected влево, у pinned вправо). Статистика: `IDeckCardService.GetDeckCardsAsync(collectionId)` (unique = число уникальных карт, total = сумма копий); перечитывается при смене контекстов и по `_stripRefresh++` (`RecordLastActionAsync` — +/−, KB-добавление, Move; `OnEditCardSaved`; `OnSplitDone`; `OnImportCompleted`; `OnLegacyImportCompleted`). SearchControl вынесен из центральной панели в верх окна CardFilterDialog.

Header-действия: `Legacy Import`, `Update DB` (Import перенесён в add-node меню навигатора с контекстом родителя — `ParentNodeId` → DeckImportWizard; Export — в контекстное меню ⋯, экспорт кликнутого узла через `_exportTargetId`). Оверлей-диалоги управляются state-флагами (`_showXxx`) и рендерятся только при видимости; закрытие — только кнопкой ✕ (и/или Cancel + Escape где был), клик по оверлею ничего не делает. Status bar в нижней части центральной панели.

Content area (`@key="_currentCollectionId"`): deck-режим — TabBar (Cards + analysis pages) через RenderFragment `RenderDeckContent()`; KB-режим — `RenderKBContent()`. Переключение режима: `_currentCollectionId` + `FilterViewModel.CurrentCollectionId`.

Модель current/pinned: два независимых контекста — `_currentCollection` (`LockableCollection`: что отображается в центральной панели) и `_pinnedCollection` (прибитая цель операций: LastActions, KB «+», quick Move). Единственный писатель pinned — кнопки Pin навигатора; выбор узла (`OnDeckSelected`) ставит только current, очередь LastActions никогда не сбрасывается — каждая запись несёт свою метку Location. Прыжок из InstancesView (`OnDeckSelectedFromInstance`) меняет только current; `OnShowKB` сбрасывает только current (pinned и очередь живут). Ghost-валидация: `OnNodeDeleted` проверяет узлы через `IDeckManagementService.GetByIdAsync` и сбрасывает пропавшие контексты (покрывает прямое удаление и каскад «удалил папку с прибитой колодой внутри»). Lock-состояние: `IsLocked` хранится в обоих `LockableCollection` (снапшот на момент выбора) и обновляется по событию `OnNodeLockToggled` из DecksNavigator; блокировка узла снимает его пин. Locked-коллекции не принимают карточные операции — кнопки −/+, (+), Move, ✎, ⫯ в UI скрыты.

### CardsListControl.razor
Единый браузер карт для обоих режимов: KB (`CurrentCollectionId == null`) и deck/локация (`CurrentCollectionId` установлен).

- Сайдбар сетов с resizable splitter (JS ES Module `/js/splitter.js`, 160–500px, позиция персистится в user settings).
- Toolbar: name filter (debounce 300ms, 3+ символов), view mode (list/gallery/tree, персистится), multi-select Type/Rarity, кнопка Filters (CardFilterDialog), `📦 Move` — только когда отображаемая коллекция отличается от pinned и ни одна из них не locked (`CurrentCollectionId ≠ null && PinnedCollectionId.HasValue && CurrentCollectionId ≠ PinnedCollectionId && !CurrentCollectionLocked && !PinnedCollectionLocked`).
- Сортировка — по заголовкам столбцов CardListView (Name/CMC/Rarity/Set/Type): клик = переключение сортировки на столбец (по возрастанию, если не по нему), повторный клик по тому же заголовку = смена направления; активный заголовок подсвечен (▼/▲). Состояние — в `FilterViewModel.SortField`/`SortAsc`, канал тот же (`Apply()` → `Changed`).
- Серверная пагинация: `ICardSearchService.GetPagedAsync(CardListFilter)`; page size 1–150 (персистится).
- Deck-режим: physical instances через `IDeckCardService.GetDeckCardsAsync` — count −/+, ✎ edit, ⫯ split (Count > 1), выбор карты → правая панель.
- Quick Move (`📦 Move`): `OnMoveToPinned(physical)` → MainLayout вызывает `IDeckCardService.MoveCardsAsync(currentCollectionId, [cardId], pinnedCollectionId)` (карта уезжает со всеми копиями) + запись в `LastActionsQueue` (Add, число перемещённых копий) + `_kbRefreshTrigger++` (перезагрузка списка).
- KB-режим с установленным pinned: строки показывают только «+» (`KbAddMode`, скрыт при locked pinned) → `IDeckCardService.MaterializeFromIdsAsync(pinnedCollectionId, [cardId])` (merge-семантика) + запись в очередь. Левый столбец **Total**: в KB-режиме с pinned — количество копий карты в pinned-коллекции (один GROUP BY через `IDeckCardService.GetDeckCardCountsAsync`; 0 → пусто, ширина фиксирована; обновляется локально при «+», перечитывается при смене pinned); в deck-режиме заменяет старую колонку count — счётчик −/+ (read-only при locked), всегда > 0, т.к. карта с нулём исчезает из колоды; колонка # в deck-режиме скрыта. Операции −/+ в deck-режиме фиксируются в очереди всегда — с меткой локации (имя current); при locked current кнопки −/+, ✎, ⫯ скрыты (count показывается read-only); очередь не сбрасывается, записи из разных коллекций сосуществуют.
- Sync доступен только в KB-режиме — явный guard `IsLocationMode` (`CurrentCollectionId.HasValue`): кнопка «Sync from Scryfall» в пустом состоянии рендерится только при `!IsLocationMode` **и** когда кликнутый сет реально пуст в БД (прямой запрос `HasCardsQuery`, без вывода из фильтров: пустой список при картах в сете = «спрятали фильтры», кнопка не показывается). `OnSyncClick()` в location mode инертен, а `RenderDeckContent()` не передаёт `OnOpenSync` вовсе (collection/deck/sb — только отображение карт из БЗ). Header-действие «Update DB» остаётся видимым во всех режимах (глобальный хром — решение Рона).
- Список сетов — ленивое DB-представление `FilteredCardSetsCollection` (Shared/): кэш по составу скоупа `FilterViewModel.FilteredSetCodes`, первый запрос к БД при первом рендере / смене состава, `Invalidate()` после синка БЗ. Location-режим: при загрузке узла дистинктные `SetCode` карт уходят в `FilterViewModel.SetNodeScope(codes)` (авто-скоуп); явный фильтр Sets в Card Filter доступен только в KB-режиме (`SetFilteredSetCodes()`), при переходе в location-узел он обрезается до карт узла (ничего не осталось → возврат к полному скоупу узла). Клик по сету сайдбара — `SetActiveCardSets(code)` (ровно один код, null = сброс); поисковый текст сужает только видимый список сетов, на карточки не влияет. Запрос берёт `FilterViewModel.GetEffectiveSetCodes()` (клик ∩ скоуп; пустое пересечение = пустой список → ноль карт). Счётчики в шапке: location — `sets: _filteredSets.Count`, `cards:` число уникальных карт узла (ключи `_physicalByCardId`), суженное по выбранному сету при его наличии, `total:` сумма копий (`SUM(Count)`); KB — `1` при выбранном сете, иначе число сетов скоупа; cards — `CardCount` выбранного сета или `_totalCount`.

### SyncKnowledgeBase.razor
Компонент синхронизации БЗ: выпадающий список сетов, чекбокс "Download images", кнопка Sync, прогресс-бар, отчёт с детализацией.

### Blazor компоненты
Все компоненты реализованы. Статус и детальное описание — в [`ui.md`](./ui.md).

## FilterViewModel — единый канал фильтрации и сортировки

*Файл:* `MagicAssistant.App/Shared/FilterViewModel.cs`

`FilterViewModel` — центральный POCO-класс, инкапсулирующий всё состояние фильтрации и сортировки для KB- и deck-режимов. Все UI-компоненты, участвующие в фильтрации (SearchControl, CardsListControl, CardFilterDialog), мутируют один экземпляр этого класса и триггерят событие `Changed` для синхронизации.

### Свойства

| Свойство | Тип | Описание |
|----------|-----|----------|
| `FilteredSetCodes` | `List<string>` | Скоуп списка сетов: автозаполнение location-режима (коды карт узла, `SetNodeScope()`) или явный фильтр Sets в Card Filter (только KB, `SetFilteredSetCodes()`). Пустой = все сеты. `Clear()` сбрасывает только в KB-режиме |
| `ActiveCardSets` | `List<string>?` | Кликнутый сет сайдбара — ровно один код или null. `SetActiveCardSets(code)`; «All Sets»/✕/`Clear()` → null. Запрос берёт `GetEffectiveSetCodes()`: клик ∩ скоуп (OrdinalIgnoreCase); пустое пересечение = пустой список = «нет кандидатных сетов» (ноль карт в SQL), отличимо от null («фильтрация по сетам не применяется») |
| `SetSearchText` | `string` | Текст поля «Filter sets...». Сужает только видимый список сетов сайдбара — на карточки не влияет. Переживает пересоздание компонента (`@key`-смена при навигации). Сбрасывается в `Clear()` |
| `Rarity` | `List<string>` | Редкости (multi-select). OR-выражение: "common OR rare". Методы: `SetNewRarityFilter()`, `GetRarityOrExpression()` |
| `NameFilter` | `string` | Фильтр по имени (3+ символов, debounced) |
| `SearchQuery` | `string` | FTS5-запрос из SearchControl (`"phrase"`, `name:term`, OR/AND/NOT) |
| `TypeFilter` | `List<string>` | Типы карт (multi-select). OR-выражение: "creature OR land". Методы: `SetNewTypeFilter()`, `GetTypesOrExpression()` |
| `Artist` | `string` | Имя художника |
| `ColorType` | `string` | Значение color-type фильтра |
| `ColorFilters` | `Dictionary<ColorBitFlags, bool>` | 9 чекбоксов: C/W/U/B/R/G/MULTI/MONO/HYBRID. `ColorFilterHelper.GetFilter()` строит SQL-маску |
| `ColorAndMode` / `ColorOnlyMode` / `ColorIdentityMode` | `bool` | Модификаторы цветовых фильтров |
| `PowerOp`/`PowerValue`, `ToughnessOp`/`ToughnessValue`, `CmcOp`/`CmcValue` | `string` | Свойства P/T/CMC: оператор (`>=`, `=`, `<=`) + значение |
| `LayoutFilter` | `string` | Фильтр по layout (например, двусторонние карты) |
| `LegalityFilter` | `string` | Фильтр по легальности |
| `GameChangerFilter` | `bool` | Флаг game-changer карт |
| `Ownership` | `string` | Collection-фильтр: "all"/"owned"/"borrowed" → `Deck.IsOwn`. Только deck-режим |
| `MinCount` / `MaxCount` | `string` | Collection-фильтр: диапазон количества карт в локации (парсится при построении запроса) → `Deck.Count`. Только deck-режим |
| `MinPrice` / `MaxPrice` | `string` | Collection-фильтр: диапазон цены USD (парсится при построении запроса) → `CAST(Cards.Usd AS REAL)`. Только deck-режим |
| `IsFoil` | `bool` | Collection-фильтр: передаётся в Data-слой, в SQL пока не используется (нет колонки foil-флага) |
| `HasForTrade` | `bool` | Collection-фильтр: только карты с `Deck.ForTradeCount > 0`. Только deck-режим |
| `HasComment` | `bool` | Collection-фильтр: только карты с непустым `Deck.Comment`. Только deck-режим |
| `CurrentCollectionId` | `int?` | ID отображаемого узла навигации (колода/коллекция/sideboard). Навигационное состояние, не фильтр — исключён из `Clear()`. Устанавливается MainLayout при выборе в навигаторе / прыжке из InstancesView, сбрасывается при переходе в KB. Переключает CardsListControl между KB- и deck-режимами |
| `SortField` | `string` | Поле сортировки: Name/CmcNumber/Type/SetCode/Rarity (совпадает с `CardListFilter.SortFields`) |
| `SortAsc` | `bool` | Направление сортировки |
| `ClearGeneration` | `int` | Инкрементируется при каждом `Clear()` — подписчики по нему детектят внешний сброс и синхронизируют локальные вводы (debounce-буфер имени, поиск по сетам) |

### Событие и методы

| Член | Подпись | Описание |
|------|---------|----------|
| `Changed` | `event Action?` | Срабатывает при `Apply()` — подписчики делают re-query через `InvokeAsync()` |
| `Apply()` | `void` | Триггерит `Changed?.Invoke()` |
| `ClearAndApply()` | `void` | Вызывает `Clear()` → `Apply()` |
| `Clear()` | `void` | Сбрасывает все поля к значениям по умолчанию без уведомления |

### Архитектура потока данных

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
│              │  │ (click select)   │  │       │ Layout/Legality/Game    │
│              │  │ Sort headers ────┼──┘       │ Changer ────────────────┼──┤
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

### Источники изменений (все через один канал)

| Источник | Свойство ViewModel | Механизм |
|----------|-------------------|----------|
| SearchControl (FTS5, вверху окна CardFilterDialog) | `SearchQuery` | Enter / 🔍 → `SearchQuery = query` → `Apply()`; ✕ / Escape → очистка → `Apply()` |
| Type dropdown (toolbar, multi-value) | `TypeFilter` (List<string>) | `onchange` → `SetNewTypeFilter()` / `SetTypeFilters()` → `Apply()` |
| Rarity dropdown (toolbar, multi-value) | `Rarity` (List<string>) | `onchange` → `SetNewRarityFilter()` → `Apply()` |
| Name input (debounced 300ms) | `NameFilter` | timer / Enter / Clear → `Apply()` |
| Set sidebar text input | `SetSearchText` | Ввод (2+ символов) сужает видимый список сетов — без re-query карточек; текст переживает remount через `FilterViewModel.SetSearchText` |
| Set sidebar selection | `ActiveCardSets` (List<string>?) | Клик по сету → `SetActiveCardSets(code)` + `Apply()`; «All Sets»/✕ → `SetActiveCardSets(null)` + `Apply()` |
| Заголовки столбцов CardListView (list view) | `SortField`, `SortAsc` | Клик = новый столбец (asc), повторный клик = смена направления → `Apply()` |
| MainLayout navigation | `CurrentCollectionId` | `OnDeckSelected(LockableCollection)` → `= CollectionId` (pinned не трогается; очередь не сбрасывается) → `Apply()`; `OnDeckSelectedFromInstance(deckId)` → `= deckId` (только current) → `Apply()`; `OnShowKB()` → `= null` (pinned живёт) → `Apply()` |
| CardFilterDialog → Apply | all fields (Type/Rarity via MultiSelectList, ColorFilters dict, Properties; Collection: Ownership/Count/Price/Tags — только при CurrentCollectionId) | `Apply()` + close dialog |
| CardFilterDialog → Clear All | all fields | `ClearAndApply()` + close dialog |

### Исключения (прямые вызовы, не через канал)

| Источник | Причина |
|----------|---------|
| Pagination (Prev/Next) | Навигация внутри результатов, не изменение фильтров |
| `RefreshTrigger` parameter | Внешний триггер после завершения sync KB |

### Миграция от старого паттерна

**Было:** `FilterResult` → `ActiveFilter` parameter → `OnParametersSetAsync` → client-side фильтрация текущей страницы.

**Стало:** `FilterViewModel` → `event Changed` → server-side re-query через `CardListFilter` → полная перезагрузка данных.

---

## Цветовые фильтры (ColorFilter)

Фильтрация по цветам через битовые маски в SQL:
- `ColorFilter.RequiredMask` — биты которые ДОЛЖНЫ присутствовать
- `ColorFilter.ExcludedMask` — биты которые НЕ ДОЛЖНЫ присутствовать (Only/Identity mode)
- `AndMode` — все выбранные цвета должны быть (AND), иначе любой (OR)
- `IdentityMode` — фильтрация по `IdentityBits` вместо `ColorBits`, имплицирует Only

SQL-перевод:
- OR mode: `(ColorBits & requiredMask) != 0`
- AND mode: `(ColorBits & requiredMask) = requiredMask`
- Excluded: `(ColorBits & excludedMask) == 0`

**DFC-поддержка** (`ToSqlWhereWithDfcSupport()`, production-метод — используется в `SqliteCardStore.GetPagedAsync`): у двусторонних карт `ColorBits` в `Cards` = NULL, поэтому фрагмент имеет форму `((cardCondition) OR (Id IN (SELECT CardId FROM CardFaces WHERE faceCondition)))`. Инвариант генерации: любой фрагмент, чей верхний уровень — OR, обязан быть обёрнут во внешние скобки, потому что `GetPagedAsync` склеивает whereParts через AND. Тождественная обёртка — в `NumericPropertyFilter.ToSqlWhereWithDfcSupport()` (Power/Toughness/CMC: вместо IN-подзапроса — `cardNumberColumn IS NULL AND EXISTS(...)`).

Роли SQL-методов: `ToSqlWhereWithDfcSupport()` — production-путь (GetPagedAsync); `ToSqlWhere()` — тестовый вариант генерации (используется только в ColorFilterHelperTests / ColorFilterRoundTripTests / ColorFilterSqlIntegrationTests).

`ColorFilterHelper.GetFilter(checkboxes, andMode, onlyMode, identityMode)` строит фильтр из UI-состояния.

## Фильтры и выражения (MagicCardFilter, Expr)

Система построена на AST-дереве выражений с поддержкой логических операций, сравнений и regex-поиска.

### Архитектура

```
Expr (абстрактный базовый класс)
├── TrueExpr / FalseExpr / EmptyExpr  ← оптимизированные константы
│   └── And/Or/Not — короткое замыкание: True.And(b)=b, False.Or(b)=b
├── BinaryExpr  ← AND, OR, EQUALS, MATCHES, GE(>=), LE(<=), EQ(==)
├── NotExpr     ← унарное NOT (обёртка над Expr)
├── CardFieldExpr  ← обёртка MagicCardField → ICardField
└── Value / TextValue  ← константные значения (TextValue: wordBoundary, caseSensitive, regex)
```

### Ключевые файлы

| Файл | Роль |
|------|------|
| `Expressions/Expr.cs` | Базовый класс: `True`, `False`, `Empty`, методы `And()`, `Or()`, `Not()` |
| `Expressions/BinaryExpr.cs` | Двоичные выражения + статические фабрики `FieldEquals`, `FieldMatches`, `FieldOp`, `FieldInt`, `TextSearch` |
| `Expressions/CardFieldExpr.cs` | Обёртка `MagicCardField` → `ICardField` для извлечения значения поля из карты |
| `Expressions/Value.cs` / `TextValue.cs` | Константные значения. `TextValue` поддерживает word boundary, case sensitivity, regex |
| `Expressions/NotExpr.cs` | Унарное отрицание |
| `Expressions/SearchStringTokenizer.cs` | Парсер строки поиска: токены текста, `"quoted"`, `\regex:...`, `!NOT` |
| `Expressions/Abilities.cs` | известные способности MTG (keyword + mined) с regex-паттернами |
| `MagicCardFilter.cs` | Главный класс фильтра: хранит AST (`root`), сортировку, группировку. `FilterCards()`, `Update()` |

### Пайплайн работы

```
Сырой текст → SearchStringTokenizer → Token[] → BinaryExpr (MATCHES/AND) → Expr root
     ↓
MagicCardFilter.FilterCards(cards) → root.Evaluate(card) для каждой карты
```

- `True`/`False`/`Empty` — оптимизация: `True.And(b)` → `b`, `False.Or(b)` → `b` (короткое замыкание)
- `MATCHES` делегируется `card.Matches(field, pattern)` для полей Name/Type/Set/Rarity, иначе — regex на строковом представлении
- `EQUALS` сравнивает значения полей (string equality + numeric с float tolerance `0.001f`)
- `>=`, `<=`, `==` парсят float и сравнивают численно
- `TextSearch` токенизирует ввод: `!word` → NOT, `\regex:...` → regex режим, пробелы → AND

### ISearchableProperty — групповая фильтрация

```csharp
public interface ISearchableProperty
{
    string GetFilterField();
    IEnumerable<string> GetIds();
    string GetNameById(string id);
}
```

Реализации: `ColorTypesSearch` (W/U/B/R/G/Multicolor/Colorless), `Editions`, `Rarity`, `Locations`. Используются в `MagicCardFilter.CreateGroup()` для построения OR/AND-групп из карты параметров.

### IMagicCard.Matches

```csharp
bool IMagicCard.Matches(ICardField field, string pattern);
```

Делегирует специфичную логику сопоставления:
- Name/Type → contains (case-insensitive)
- Set/Rarity → exact match (case-insensitive)
- Остальные поля → contains fallback

## Агрегаторы (Analysis)

### Архитектура

Единый сервис `IAnalysisService` вычисляет все метрики за **один проход**.

```
IAnalysisService
├── Analyze(IEnumerable<MagicCardPhysical>) → DeckAnalysisResult
│   ├── ManaCurve (int[8]) — CMC 0-7+ для не-земель
│   ├── ColorDistribution (Dictionary<string,int>) — W/U/B/R/G/Colorless
│   ├── TypeDistribution — Land / Creature / NonCreature
│   ├── CreatureSubtypes (Dictionary<string,int>) — подтипы существ
│   ├── AbilityStats (Dictionary<string,int>) — keyword abilities
│   └── BasicStats — TotalCards, TotalManaCost, MaxRepeats
└── ComputeManaCurve(IEnumerable<MagicCardPhysical>) → int[8]
```

### Ключевые файлы

| Файл | Роль |
|------|------|
| `Analysis/IAnalysisService.cs` | Интерфейс сервиса анализа |
| `Analysis/AnalysisService.cs` | Однопроходная реализация |
| `Analysis/DeckAnalysisResult.cs` | Immutable result object |
| `Queries/GetDeckAnalysisQuery.cs` | Query + Handler для анализа колоды |

### Blazor компоненты

Все analysis-компоненты получают данные через DI: `IAnalysisService` (однопроходные метрики) + `ICollectionStore` (карты колоды). `DeckLegalityPage` использует только `ICollectionStore`:

| Компонент | Что показывает |
|-----------|---------------|
| `ManaCurvePage.razor` | Столбчатая диаграмма CMC 0-7+ + средний CMC |
| `SpellColourPage.razor` | Прогресс-бары распределения по цветам |
| `CreaturePage.razor` | Таблица подтипов существ |
| `TypePage.razor` | Распределение Land/Creature/Other |
| `AbilityPage.razor` | Таблица keyword abilities |
| `InfoPage.razor` | 8 карточек: total cards, mana cost, max repeats, colors, lands, creatures |
| `DeckLegalityPage.razor` | Легальность колоды по форматам |

`TabBar` в MainLayout переключает analysis-компоненты; каждый получает `DeckId`.

## Текущее состояние

### ✅ Реализовано: вертикальные срезы

**Архитектура вертикальных срезов:**
- Доменные сервисы: `IDeckManagementService`, `IDeckCardService`, `ICardResolveService`, `IKnowledgeBaseSyncService`, `IDeckImportExportService`, `ICardSearchService`, `IDeckAnalysisService`
- Вся бизнес-логика — в вертикальных срезах
- Blazor-компоненты используют только вертикальные сервисы (не Data-контракты напрямую)

**Контракты:**
- `IDbCardStore.HasCardsForSetAsync(setCode)` — `SELECT EXISTS` по Cards (ведёт синк-кнопку пустого состояния)
- `OwnCountStats` record — `{ CardId, OwnCount, TotalCount, OwnUnique }`
- `ICollectionStore.GetOwnCountStatsAsync()` — один SQL GROUP BY для всех own_count
- `IDbCardStore.BulkUpdateOwnCountAsync()` — bulk persist в OwnCounts table
- `IDbCardStore.UpdateCardPropertyAsync()` — update отдельного свойства
- `ICollectionStore.CreateCollectionAsync()` — создание коллекции (NodeType=1) без sideboard
- `ICollectionStore.CreateFolderAsync()` — создание папки (NodeType=0) без sideboard
- `ICollectionStore.GetCardsAsync()` / `GetAllAsync()` — чтение физических карт по всем коллекциям
- `ICollectionStore.GetAllPhysicalCardsAsync()` — все физические карты (для ownership-статистики)
- `IDbCardStore.GetCardsByIdsAsync(ids)` — batch-запрос для эффективного разрешения карт
- `ICollectionStore.GetCardCountsAsync(collectionIds, cardIds)` + `IDeckCardService.GetDeckCardCountsAsync(collectionIds, cardIds)` — точечная сумма счётчиков `(CollectionId, CardId) → SUM(Count)` для набора карт в наборе коллекций (один GROUP BY по Deck без JOIN к Cards; используется LastActions для totals вместо полной загрузки колоды)

**Таблицы БД:**
- `OwnCounts` — персистентность ownership stats (CardId PK, OwnCount, TotalCount, OwnUnique)

---

### ✅ Реализовано: Core Layer и инфраструктура
- **Модель данных** — MagicCard, MagicCardPhysical, MagicCardField, Edition, Location, CardGroup, ColorTypes, LegalityMap, Format
- **Контракты** — ICardStore<T>, IDbCardStore (GetEditionsAsync, BulkUpdateOwnCountAsync, GetCardsByIdsAsync), ICollectionStore (CreateCollectionAsync, CreateFolderAsync, LockAsync, UnlockAsync, GetOwnCountStatsAsync), IScrayfallClient
- **Фильтры и выражения** — AST: Expr → BinaryExpr/NotExpr/CardFieldExpr/TextValue, MagicCardFilter с сортировкой/группировкой, SearchStringTokenizer, Abilities (regex), ISearchableProperty
- **Агрегаторы** — `IAnalysisService` однопроходный анализ: mana curve, colors, types, creatures, abilities, basic stats. Analysis Blazor-компоненты.
- **Коллекционер** — `GetCollectorProgressQuery`
- **Единая БД `cards.db`** — таблицы (Cards, CardsFts, CardSet, Collection, Deck, OwnCounts, CardFaces, CardsImageUris, CardFacesImageUris) + CardAllParts, FTS5 триггеры, индексы, FK с ON DELETE CASCADE
- **SqliteCardStore** — CRUD, транзакции, FTS5-поиск, ConcurrentDictionary кэш, batch-запрос GetCardsByIdsAsync
- **SqliteCollectionStore** — SQLite + Dapper, ON CONFLICT merge, JOIN-запросы к Cards
- **ScryfallClient + ScryfallMapper** — авто-пагинация, маппинг legality
- **ImageCacheService** — `{set}/{lang}/{front|back}/{uid}.png`, создание директорий по языкам, UID-проверка
- **SyncKBCommand** — фазы (resolve → download → map → images → save → done), прогресс через SyncSetStateRegistry
- **SyncCardSetsCommand** — синхронизация списка сетов Scryfall в БД (флаг FullUpdate); вызывается из диалога SyncKnowledgeBase
- **Импорт/экспорт** — нативный JSON (JsonImport/JsonExport из Data.IO); legacy XML-импорт — XmlImport (OldDeckImportWizard)
- **Все Commands**: CreateNode, CreateDeck, CreateCollection, CreateFolder, DeleteNode, RenameNode, LockNode, UnlockNode, AddCards, RemoveCards, MoveCards, UpdateCard, SyncKB, SyncCardSets, ImportResolvedCards, SaveUserSetting
- **Все Queries**: SearchCards, GetDeckCards, GetScryfallAvailableSets, GetKBAvailableSets, HasCards, GetSyncProgress, GetNodesTree, GetNodeById, GetNodesBranch, GetCardById, GetCardPrintings, GetCardLanguages, GetCardInstances, GetCardSet, GetDeckAnalysis, GetCollectorProgress, GetUserSetting, GetSetSyncStatus, ResolveLegacyCards, ValidateCardIds
- **Инвариант импорта**: новый узел (колода/коллекция) никогда не создаётся до полного резолва всех карт. Коммит — только после 100% резолва (+ ручного сопоставления нераспознанных); new-режим атомарен (`CreateDeckWithCardsAsync`, одна транзакция). Оркестрация интерактива — в `DeckImportExportService` (доменный модуль всех действий с импортом), бизнес-логика — в атомарных командах/запросах
- **MainLayout** — 3 панели, header-действия, TabBar, рендер диалогов (закрытие — только ✕/Cancel + Escape, клик по оверлею не закрывает), status bar; CardsListControl — единый браузер карт для KB- и deck-режимов
- **SyncKnowledgeBase.razor** — рабочий компонент синхронизации
- **Analysis-компоненты** — ManaCurve, SpellColour, Creature, Type, Ability, Info, DeckLegality
- **DI**: Commands/Queries → Scoped, вертикальные сервисы → Scoped, инфраструктурные сервисы → Singleton
- **Lock-механика (UI)**: кнопка-lock в строках Deck/Collection навигатора (модель видимости как у Pin); lock deck распространяется на sideboard (Core, `LockAsync`/`UnlockAsync`); locked узлы не пинятся, при блокировке pinned-узла пин снимается; карточные операции (−/+, (+), Move, ✎, ⫯) скрыты для locked коллекций (count — read-only)

### ⚠️ Частично реализовано
- GetDeckCardsQuery использует `ICollectionStore.GetDeckCardsAsync()` с SQL JOIN — фильтрация на уровне БД
- Analysis-компоненты читают карты через `ICollectionStore.GetDeckCardsAsync(deckId)` и считают метрики через `IAnalysisService`

### ❌ Не реализовано
- События (EventManager, CardEvent) — UI не реагирует на изменения карт
- Локализация карт (locale/CardText)
