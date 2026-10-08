# MagicAssistant — UI Documentation

*ASP.NET Core Blazor Server · .NET 10 · Blazor Web App (MapRazorComponents + InteractiveServerRenderMode)*

---

## Table of Contents

- [1. Application Layout](#1-application-layout)
  - [1.1 Three-Panel Structure](#11-three-panel-structure)
  - [1.2 Component Inventory](#12-component-inventory)
- [2. Left Panel — Navigation](#2-left-panel--navigation)
  - [2.1 DecksNavigator](#21-decksnavigator)
  - [2.2 DecksNavigatorTreeItem](#22-decksnavigatortreeitem)
  - [2.3 LastActions](#23-lastactions)
- [3. Center Panel — Content](#3-center-panel--content)
  - [3.1 Header Actions](#31-header-actions)
  - [3.2 SearchControl](#32-searchcontrol)
  - [3.3 TabBar](#33-tabbar)
  - [3.4 CardsListControl](#34-cardslistcontrol)
  - [3.5 CardListView](#35-cardlistview)
  - [3.6 CardGalleryView](#36-cardgalleryview)
  - [3.7 CardTreeView](#37-cardtreeview)
  - [3.8 CollectorView](#38-collectorview)
  - [3.9 ContextStrip](#39-contextstrip)
- [4. Right Panel — Card Details](#4-right-panel--card-details)
  - [4.1 CardDescriptionView](#41-carddescriptionview)
  - [4.2 PrintingsView](#42-printingsview)
  - [4.3 InstancesView](#43-instancesview)
- [5. Analysis Pages (Tab Bar)](#5-analysis-pages-tab-bar)
- [6. Dialogs](#6-dialogs)
- [7. Wizards](#7-wizards)
- [8. Reusable Controls](#8-reusable-controls)
- [9. Architecture](#9-architecture)

---

## 1. Application Layout

### 1.1 Three-Panel Structure

**Prerendering:** disabled in `App.razor` — one shared `InteractiveServerRenderMode(prerender: false)` instance applied to both `HeadOutlet` and `Routes` (mixing prerendered and non-prerendered interactive components in one tree is not allowed). The first HTML response is an empty shell; every component initializes exactly once after the SignalR circuit attaches — no double `OnInitializedAsync`, no duplicate data loading on startup. Intentional: the app is fully interactive (all content arrives from SQLite), so prerendering would only render "Loading" states.

MainLayout (`Shared/MainLayout.razor`) renders a three-panel grid:

```
┌─────────────────────────────────────────────────────────────────────┐
│  Header: "⚔ Magic Assistant — Card Organizer"                      │
│          [Legacy Import] [Update DB]                                │
├──────────┬──────────────────────────────────┬───────────────────────┤
│ LEFT     │         CENTER                   │       RIGHT           │
│ Panel    │         Panel                    │       Panel           │
│          │                                  │                       │
│ Decks    │  ContextStrip (selected → pinned)│  CardDescriptionView  │
│Navigator │                                  │  ──────────────────── │
│          │  TabBar (if deck selected)        │  PrintingsView        │
│          │  ┌────────────────────────────┐  │  ──────────────────── │
│          │  │ Content Area               │  │  InstancesView        │
│          │  │ CardsListControl /         │  │                       │
│          │  │ Analysis pages             │  │                       │
│ Last     │  └────────────────────────────┘  │                       │
│Actions   │  Status Bar                      │                       │
└──────────┴──────────────────────────────────┴───────────────────────┘
```

**Collapsible side panels:** each side panel has a toggle button in its top corner (`panel-toggle`). Left: `<` at the top-right corner — collapses the panel to a 20px strip (content is clipped, not unmounted — navigator/LastActions state survives), the arrow flips to `>`; clicking again expands it. Right: mirrored — `>` at the top-left, `<` when collapsed. The content wrapper `.panel-content` keeps its natural width (`min-width` 280px / 360px), so during the 0.2s width transition content slides under the clip instead of reflowing.

**Content area:** `<div class="content-area" @key="_currentCollectionId">` — remounts on collection switch. Two render branches (RenderFragment, Razor syntax):

- `RenderDeckContent()` — `@switch (ActiveTab)`: analysis pages + default → `CardsListControl` (deck mode).
- `RenderKBContent()` — `CardsListControl` (KB mode).

**Dialogs:** Overlay-level dialogs managed by MainLayout state flags (`_showXxx`), rendered only when visible. Closing is only via the ✕ button (and Cancel/Escape where present) — clicking the overlay does nothing.

| Dialog | Trigger | Purpose |
|--------|---------|---------|
| SyncKnowledgeBase | Header `Update DB` | Full set sync from Scryfall |
| SyncSelectedSetCards | CardsListControl empty state → `OnOpenSync(setCode)` | Sync specific set |
| DeckImportWizard | DecksNavigator add-node menu (`+` → Import; root or folder row) | Atomic modern import — validation of all card IDs against KB, then node creation + cards in one transaction |
| OldDeckImportWizard | Header `Legacy Import` | Staged legacy import: missing-set sync prompt → unresolved-cards table with search → commit only when 100% mapped |
| DeckExportWizard | DecksNavigator context menu (⋯ → Export; deck/collection nodes only) | Native JSON export + browser download of the clicked node |
| EditMagicCardPhysicalDialog | CardsListControl ✎ (deck mode) | Edit card instance (count, ownership, price, tags) |
| SplitDialog | CardsListControl ⫯ (deck mode, count > 1) | Split stack main/sideboard |
| CardFilterDialog | CardsListControl `Filters` button | Central filter dialog: Sets (KB only)/Type/Rarity/Color/Properties; gold **Advanced** button opens the auxiliary dialog |
| CardFilterAdvancedDialog | CardFilterDialog gold **Advanced** button | Auxiliary filter dialog: Artist/Layout/Legality/Other + Collection section (deck mode); single Close button, committed by the main dialog's Apply |

### 1.2 Component Inventory

Компоненты и корневая страница `Index.razor`.

| Компонент | Файл | Описание |
|-----------|------|----------|
| **Layout** | | |
| MainLayout | `Shared/MainLayout.razor` | Трёхпанельная раскладка, header-действия, рендер диалогов, RenderFragment для deck/KB контента, status bar |
| TabBar | `Shared/TabBar.razor` | Вкладки: Cards, Mana Curve, Colors, Creatures, Types, Abilities, Info, Legality, Collector |
| **Left Panel** | | |
| DecksNavigator | `Components/Views/DecksNavigator.razor` | Дерево навигации, CRUD колод, кнопки Pin/Lock, Import в add-node меню, Export в контекстном меню |
| DecksNavigatorTreeItem | `Components/Views/DecksNavigatorTreeItem.razor` | Рекурсивный узел дерева (приватный) |
| LastActions | `Components/Views/LastActions.razor` | Панель последних операций: очередь (N=3), карточка слева, справа — метка локации над формулой before±N total |
| **Center Panel — Views** | | |
| CardsListControl | `Components/Views/CardsListControl.razor` | Единый браузер карт: KB-режим и deck-режим (`FilterViewModel.CurrentCollectionId`). Сайдбар сетов, splitter, toolbar (name filter, view mode, type/rarity, Filters, Move), пагинация |
| CardListView | `Components/Views/CardListView.razor` | Табличный view: count −/+ / ✎ / ⫯ в deck-режиме, пагинация |
| CardGalleryView | `Components/Views/CardGalleryView.razor` | Галерея карт с изображениями, lazy loading, page-size stepper (1–150) |
| CardTreeView | `Components/Views/CardTreeView.razor` | Дерево карт: группировка Color/Type/Set, expand/collapse |
| CollectorView | `Components/Views/CollectorView.razor` | Трекер прогресса по сетам с прогресс-барами |
| SearchControl | `Components/Views/SearchControl.razor` | FTS5-поиск (вверху окна CardFilterDialog): ввод → `FilterViewModel.SearchQuery` + `Apply()` |
| ContextStrip | `Shared/ContextStrip.razor` | Полоса из трёх секций вверху центральной панели: selected (слева) → золотая стрелка SVG (центр) → pinned (справа); «(x/y)» с тултипом через `IDeckCardService.GetDeckCardsAsync`; KB-вариант «KnowledgeBase Cards»; пустой pinned → «? (pin to set)» |
| **Right Panel** | | |
| CardDescriptionView | `Components/Views/CardDescriptionView.razor` | Детали карты: front/back (двусторонние), языки, цены, MagicText. Read-only |
| PrintingsView | `Components/Views/PrintingsView.razor` | Все печати: front/back миниатюры для двусторонних, фильтр по сету, OnPrintingSideChanged |
| InstancesView | `Components/Views/InstancesView.razor` | Экземпляры карты по всем колодам со сводкой |
| **Analysis Pages** | | |
| ManaCurvePage | `Components/Analysis/ManaCurvePage.razor` | CSS бары CMC 0-7+, hover-тултипы, статистика (avg/median/peak) |
| SpellColourPage | `Components/Analysis/SpellColourPage.razor` | Цветовой баннер идентичности, прогресс-бары с градиентами |
| CreaturePage | `Components/Analysis/CreaturePage.razor` | Горизонтальные бары подтипов существ |
| TypePage | `Components/Analysis/TypePage.razor` | Сетка карточек типов с emoji-иконками |
| AbilityPage | `Components/Analysis/AbilityPage.razor` | Горизонтальные бары keyword abilities |
| InfoPage | `Components/Analysis/InfoPage.razor` | Stat-карточки, badge'и идентичности, прогресс-бары композиции |
| DeckLegalityPage | `Components/Analysis/DeckLegalityPage.razor` | Summary banner legality, карточки форматов с expand/collapse |
| **Dialogs** | | |
| SyncKnowledgeBase | `Components/Dialogs/SyncKnowledgeBase.razor` | Диалог синхронизации, прогресс-бар, отмена |
| SyncSelectedSetCards | `Components/Dialogs/SyncSelectedSetCards.razor` | Синхронизация конкретного сета (предвыбранный SetCode) |
| EditMagicCardPhysicalDialog | `Components/Dialogs/EditMagicCardPhysicalDialog.razor` | Редактирование экземпляра: count, ownership, price, comment, tags, forTrade |
| SplitDialog | `Components/Dialogs/SplitDialog.razor` | Разделение стопки deck/sideboard ползунком на прогресс-баре (0–total) |
| CardFilterDialog | `Components/Dialogs/CardFilterDialog.razor` | Sets picker (только KB), Type/Rarity (MultiSelectList), color-чекбоксы + модификаторы, Properties (P/T/CMC); золотая кнопка Advanced открывает вспомогательный диалог |
| CardFilterAdvancedDialog | `Components/Dialogs/CardFilterAdvancedDialog.razor` | Вспомогательные фильтры: Artist, Layout, Legality, Other (Game Changer), Collection (deck mode: ownership/count/price/tags — IsFoil disabled); единственная кнопка Close |
| SetTypeFilterDialog | `Components/Dialogs/SetTypeFilterDialog.razor` | Toggle-picker по типам сетов (CollectorView Type Filter): staged-выбор, коммит только на Apply; персистится в user settings |
| **Wizards** | | |
| DeckImportWizard | `Components/Wizards/DeckImportWizard.razor` | Импорт нативного JSON в новую или существующую колоду |
| OldDeckImportWizard | `Components/Wizards/OldDeckImportWizard.razor` | Импорт legacy XML (Gatherer ID + полные имена сетов) |
| DeckExportWizard | `Components/Wizards/DeckExportWizard.razor` | Экспорт колоды в нативный JSON с скачиванием файла |
| **Shared Controls** | | |
| MultiSelectList | `Components/Controls/MultiSelectList.razor` | Универсальный обобщённый компонент выбора: чипы, диалог-оверлей, MultiSelectViewModel |
| SetSelectorDropdown | `Components/Controls/SetSelectorDropdown.razor` | Dropdown-селектор сетов (используется в SyncKnowledgeBase) |
| MagicText | `Components/Shared/MagicText.razor` | Рендеринг текста карт с MTG-символами (мана-косты) |


---

## 2. Left Panel — Navigation

### 2.1 DecksNavigator

**File:** `Components/Views/DecksNavigator.razor`

**Purpose:** Tree-view navigation of collections and decks.

**Parameters:** `OnDeckSelected(LockableCollection)`, `OnPinToggled(LockableCollection)`, `OnNodeLockToggled((int Id, bool Locked))`, `OnShowAllCards`, `OnNodeDeleted(int)`, `OnImportRequested(int?)`, `OnExportRequested(int)`, `SelectedNodeId(int?)`, `PinnedNodeId(int?)`.

**States:** Loading / Error + Retry / Empty ("No collections yet." + "Create first deck" button) / Normal (recursive tree).

**Key behavior:**

- **Single SQL query on init:** All nodes fetched in one query via `IDeckManagementService.GetAllNodesAsync()`. Tree built on .NET — no recursive DB calls.
- **Per-level sort order:** Collection → Deck → Folder (alphabetical within type).
- **Fully expanded by default.** Collapse only hides children — does not evict cached data.
- **In-place mutations:** Rename mutates `node.Name`, Lock/Unlock mutates `node.Locked` (and, for decks, `deck.Sideboard.Locked`) on the shared tree reference; Delete removes the node via `RemoveFromTree`. Successful deletion fires `OnNodeDeleted(deletedId)` — MainLayout invalidates current/pinned contexts that reference the deleted node (direct hit or folder cascade).
- **Highlight is a pure projection:** the navigator holds no selection state of its own. `.selected` is rendered from the `SelectedNodeId` parameter (= `MainLayout._currentCollectionId`). KB mode → `null` → nothing highlighted; the InstancesView jump switches the highlight along with the displayed collection. Folders are never selected.
- Click deck/collection/SB → `OnDeckSelected(ToLockable(node))` → MainLayout sets **the display context only** (`_currentCollection` + `FilterViewModel.CurrentCollectionId`); the pinned collection is changed exclusively by the Pin buttons. Re-clicking the already-displayed node is a no-op.
- **Lock button (deck/collection rows only):** works like the Pin buttons — hidden until row hover, stays visible (amber) while locked. Folders have no lock; a sideboard inherits its deck's lock state (Core propagates it in `LockAsync`/`UnlockAsync`). Click → `OnLockToggle(id)` → `IDeckManagementService.LockAsync/UnlockAsync` → in-place tree mutation + `OnNodeLockToggled((id, locked))` to MainLayout, which refreshes `_currentCollection.IsLocked` / `_pinnedCollection.IsLocked` and **unpins the node if it just became locked**.
- **Pin buttons are hidden on locked nodes** (deck/collection/sideboard) — a locked node cannot be pinned; locking an already-pinned node removes its pin. A locked node can still be selected and displayed (`_currentCollection`).

**Context menu (⋯ button):** icons are inline SVG (`.ctx-icon`, 15px, currentColor): Rename — «ab|», Clear/Clear sideboard — eraser, Export — upload, Delete — bold red X; pin/lock keep their state dots.

| Action | Behavior |
|--------|----------|
| ✏ Rename | Overlay rename dialog (name input + Save/Cancel, Enter to save) |
| 🟢 Pin / Unpin | Deck/collection nodes only. Toggles the pin on the clicked node; disabled while locked. **Deck with sideboard: rendered as a horizontal pair** — deck action (1/3 width) \| **Pin sideboard** for their sideboard (2/3 width), same row |
| 🧹 Clear sideboard | Deck nodes with sideboard only, **same row as 🧹 Clear** (right cell, 2/3 width). Opens a confirmation dialog ("Delete all N cards from the sideboard of \"X\"? The sideboard itself is kept.", N = `Sideboard.CardCount`); **disabled (gray) while the deck is locked**, same as the pin items. Confirm → `OnClearSideboardRequested(sideboardId)` (the SB node's own ID, not the parent deck's) → MainLayout: `IDeckCardService.ClearNodeAsync` — every card row in the SB is removed, the SB node itself is kept; status bar + strip/refresh triggers + navigator counts |
| 🧹 Clear | Deck/collection nodes only (folders get no item); deck with sideboard renders it as the left cell (1/3 width) of the pair row. Opens a confirmation dialog ("Delete all N cards from the deck/collection \"X\"? The deck/collection itself is kept.", N = the node's `CardCount`); **disabled (gray) while locked**, same as the pin items. Confirm → `OnClearRequested(nodeId)` (the clicked node's own ID) → MainLayout: `IDeckCardService.ClearNodeAsync` — every card row of the node is removed, the node itself is kept; status bar + strip/refresh triggers + navigator counts |
| 🔒 Lock / 🔓 Unlock | Deck/collection nodes only. Toggles node lock status |
| ⬇ Import | Deck/collection nodes. Closes the menu, fires `OnImportIntoRequested(collectionId)` — the clicked node's own ID → MainLayout opens DeckImportWizard pre-targeted at that CollectionId (`TargetNodeId`): cards **merge into the existing node** (a deck also receives the file's `sideboard` array into its sideboard child), the "Import to" select is hidden; **disabled (gray) while locked**, same as Clear. The add-node menu's Import keeps its old semantics (new deck inside the parent) |
| 📤 Export | Deck/collection nodes. Closes the menu, fires `OnExportRequested(nodeId)` — the clicked node's ID → MainLayout opens DeckExportWizard for that node (not the displayed collection); a deck export includes its sideboard in the JSON (`sideboard` field) |
| 🗑 Delete | Overlay delete confirmation ("Delete X and all its cards?") |

**Menu scope:** folders get Rename and Delete only — Pin/Lock/Import/Export/Clear items render on deck/collection nodes (`NodeType != Folder`), Clear sideboard renders on deck nodes that have a sideboard. Node creation lives in the add-node menu, not here.

**"+ add node" row:** Inline row at root level and below each folder's children. Clicking `+` opens dropdown: Collection / Deck / Folder, then a separator and **Import**: fires `OnImportRequested(parentId)` — null at root, the folder id for folder rows; MainLayout opens DeckImportWizard with that parent context, so "new deck" is created inside it.

**Sideboard badge:** Deck nodes display `SB` button (card count if > 0) before the ⋯ menu. Click → selects sideboard.

### 2.2 DecksNavigatorTreeItem

**File:** `Components/Views/DecksNavigatorTreeItem.razor`

**Purpose:** Recursive tree node component — renders any depth of nesting.

**Features:**

- **ExpandedSet:** Separate `HashSet<int>` parameter — determines visual expansion. Decoupled from cache: `IsExpanded = ExpandedSet.Contains(Node.Id)`.
- **Three node types:** Folder (with toggle ▸/▾ + recursion), Deck (with SB button), Collection.
- **Pin buttons:** on Deck, Collection and sideboard rows; rendered only when the node is not locked (a locked node cannot be pinned).
- **Lock button:** on Deck and Collection rows only — folders have no lock, sideboards inherit it from their deck. Same visibility model as Pin: hidden until row hover, stays visible with amber color while `.locked`. Click → `OnLockToggled(node.Id)` up to DecksNavigator.
- **SB button:** On Deck nodes, before ⋯ menu. Shows card count if > 0. Click → `SelectedNode = deck.Sideboard`.
- **⋯ More button:** LMB → context menu (same actions as right-click). `@onclick:stopPropagation` prevents deck selection.
- **Import pass-through:** the folder's AddNodeRow carries `ParentNodeId` = this node's id and forwards `OnImportRequest` straight up — nested folders each report their own id.

**CSS Isolation (recursive component):** Styles for `.tree-node`, `.tree-label`, `.tree-actions` live in the component's own scoped CSS — not via parent `::deep` (per docs/blazor-guide 05, approach 1). Depth indentation is passed by the parent through a CSS variable (`--depth`) with `padding-left: calc(var(--depth) * 16px + 8px)`.

### 2.3 LastActions

**File:** `Components/Views/LastActions.razor`

**Purpose:** Rolling history of the last card operations — across any collection. Each entry is labeled with its own location, so the queue is **never reset** (no clearing on navigation or node deletion). Pinned at the bottom of the left panel below DecksNavigator, separated by `<hr class="divider">` — does not scroll with the navigator.

**Parameters:** `Queue (required LastActionsQueue)` — the only parameter; no pinned/current context is passed in.

**Layout:**
- Header "Last actions" (same section-header style as PrintingsView).
- Queue rows: card image + name + meta (set / rarity badge / CMC / language, same as printing-row) on the left; a right-aligned column (`.action-side`) with the location label on top (`item.Location`, full name on hover) and directly below it the formula `before ±N total` — delta colored gold/red by operation, total = current count of that card **in the entry's own location** after the operation (clamped at 0; renders as 0 if the location no longer exists). `before` is derived from the live total: Add → total − N, Sub → total + N.

**Queue semantics (`LastActionsQueue`, in-memory, owned by MainLayout):**
- Fixed capacity `QueueLength = 3`; a new entry is appended to the tail, the oldest is evicted from the head. Re-enqueueing an existing `(LocationId, Card.Id)` pair replaces that entry with the incoming item at the tail — the row always shows the latest operation (Add/Sub) and count; `Changed` fires in both cases.
- Entry type: `LastActionItem(int LocationId, string Location, MagicCard Card, int Count, LastActionOperationEnum Add | Sub)`.
- Lifetime: entries live until evicted by capacity. The queue is never cleared — entries from different collections coexist, each carrying its own location label. Nothing is persisted anywhere.

**Sources of entries (all via `MainLayout.RecordLastActionAsync(CardOperationRecord)`):**

| Source | Location label | Entry |
|--------|----------------|-------|
| (+)/(−) in deck mode | name of the viewed collection (current) | Add/Sub, Count = \|delta\| |
| (+) in KB mode (pinned collection set) | name of the pinned collection | Add, Count = 1 (`MaterializeFromIdsAsync`, merge semantics) |
| Quick Move (viewing a collection different from pinned) | name of the pinned collection | Add, Count = copies moved |

The location name is resolved at record time via `IDeckManagementService.GetByIdAsync` (sideboards are named "{deck} (sb)" by the store).

**Totals:** on every queue change the component collects the distinct `(LocationId, CardId)` pairs in the queue and loads them with one grouped query (`ICollectionStore.GetCardCountsAsync(collectionIds, cardIds)` → `IDeckCardService.GetDeckCardCountsAsync`); a deleted location simply yields no rows → total renders as 0 (never a dash).

---

## 3. Center Panel — Content

### 3.1 Header Actions

**File:** `Shared/MainLayout.razor` (header section)

There is no separate Toolbar component. Action buttons live in the app header, right-aligned (Import and Export live in the DecksNavigator — see §2.1):

| Button | Action |
|--------|--------|
| `Legacy Import` | Opens OldDeckImportWizard (Gatherer ID + full set names) |
| `Update DB` | Opens SyncKnowledgeBase |

### 3.2 SearchControl

**File:** `Components/Views/SearchControl.razor`

FTS5 full-text search input. Pure input control for the FilterViewModel channel: writes `SearchQuery` and calls `Apply()`. The re-query is performed by CardsListControl (single `Changed` channel).

**Placement:** rendered at the top of the CardFilterDialog window. `OnSearchCompleted` is forwarded to MainLayout so the status bar keeps reporting search commits.

| Element | Description |
|---------|-------------|
| Text input | Search query, `@bind:event="oninput"` |
| Clear button (✕) | Clears `SearchQuery`, fires `Apply()` + `OnSearchCompleted("")` |
| Search button (🔍) | Commits query: `FilterViewModel.SearchQuery = query` → `Apply()` → `OnSearchCompleted(query)` |
| Syntax hint (ⓘ) | Tooltip: FTS5: "exact phrase" · name:term · term1 OR term2 |

**Keyboard:** Enter = apply, Escape = clear.

**Output:** `OnSearchCompleted(query)` → MainLayout updates `_statusText`.

### 3.3 TabBar

**File:** `Shared/TabBar.razor`

Visible only when a collection is displayed (`_currentCollectionId != null`). Tabs:

| Tab | Value | Component (RenderDeckContent switch) |
|-----|-------|-----------|
| Cards | `cards` (default) | CardsListControl (deck mode) |
| Mana Curve | `curve` | ManaCurvePage |
| Colors | `colors` | SpellColourPage |
| Creatures | `creatures` | CreaturePage |
| Types | `types` | TypePage |
| Abilities | `abilities` | AbilityPage |
| Info | `info` | InfoPage |
| Legality | `legality` | DeckLegalityPage |
| Collector | `collector` | CollectorView |

Active tab highlighted with `.active` class. Click → `OnTabSelected(value)` → MainLayout updates `ActiveTab`.

### 3.4 CardsListControl

**File:** `Components/Views/CardsListControl.razor`

**Purpose:** The single card browser for both modes:

- **KB mode** — `FilterViewModel.CurrentCollectionId == null`. Browse the knowledge base.
- **Deck (location) mode** — `FilterViewModel.CurrentCollectionId.HasValue`. Browse and manage cards of the selected deck/collection/sideboard.

**Parameters:**

| Parameter | Type | Purpose |
|-----------|------|---------|
| `FilterViewModel` | `required FilterViewModel` | Central filter/sort state (shared with CardFilterDialog) |
| `OnCardSelected` | `EventCallback<Guid?>` | Row click → right panel |
| `OnCardSideChanged` | `EventCallback<(Guid, bool)>` | Gallery: card + shown side |
| `OnOpenFilter` | `EventCallback` | Opens CardFilterDialog |
| `OnOpenSync` | `EventCallback<string>` | Empty state → SyncSelectedSetCards (set code) |
| `RefreshTrigger` | `int` | External reload trigger (after KB sync, after card move) |
| `ActionsQueue` | `required LastActionsQueue` | Shared in-memory queue of last card actions — count changes, KB (+) adds and quick moves are recorded here for LastActions |
| `OnEditCard` | `EventCallback<MagicCardPhysical>` | Deck mode: ✎ → EditMagicCardPhysicalDialog |
| `OnSplitCard` | `EventCallback<MagicCardPhysical>` | Deck mode: ⫯ → SplitDialog (count > 1) |
| `PinnedCollectionId` | `int?` | Pinned operation target (`MainLayout._pinnedCollection.CollectionId`): KB-mode (+) and quick Move act against it |
| `OnMoveToPinned` | `EventCallback<MagicCardPhysical>` | Quick move of the selected card (with all copies) to the pinned collection |
| `CurrentCollectionLocked` | `bool` | The displayed collection is locked — its card-mutation controls (−/+, ✎, ⫯) are hidden |
| `PinnedCollectionLocked` | `bool` | The pinned collection is locked — KB (+) and quick Move are hidden |

**Layout:** Two-panel — left sidebar (sets list + resizable splitter) + right content area (toolbar row + card view + empty state).

**Left Sidebar — Sets (click select):**
- Set list is a lazy DB view (`FilteredCardSetsCollection`, Shared/): cached by scope composition, queried on first render / scope change; `Invalidate()` after a KB sync. Scope = `FilterViewModel.FilteredSetCodes` — empty means all sets in the KB; location mode auto-fills it with the node's card set codes (`SetNodeScope()`), and the explicit Sets filter from Card Filter (KB only) replaces it. Display order: newest release first, then by parent set code; subsets get the `.subset` class (indented). An empty node shows no sets at all — no fallback to the full KB.
- Text search input (filters sets by name/code, min 2 characters). Typing narrows **only the visible set list** — cards are not re-queried. The text is stored in `FilterViewModel.SetSearchText` and survives component remounts (`@key` change on navigation). Empty input restores the full scope list immediately.
- Clicking a set selects it for the card grid: exactly one code (`FilterViewModel.SetActiveCardSets(code)`); the «ALL» row (label dynamic: KB — "All Sets"; location — "All sets of collection" / "All sets of deck" / "All sets of sideboard" per `FilterViewModel.CurrentCollectionNodeType`) and the ✕ button clear the selection, the card filter then falls back to the scope. The query takes `GetEffectiveSetCodes()` = clicked set ∩ scope; an empty intersection yields zero cards.
- Header: location mode — `sets: N, cards: M, total: T` (unique cards of the node narrowed by the selected set when one is active); KB mode — `KB (sets: N, cards: M)` (cards = the selected set's `CardCount`, otherwise `_totalCount`).
- Set icons via CSS `.set-symbol-{code}` classes.
- Header: `KB (sets: N, cards: M)` — `GetDisplayedSetsCount()` / `GetDisplayedCardsCount()`. Right side of the header — a collapse toggle button (`.sets-toggle`, mirrors MainLayout `.panel-toggle`): collapses the sidebar to a 28px strip with only the button visible (`<` → `>`, title/search bar/set list hidden via `.sets-sidebar.collapsed`). State is persisted (`KBCardsListControl.SetsCollapsed`) like the splitter position — the component remounts on every navigation, so a plain bool would reset. Dragging the splitter while collapsed expands the sidebar first, then resizes it.
- **Resizable splitter:** Draggable 8px divider via JS ES Module (`/js/splitter.js`, `import` pattern + `IAsyncDisposable` + `JSDisconnectedException` handling). `requestAnimationFrame` throttling (~60fps). Range: 160–500px. Position persisted via `SaveUserSettingCommandHandler`.

**Toolbar row:**
- Name filter input: debounced (`_nameInputBuffer` → 300ms → `FilterViewModel.NameFilter`). 3+ chars required for the query. Enter = immediate commit.
- Sort: handled by the CardListView column headers (see 3.5).
- View mode buttons: List / Gallery / Tree. Persisted via user settings (`KBCardsListControl.ViewMode`).
- Type dropdown: multi-value. Placeholder shows OR expression when > 1 value.
- Rarity dropdown: multi-value.
- `Filters` button → CardFilterDialog (shares the same `FilterViewModel`).
- Clear filters (✕) — active when any filter is set: `ClearAndApply()`. Also resets the local input buffers (debounced name filter, set search text) via `FilterViewModel.ClearGeneration` — stale text does not survive a Clear All.
- `📦 Move` button — visible only when a collection different from the pinned one is displayed **and neither the displayed nor the pinned collection is locked** (`CurrentCollectionId ≠ null && PinnedCollectionId.HasValue && CurrentCollectionId ≠ PinnedCollectionId && !CurrentCollectionLocked && !PinnedCollectionLocked`). Disabled until a card is selected. Quick move to the pinned collection — no picker.

**Data loading (`ApplyFiltersAndPagination`):**
- Server-side paged query: `ICardSearchService.GetPagedAsync(CardListFilter)` with `SetCodes = FilterViewModel.GetEffectiveSetCodes()` (clicked set ∩ scope; a non-null empty list → zero cards), `Rarity`, `TypeFilters`, `NameFilter` (3+ chars), `Query` (FTS5), `LocationFilter = CurrentCollectionId` (Core-layer property name is unchanged), color/property/layout/legality filters, sort field/direction, page/size.
- Deck mode: physical instances loaded via `IDeckCardService.GetDeckCardsAsync(CurrentCollectionId)` into `_physicalByCardId`. The leftmost **Total** column replaces the old count column (−/+ controls live in it; read-only when locked) — always > 0, because a card whose count drops to 0 disappears from the deck. The # column is hidden in deck mode.
- KB mode with a pinned collection set — unified with deck mode: the leftmost **Total** column shows `[−] total [+]` (one grouped query `IDeckCardService.GetDeckCardCountsAsync`, empty when 0, fixed width). (+) adds one copy to the pinned collection via `IDeckCardService.MaterializeFromIdsAsync` (merge semantics); (−) removes one copy (`RemoveCardsAsync` at 0, otherwise `UpdateCardAsync`) — both are recorded in LastActions. The minus button is hidden when the total is 0; a locked pinned collection shows the total read-only with no buttons. The column stays in sync locally on (+)/(−) press and re-fetches when the pinned target changes while KB is displayed.
- Errors reset the list to empty (no error UI in this component).

**View switching:**

| Mode | Component |
|------|-----------|
| List | CardListView |
| Gallery | CardGalleryView |
| Tree | CardTreeView |

**Pagination:** Default 50 cards/page. Prev / "Page N / M" / Next. Page size adjustable in gallery/tree mode (1–150, persisted via user settings). Any filter change resets to page 1.

**Empty states:**

| Condition (mode explicit) | Message | Action |
|-----------|---------|--------|
| KB: a set is clicked and it has no cards in the DB (direct `HasCardsQuery` check — no filter inference) | "No cards from the selected set in knowledge base yet." | `Sync from Scryfall` button → `OnOpenSync` |
| KB: active filters, no results | "No cards match the selected filters" | — |
| KB: no filters, KB empty (explicitly gated on KB mode) | "No cards in knowledge base yet." | — |
| Location: the node itself is empty — count check (`GetLocationTotalCount()`) precedes any filter inference, so an empty node reports as empty even with active filters | "No cards in the deck/collection/sideboard yet." (wording by `CurrentCollectionNodeType`) | — |
| Location: the node has cards but none displayed — filters are hiding them | "No cards match the selected filters" | — |

**Clear All button (✕):** lights up on `HasActiveFilters` — at least one *card* filter is set. The set scope (`ActiveCardSets`) and location mode itself are excluded on purpose: the set scope has its own ✕ in the sets sidebar, and a bare location is not a filter.

**Move flow:** 📦 Move (visible only when current ≠ pinned and neither is locked) → `OnMoveToPinned(physical)` → MainLayout: `IDeckCardService.MoveCardsAsync(currentCollectionId, [cardId], pinnedCollectionId)` → the addition is recorded in LastActionsQueue (Add, copies moved) → status bar + `_kbRefreshTrigger++` (reload). The selected card moves with all its copies.

### 3.5 CardListView

**File:** `Components/Views/CardListView.razor`

Tabular view rendered by CardsListControl in list mode.

**Card table (leftmost column: Total in deck mode and in KB with a pinned target; # otherwise):**

| Column | Content | Interactive |
|--------|---------|-------------|
| Total | Leftmost column. **Deck mode:** replaces the old count column — count with −/+ buttons (**locked collection: read-only**); always > 0, because a card whose count drops to 0 disappears from the deck. **KB mode with pinned target (unified display):** `[−] total [+]` — total copies of the card **in the pinned collection** (one grouped query `IDeckCardService.GetDeckCardCountsAsync`), empty when 0 — the column keeps its fixed width (`min-width: 44px`); updated locally on (+)/(−) press (same total LastActions shows), re-fetched on pin change / page load | Deck mode: `−` decrements (removes at 0); `+` increments (no upper limit). KB: `−` removes one copy from the pinned collection (hidden when total = 0); `+` adds one copy; locked pinned → read-only value |
| # | KB mode only (hidden in deck mode — Total replaces it). Row number within page | — |
| Name | Card name (printed name preferred) | ✎ Edit → `OnEditRequested`; ⫯ Split (count > 1) → `OnSplitRequested` — deck mode only, **hidden when the collection is locked** |
| CMC | Converted Mana Cost or "—" | — |
| Cost | Mana cost via `<MagicText>` | — |
| Rarity | Color-coded badge | — |
| Set | Set abbreviation or "—" | — |
| Type | Type line (truncated at 28 chars, full on hover) | — |

**Sortable column headers:** Name, CMC, Rarity, Set, Type (Total / # / Cost are not sortable). Click a header → sort switches to that column in ascending order (if it was not sorted by it); repeat click on the same header → toggles the direction. The active header is highlighted with a ▼/▲ indicator (same semantics as the old toolbar buttons). State lives in `FilterViewModel.SortField`/`SortAsc` — the single `Changed` channel re-queries from page 1.

Row click → `OnCardClicked(card)` → selection + right panel. All action buttons use `@onclick:stopPropagation` to prevent row selection.

**Pagination footer:** ← Prev / "Page N / M" / Next →.

### 3.6 CardGalleryView

**File:** `Components/Views/CardGalleryView.razor`

Visual grid of card images rendered by CardsListControl in gallery mode.

- CSS Grid: responsive density (`auto-fill`).
- Each tile: image via `ImageHelper.GetImageUrl(card, isFront)` (local cache, lazy loading) + fallback placeholder + name + meta (set + rarity badge).
- Double-sided cards: front/back toggle per tile → `OnCardSideChanged`.
- Deck mode: count badges from the `Counts` map.
- Pagination: Prev/Next + page-size stepper (1–150, persisted).
- Hover effect: frame highlight + box-shadow.

### 3.7 CardTreeView

**File:** `Components/Views/CardTreeView.razor`

Grouped view of cards rendered by CardsListControl in tree mode.

**Grouping modes (toolbar buttons):**

| Mode | Group key |
|------|-----------|
| Color | Card color identity |
| Type | Primary type from type line |
| Set | Set abbreviation |

**Rendering:** Header per group: expand icon (▸/▾) + color dot (MTG color hex) + group name + card count in parentheses. Click header → toggle expand/collapse. Expanded: list of cards with CMC. Groups sorted by count descending.

**Summary:** "{N} cards in {M} groups" at bottom. Page-size stepper (1–150, persisted).

### 3.8 CollectorView

**File:** `Components/Views/CollectorView.razor`

**Purpose:** Track collection progress across sets.

**Data source:** `IDeckAnalysisService.GetCollectorProgressAsync(setTypes)` — single SQL query (direct DI, no HTTP); an active set-type filter adds `WHERE s.SetType IN (...)` to that same query.

**Set type filter:** the "Type Filter" button opens SetTypeFilterDialog (§6.7) — one toggle per `CardSetType` value; pressed types are staged locally and committed only on Apply (✕/Cancel close without committing). The filter is persisted in user settings (`CollectorView.SetTypeFilter`, comma-separated names) and restored before the first query; the button shows its active state while the filter is non-empty.

**Counting rules:** Total — set checklist size from `CardSet.CardCount` (Scryfall). Collected/Owned — distinct printings across all user decks, deduplicated by `CollectorNumber` within the set: different printings of the same OracleId+Language are separate cards, a different Language for the same printing (shared CollectorNumber) is one card.

**Table columns:**

| Column | Content |
|--------|---------|
| Set | Set name |
| Collected | Distinct printings in user's decks for this set (CollectorNumber dedup, language duplicates collapse) |
| Total | Set checklist size (`CardSet.CardCount`, from Scryfall) |
| Progress | Visual progress bar (width = percent%) + percentage label |
| 4x | Reserved for future (currently "—") |

**Empty state:** "No collector data loaded. Sync sets from Scryfall to track progress."

### 3.9 ContextStrip

**File:** `Shared/ContextStrip.razor` (+ scoped `.razor.css`)

Status strip at the top of the center panel. A single bar in three sections: selected (left) | gold arrow pointing right (middle, inline SVG 24×24, `#ffd700`) | pinned (right).

| Section | Content |
|---------|---------|
| Left — selected | Always filled: the displayed collection name, or the KB variant `KnowledgeBase Cards` when no collection is displayed. Stats `(x/y)` pushed to the outer left edge; name pushed right toward the arrow |
| Middle — arrow | Gold SVG arrow pointing right (24×24 px), vertically centered, `aria-hidden` |
| Right — pinned | Pinned collection name (only deck/collection/sideboard can be pinned) pushed left toward the arrow; stats `(x/y)` pushed to the outer right edge. Nothing pinned → `? (pin to set)` in bold gray 24px, no stats |

**Typography:** names are large gold bold 24px (`#ffd700`) with ellipsis truncation and a full-name `title` tooltip; because each name hugs the arrow, the two names visually join in the center as "Selected Name → Pinned Name". Stats render as `(x/y)` (12px, muted) with a native `title` tooltip `unique: x, total: y`. The old `pinned:`/`selected:` labels and the middle divider are gone.

**Data:** `unique` = distinct cards in the node, `total` = sum of counts — one `IDeckCardService.GetDeckCardsAsync(collectionId)` per filled half (one row per card in Deck). Reloaded when the PinnedCollection/SelectedCollection ids change or on `RefreshTrigger` bump: MainLayout increments `_stripRefresh` in `RecordLastActionAsync` (+/−, KB add, Move), `OnEditCardSaved`, `OnSplitDone`, `OnImportCompleted`, `OnLegacyImportCompleted`. Unreachable/deleted node → zeros.

---

## 4. Right Panel — Card Details

Three stacked components separated by `<hr class="divider">`. All triggered by `_selectedCardId` set from CardsListControl (row/tile click) or PrintingsView.

### 4.1 CardDescriptionView

**File:** `Components/Views/CardDescriptionView.razor`

**Purpose:** Detailed card information display. **Read-only — no edit functionality.**

**Data sources:** `ICardSearchService` (card by ID), `GetCardSetQueryHandler` (set metadata), `GetCardLanguagesQueryHandler` (language variants).

**Parameters:** `CardId (Guid?)`, `ShowBack (bool?)`.

**States:** No card selected / Loading / Card not found / Normal (full details).

**Displayed fields (in order):**
1. Front/Back tabs (for double-sided cards with 2 faces)
2. Image via `ImageHelper.GetImageUrl(card, isFront)` — local cache
3. Language toggle buttons (EN/DE/etc.) — switch by OracleId via `GetCardLanguagesQuery`
4. Name + Mana cost row (`<MagicText>` for MTG symbols)
5. Meta line: Color Type, Rarity badge, CMC, "double-sided" badge
6. Type line
7. Power/Toughness (if applicable)
8. Oracle text (`<MagicText>` for MTG symbols)
9. Details grid: Set (full name), Collector Number, Artist, Language, Prices (USD non-foil/foil)
10. Rulings (scrollable block, if present)
11. Legality tags (green badges for legal formats)

**Double-sided cards:** `ShowBack` parameter from parent. Internal front/back tabs toggle the active face. Face helpers resolve name/cost/type/oracle/P/T per face.

### 4.2 PrintingsView

**File:** `Components/Views/PrintingsView.razor`

**Purpose:** All printings of a card across different sets.

**Data flow:** CardId → `ICardSearchService.GetCardByIdAsync()` → card name → `GetPrintingsAsync(name)` → all printings sorted by SetCode.

**Parameters:** `CardId (Guid?)`, `OnPrintingSelected(MagicCard)`, `OnPrintingSideChanged((Guid, bool))`.

**Display:**
- Header: "Printings" + count badge.
- Filter bar (when > 5 printings): text input to filter by set/rarity/language.
- Each printing row: front image thumbnail + back image (for double-sided cards) + name + meta (set, rarity badge, CMC, language uppercase) + collector number.
- Front/back images are clickable — select that side in CardDescriptionView via `OnPrintingSideChanged`.
- Current printing highlighted with `.current` class.

**Interaction:** Click printing/front/back → fires `OnPrintingSelected(card)` + `OnPrintingSideChanged(cardId, showBack)` → MainLayout updates `_selectedCardId` and `_selectedCardShowBack`.

### 4.3 InstancesView

**File:** `Components/Views/InstancesView.razor`

**Purpose:** All physical instances of a selected card across all decks/collections/sideboards.

**Data source:** `ICardStore<MagicCardPhysical>` (direct DI).

**Parameters:** `CardId (Guid?)`, `OnDeckSelected(int)`.

**Each instance row:**

| Element | Content |
|---------|---------|
| Count | "×N" badge |
| Ownership | Green "Owned" / Orange "Borrowed" badge |
| Deck name | Location (truncated) |
| Sideboard tag | "SB" if in sideboard |
| Properties | Price ($), Special (★), Comment (📝) — shown if present |

**Summary:** Total copies, Owned, Borrowed, Decks count.

**Interaction:** Click instance row → `OnDeckSelected(deckId)` → MainLayout `OnDeckSelectedFromInstance` → switches the **display context only** (`_currentCollection` + `FilterViewModel.CurrentCollectionId`); the pinned collection (operation target) is not changed.

---

## 5. Analysis Pages (Tab Bar)

All analysis pages load data via direct DI — no HTTP endpoints:

- Most pages: `ICollectionStore` (deck cards) + `IAnalysisService` (single-pass metrics).
- DeckLegalityPage: `ICollectionStore` only (per-card LegalityMap check).

### 5.1 ManaCurvePage

**File:** `Components/Analysis/ManaCurvePage.razor`

Vertical bar chart of non-land card distribution by CMC (0–7+).

- 8 vertical bars (CMC 0, 1, 2, 3, 4, 5, 6, 7+). Pure CSS (no Chart.js).
- Hover tooltip: CMC label, card count, percentage of total non-land cards.
- Bar height proportional to maximum bar value. Animated transitions on load.

**Summary stats:** Average CMC (weighted), Median CMC, Non-Land Cards, Peak CMC.

### 5.2 SpellColourPage

**File:** `Components/Analysis/SpellColourPage.razor`

Color distribution of non-land cards.

- MTG color-coded dots (W/U/B/R/G/Colorless) showing deck's color identity.
- Progress bars: one per color present, width = percentage, gradient fill matching MTG color hex, sorted by count descending.

**Summary stats:** Non-Land Cards, Colors Used, Dominant Color.

### 5.3 CreaturePage

**File:** `Components/Analysis/CreaturePage.razor`

Horizontal bar chart of creature subtypes from the `CreatureSubtypes` dictionary (subtype → count).

- Summary header: total creature count + unique subtype count.
- Horizontal bars sorted by count descending. Each row: subtype name + bar track + value with percentage.

### 5.4 TypePage

**File:** `Components/Analysis/TypePage.razor`

Card type distribution as visual card grid.

- Types tracked: Land, Creature, Instant, Sorcery, Enchantment, Artifact, Planeswalker.
- Grid of type cards with emoji icons and color-coded accents. Progress bar per type showing percentage of total deck.

**Summary:** Total Cards, Types Used, Dominant Type.

### 5.5 AbilityPage

**File:** `Components/Analysis/AbilityPage.razor`

Keyword ability frequency analysis from the `AbilityStats` dictionary (known abilities with regex patterns).

- Horizontal bars sorted by count descending. Each row: ability name + bar + value with percentage of total cards.

### 5.6 InfoPage

**File:** `Components/Analysis/InfoPage.razor`

Comprehensive deck summary dashboard.

**Stat cards:** Total Cards, Total Mana Cost, Max Repeats, Colors (unique count).

**Color identity:** MTG color-coded badges.

**Composition bars:** Lands %, Creatures %, Non-Creatures %.

**Additional info:** Average CMC, Land Percentage, Creature Percentage.

### 5.7 DeckLegalityPage

**File:** `Components/Analysis/DeckLegalityPage.razor`

Format legality check for all cards in deck (data via `ICollectionStore.GetDeckCardsAsync`).

**Formats checked:** Standard, Pioneer, Modern, Legacy, Vintage, Commander, Brawl, Historic, Pauper, Future Standard.

- Summary banner: Legal formats count, Illegal formats count, Total checked.
- Legality rate: progress bar showing percentage of legal formats.
- Format cards grid: each format with ✓ Legal / ✕ Illegal badge (green/red). Illegal formats: expandable list of illegal card names. Total unique illegal cards counter.

---

## 6. Dialogs

All dialogs are overlay-level. The MainLayout dialogs are managed by state flags (`_showXxx`) and rendered only when visible (`@if (_showXxx) { <Dialog ... /> }`); SetTypeFilterDialog (§6.7) is owned by CollectorView with its own flag. Closing is only via the ✕ button (and Cancel/Escape where present) — clicking the overlay does nothing.

### 6.1 SyncKnowledgeBase

**File:** `Components/Dialogs/SyncKnowledgeBase.razor`

**Purpose:** Sync a single set from Scryfall into the knowledge base.

**Parameters:** `IsOpen`, `IsOpenChanged`, `OnCompleted`.

**States:** Loading sets / Error / Selection form / Syncing (phase + progress bar + stats + Cancel) / Result (success/failure).

**Form fields:** Set selector (`SetSelectorDropdown`, all Scryfall sets, newest first, shows name/code/card count). Download images checkbox (default: checked).

**Sync phases:**
0. Resolving — looking up set info
1. Downloading — fetching card checklist
2. Mapping — DTO → MagicCard conversion
3. Images — downloading and caching card images
4. Saving — batch UPSERT to Cards table
5. Done — completion report

**Progress tracking:** Timer polling every 500ms via `SyncSetStateRegistry`. Cancellation: `CancellationTokenSource`.

### 6.2 SyncSelectedSetCards

**File:** `Components/Dialogs/SyncSelectedSetCards.razor`

**Purpose:** Sync a specific set opened from the CardsListControl empty state (via `OnOpenSync(setCode)`). Same phase pipeline as SyncKnowledgeBase, pre-filled with the selected set code (`SetCode` parameter).

### 6.3 EditMagicCardPhysicalDialog

**File:** `Components/Dialogs/EditMagicCardPhysicalDialog.razor`

**Purpose:** Edit physical card instance properties within a deck.

**Triggered by:** ✎ button in CardsListControl (deck mode).

**Parameters:** `IsOpen`, `IsOpenChanged`, `Card (MagicCardPhysical?)`, `DeckId (int?)`, `OnSaved(MagicCardPhysical?)`.

**Card preview:** a separate 350×500 window docked to the right of the form (wizard pattern): card image only, name in `alt`. Double-sided cards show the front face (`CardFaces[0]`); the name prefers `PrintedName` over `Name`. The image loads lazily — placeholder until the front file lands on disk (`ICardImageLoader.EnsureAsync`; re-render guarded by `ReferenceEquals`, so a stale download never repaints another card).

**Form fields:**

| Field | Type | Description |
|-------|------|-------------|
| Count | Number | Always saved |
| Ownership | Select: Owned/Borrowed | Saved only if changed |
| Price ($) | Float (step 0.01) | Optional, placeholder "0.00" |
| Comment | Textarea | Optional |
| Special Tags | Text (comma-separated) | e.g., "foil, premium, forTrade" |
| For Trade Count | Number (0–count) | Creates trade split if 0 < count < total |

**Save behavior:** Only non-null/non-default values persisted (smart update via `IDeckCardService.UpdateCardAsync`). For-trade: if 0 < fcount < total → `SplitCardAsync`; if fcount ≥ total → adds "fortrade" tag.

### 6.4 SplitDialog

**File:** `Components/Dialogs/SplitDialog.razor`

**Purpose:** Split a card pile between main deck and sideboard.

**Triggered by:** ⫯ button in CardsListControl (deck mode, only visible when count > 1).

**Parameters:** `IsOpen`, `IsOpenChanged`, `Card (MagicCardPhysical?)`, `DeckId (int?)`, `OnSplitDone`.

**Card preview:** compact block at the top of the dialog — card name + "× N in deck" (no image). The name is resolved exactly like EditMagicCardPhysicalDialog: for double-sided cards the front face's `PrintedName` (fallback `Name`, then base-card values); otherwise base-card `PrintedName ?? Name`.

**Split slider:** gold uppercase "DECK" label above the bar (left, same visual scale as the badge text) and gold capsule badge "sideboard" (right, same pill as the Colors tab / CreaturePage `.sideboard-badge`: dark background, gold text, translucent gold border). On the bar line: deck count to the left of the bar, sideboard count to the right. A range slider (0–total) sits on the bar; its value is the SIDEBOARD count — dragging the handle right pushes cards into the sideboard, dragging left pulls them back to the deck (cards flow in the direction of the drag, one per step). Bar under the slider: blue (deck) + orange (sideboard), no transition — follows the pointer 1:1. Segment widths are whole percents via `Math.Round`, same as CreaturePage's bar chart — an int renders identically in any culture (a locale decimal separator would break the inline style).

**Validation:** the **Move** button is enabled only when the slider value differs from its initial position at open (all copies in the deck); disabled while saving.

**Action:** `IDeckCardService.SplitCardAsync(deckId, cardId, sideCount)` → creates right part and moves to sideboard.

### 6.5 CardFilterDialog

**File:** `Components/Dialogs/CardFilterDialog.razor`

**Purpose:** Central control interface for card filtering. All fields bind directly to the shared `FilterViewModel`.

**Parameters:** `FilterViewModel (required)`, `IsOpen`, `IsOpenChanged`, `OnSearchCompleted` (forwarded to MainLayout → status bar).

**Search at the top of the window:** `SearchControl` (FTS5) is rendered below the header, above the filter fields.

**Fields:**

| Field | ViewModel property | Control |
|-------|-------------------|---------|
| Sets (**KB mode only** — hidden in collection/deck/sideboard, where the scope is driven by the node) | `FilteredSetCodes` (List<string>) | `MultiSelectList`: chips of the current scope + picker dialog (full set catalog with search, 2+ chars; checkbox rows with set icon/name/code). Picker selection is staged and committed on Apply → `SetFilteredSetCodes()` (Cancel discards); removing a chip commits live, same as Type/Rarity |
| Type | `TypeFilter` (List<string>) | `MultiSelectList` with types as chips |
| Rarity | `Rarity` (List<string>) | `MultiSelectList` with rarities as chips with color dots |
| Color Filters | `ColorFilters[ColorBitFlags]` (Dictionary) | Checkboxes: C/W/U/B/R/G/MULTI/MONO/HYBRID. Modifiers: And/Only/Identity (bool flags) |
| Properties | `PowerOp`/`PowerValue`, `ToughnessOp`/`ToughnessValue`, `CmcOp`/`CmcValue` | select operator (>=, =, <=) + number input per property |

**Advanced filters:** Artist, Layout, Legality, Other and the Collection section live in the auxiliary **CardFilterAdvancedDialog** (§6.6), opened by the gold **Advanced** button.

**Buttons:**

| Button | Action |
|--------|--------|
| **Advanced** (gold, left side of the footer) | Opens CardFilterAdvancedDialog (§6.6) on top; the main dialog stays open behind it. |
| **Apply** | `FilterViewModel.Apply()` → fires `Changed` → CardsListControl re-queries (includes all Advanced-dialog fields). Dialog closes. |
| **Clear All** | `FilterViewModel.ClearAndApply()` → resets all fields, including the Advanced-dialog ones + fires `Changed`. Dialog closes. |
| **Cancel** | Closes dialog without firing `Changed`. Pending changes in ViewModel remain. |

**OnParametersSet:** Syncs internal MultiSelectList view models from current `FilterViewModel.Rarity` / `FilterViewModel.TypeFilter` state.

### 6.6 CardFilterAdvancedDialog

**File:** `Components/Dialogs/CardFilterAdvancedDialog.razor`

**Purpose:** Auxiliary filter dialog — the less-frequently used fields of Card Filter, split out of the main window. Rendered by CardFilterDialog as a sibling overlay above it (z-index 1100 vs 1000).

**Parameters:** `FilterViewModel (required)`, `IsOpen`, `IsOpenChanged`.

**Fields** — all bind live to the shared `FilterViewModel`; nothing is committed on Close:

| Field | ViewModel property | Control |
|-------|-------------------|---------|
| Artist | `Artist` | Text input, oninput binding |
| Layout | `LayoutFilter` | Select (all `LayoutEnum` values with display names) |
| Legality | `LegalityFilter` | Select (all `Format` values via Scryfall keys) |
| Other | `GameChangerFilter` | Toggle button |

**Collection section** — rendered only when `FilterViewModel.CurrentCollectionId` is set (deck/collection/sideboard mode); hidden in KB mode. All controls are **enabled**; the full chain UI → FilterViewModel → CardListFilter → SQL is wired.

| Field | ViewModel property | Control / SQL source |
|-------|-------------------|---------------------|
| Card Count | `MinCount` / `MaxCount` (string, parsed at query time) | Number range 1–4 → `Deck.Count` |
| Price Range ($) | `MinPrice` / `MaxPrice` (string, parsed at query time) | Number range → `CAST(Cards.Usd AS REAL)` |
| Ownership + tags (one headerless toggle row) | `Ownership` ("all"/"owned"/"borrowed"), `HasForTrade`, `HasComment` | Toggle buttons: Owned/Borrowed (press the active one again to release → "all") → `Deck.IsOwn`; For Trade → `Deck.ForTradeCount > 0`; Has comment → `Deck.Comment` non-empty; Foil stays **disabled** (no foil flag column in the DB yet; SQL branch intentionally absent) |

**Commit semantics:** Close (the single footer button) and Escape only close the dialog — changes already live in the shared ViewModel and are committed by the main dialog's **Apply**. The header has no ✕ button.

### 6.7 SetTypeFilterDialog

**File:** `Components/Dialogs/SetTypeFilterDialog.razor`

**Purpose:** Toggle picker for the CollectorView "Type Filter" button — one toggle per `CardSetType` value.

**Triggered by:** CollectorView Type Filter button (owned by CollectorView, not MainLayout).

**Parameters:** `IsOpen`, `IsOpenChanged`, `SelectedTypes (IReadOnlyCollection<CardSetType>?)`, `OnApplied(IReadOnlyCollection<CardSetType>)`.

**Behavior:** pressed toggles are staged locally; **Apply** commits the staged set via `OnApplied` → CollectorView re-queries (`WHERE s.SetType IN (...)`) and persists the filter in user settings (`CollectorView.SetTypeFilter`). ✕/Cancel close without committing. Closing is only via ✕/Cancel — clicking the overlay does nothing.

---

## 7. Wizards

### 7.1 DeckImportWizard

**File:** `Components/Wizards/DeckImportWizard.razor`

**Purpose:** Import deck from a native Magic Assistant JSON file (format v1). Triggered by the DecksNavigator add-node menu (`+` → Import) — root row or a folder's row — or pre-targeted at an existing node from the ⋯ context menu (Import, `TargetNodeId`). Legacy XML files go through OldDeckImportWizard (§7.2).

**Parameters:** `IsOpen`, `ParentNodeId (int?)` (the menu's parent context; null = root), `TargetNodeId (int?)` (fixed merge target — the ⋯ menu path; hides the select), `OnImported`, `IsOpenChanged`. Injects `IDeckImportExportService` + `ICollectionStore`.

**Form fields:**
- **JSON File** — the native `InputFile` is visually hidden; a custom button (`label[for]`, no JS) triggers it. After selection a green confirmation line appears below the button: ✓ + the file name as plain text (spaces replaced with underscores, display-only). The file is parsed immediately on selection: invalid JSON or an empty card list (**both pools** — main + sideboard) is rejected in place (error line, nothing committed), and the deck's default name is taken from the file.
- **Import to** — dropdown "New deck" + every deck/collection at any tree depth (resolved from the DB via `CollectionStore.GetAllCollectionsAsync`, not the navigation-tree dictionary, which only contains root-level nodes). **Select mode only.** With a fixed `TargetNodeId` there is no bottom group at all — the target lives in the dialog title instead: **Import to <name>** with the name in gold gradient (`.target-name`, same technology as DeckExportWizard's title). The target is always a deck/collection: for a deck the file's sideboard array merges into its sideboard child, for a Collection it is silently ignored (only the main cards are imported).
- **Deck Name** — new-deck mode only; editable, defaults to the name from the file.

When opened from a folder's menu, a hint below the dropdown names the target: "New deck will be created inside 'X'" (name resolved via `GetByIdAsync`).

**Import flow (atomic commit):**
1. New-node mode: the deck name must be non-empty and **unique across the whole tree** — duplicates are allowed only for Folder nodes (Sideboards are hidden DB children, excluded from the check); a clash aborts with an error before anything is created.
2. `IDeckImportExportService.CommitModernImportAsync(jsonContent, ParentNodeId, targetId, deckName)` — single call (`deckName` optional; overrides the file's name for new decks):
   - parse JSON (`JsonImport`): format marker + version gate, reference core `id/name/set/count` required per card in **both** the `cards` and the optional `sideboard` array (same schema), unknown fields ignored; deck name from `name`;
   - validate every card ID (main + sideboard) against KB (`ValidateCardIdsQuery`) — **unknown cards abort the import before anything is created**;
   - commit via `ImportResolvedCardsCommand`: "new deck" → node + sideboard child + main cards **+ sideboard cards into the freshly created sideboard child**, all in one transaction under the menu's parent node (root when null); existing deck → merge (ON CONFLICT → add counts), sideboard cards merge into the target deck's sideboard child — pre-checked BEFORE any insert (merge mode commits main and sideboard as two separate transactions): a non-deck target or a missing sideboard child throws, so no partial import is possible.
3. On success: `OnImported`, then the dialog closes itself (`IsOpen = false` → `IsOpenChanged(false)`).

**Invariant:** no deck/collection node is ever created before all cards are validated.

**Validation:** Import button disabled until a valid file is selected.

### 7.2 OldDeckImportWizard

**File:** `Components/Wizards/OldDeckImportWizard.razor`

**Purpose:** Import legacy XML format (Gatherer ID + full set names) through the staged interactive flow of `IDeckImportExportService`.

**State machine:**

```
FileSelection → [Import Legacy] → Checking (CheckLegacyImportAsync)
  → missing sets? → SetPrompt: list of sets without card data, language toggles
     (shared "SyncKnowledgeBase.Languages" setting), "Download card images" checkbox
     → [Download & Import] → Syncing: sequential SyncKBCommand per set,
        progress polled via GetSyncProgressQuery (500ms), "set i of M", Cancel (CTS)
  → Resolving (ResolveLegacyCardsAsync)
     → all resolved → Committing (CommitImportAsync) → OnImported
     → unresolved rows → UnresolvedTable: 2-column table —
        col 1: card name + set; col 2: search input (starts at 3+ chars,
        debounce 300ms, SearchCardMatchesAsync → top-10 candidates with set/CMC),
        click candidate → selected chip with ✕ (re-pickable)
        → [Import N cards] (N = total copies in the save collection built by `BuildLegacyImportCardsAsync` once every row is mapped; includes duplicates; label shows no number until then) enabled only when EVERY row is mapped (strict all-or-nothing)
        → BuildLegacyImportCardsAsync (full collection: auto-resolved + manually mapped, counts from XML) → CommitImportAsync
     → OnImported → MainLayout: _stripRefresh++ + _kbRefreshTrigger++ (the viewed list reloads — new cards visible immediately when the import targeted it) + RefreshCardCountsAsync()
```

**Invariant:** the deck node is created only after 100% of cards are resolved and (if needed) manually mapped. Cancel at any stage — nothing is created or modified.

**Hover preview:** hovering a search candidate shows a card preview with lazy image loading (`ICardImageLoader`): fixed 300×400, pinned to the right of the viewport and vertically centered (`position: absolute` — outside the overlay's flex flow, so the dialog never shifts).

**Parameters:** `IsOpen`, `OnImported`, `IsOpenChanged` (same as DeckImportWizard). Injects: `IDeckManagementService` (existing-deck list), `IDeckImportExportService` (all flow steps), `GetSyncProgressQueryHandler` (sync progress polling), `GetUserSettingQueryHandler`/`SaveUserSettingCommandHandler` (language selection persistence).

**Split cards:** legacy face suffix is stripped for resolution — "Trial // Error (Error)" resolves as "Trial // Error".

### 7.3 DeckExportWizard

**File:** `Components/Wizards/DeckExportWizard.razor`

**Purpose:** Export a deck to the native Magic Assistant JSON format (v1). Triggered by the DecksNavigator context menu (⋯ → Export, deck/collection nodes only) — `DeckId` is the clicked node (`MainLayout._exportTargetId`), not necessarily the displayed collection.

**Parameters:** `DeckId (int?)`, `IsOpen`, `OnExported`, `IsOpenChanged`. Injects `IDeckManagementService` + `IDeckImportExportService` + `IJSRuntime`.

**Form fields:** Title **Export <deck name>** with the name in gold gradient (`.export-deck-name`). File Name: editable, auto-generated from deck name with spaces → underscores live (oninput). The format group is hidden — the native JSON format (v1) is the only one.

**Export flow:**
1. `IDeckImportExportService.ExportDeckToJsonAsync(deckId)` → generates the JSON string (`JsonExport`: envelope format/version/name/comment/type/exportedAt + cards + **optional sideboard array**; reference core always written, optional fields omitted at defaults, pretty 2-space indent). The sideboard node is present only for deck nodes that have a sideboard child with cards (`GetSideboardIdAsync` → `GetDeckCardsAsync(sb)`); empty or absent → the field is omitted entirely.
2. JavaScript `downloadFile.save(fileName, content, "application/json")` from `wwwroot/js/site.js` — Blob + object URL; content is passed as an interop argument, never interpolated into the script.
3. File downloaded as `{DeckName}.json`.

---

## 8. Reusable Controls

### 8.1 MultiSelectList

**File:** `Components/Controls/MultiSelectList.razor`

**Purpose:** Universal generic component for displaying and managing a collection of selected items as chips, with an optional add dialog.

**Generic parameters:** `<TKey, TItem>` where `TKey : notnull`

**ViewModel:** `MultiSelectViewModel<TKey, TItem>` (in `Shared/`) — POCO with `event Action? Changed`, `IReadOnlyList<MultiSelectItemBase> Items`.

**Methods:**
- `Initialize()` — overloads: from tuples `(key, item)`, from `IDictionary<TKey, TItem>`, from `IEnumerable<TItem>` with selectors. All clear current selection, deduplicate by key, fire `Changed` once.
- `AddNewItem(key, item, getDisplayValue)` — duplicate check by Key.
- `RemoveByKey(key)`, `RemoveItem(itemBase)`, `Clear()`
- `GetItemByKey(key)`, `GetAllItems()`, `Items`, `Count`, `IsEmpty`

**Parameters:** `ViewModel`, `IsAddDialogOpen` (with `Changed`, supports `@bind`), `AddDialogContent?` (RenderFragment), `OnAddClicked`, `OnItemRemoved`.

**Visual Forms:**

| Form | Description |
|------|-------------|
| Chip List (main) | Flex-wrap container with pill chips: `(DisplayValue ✕)(+)`. Click ✕ → `RemoveByKey()` → `OnItemRemoved`. Click + → opens dialog. |
| Add Dialog (overlay) | Opens on + click. Dark overlay with `@onclick:stopPropagation`. Header: "Add Item" + ✕. Body: `@AddDialogContent`. Closes: Escape, overlay click, ✕ button. |

**Data flow:** Parent creates ViewModel → component subscribes to `Changed` in `OnInitialized` → user clicks ✕/+ → ViewModel mutations → `Changed` event → `InvokeAsync(StateHasChanged)` → re-render.

**Scoped CSS:** Dark theme (`#1e1e1e` container, `#2d2d2d` chips). Chips: `border-radius: 16px`. Remove button (✕): circular, hover red. Add button (+): circular (`border-radius: 50%`), dashed border, hover blue. Dialog overlay: `z-index: 1000`.

### 8.2 SetSelectorDropdown

**File:** `Components/Controls/SetSelectorDropdown.razor`

Dropdown selector for Scryfall sets. Used by SyncKnowledgeBase (set selection form). Displays set name, code, and card count; newest sets first.

### 8.3 MagicText

**File:** `Components/Shared/MagicText.razor`

Renders card text with MTG symbols (mana costs, {W}, {2}, etc.) via CSS background-image icon classes (`.card-symbol-*` in global `site.css`). Used by CardListView, CardDescriptionView, and other views for cost/oracle text.

---

## 9. Architecture

### 9.1 Data Flow

```
DecksNavigator → OnDeckSelected(ToLockable(node)) → _currentCollection = node (display context only — pinned is changed exclusively by Pin buttons; queue is never reset — entries carry their own Location label)
DecksNavigator lock button → OnLockToggle(id) → IDeckManagementService.LockAsync/UnlockAsync (deck lock propagates to sideboard in Core)
    → tree mutation + OnNodeLockToggled((id, locked)) → MainLayout: _currentCollection.IsLocked / _pinnedCollection.IsLocked refresh, pin removed if the node became locked
DecksNavigator → OnNodeDeleted(id) → MainLayout validates pinned/current via GetByIdAsync → resets ghosts (KB fallback)
DecksNavigator add-node menu (+ → Import) → OnImportRequested(parentId, null = root) → MainLayout: _importParentId + DeckImportWizard ("new deck" created under parentId)
DecksNavigator ⋯ menu (Export — deck/collection only) → OnExportRequested(nodeId) → MainLayout: _exportTargetId + DeckExportWizard (exports the clicked node)
CardsListControl → OnCardSelected(cardId) → _selectedCardId → CardDescriptionView + PrintingsView + InstancesView
CardsListControl (deck mode) → OnEditCard(card) → EditMagicCardPhysicalDialog
CardsListControl (deck mode) → OnSplitCard(card) → SplitDialog (count > 1)
CardsListControl (deck mode, current ≠ pinned, neither locked) → OnMoveToPinned(physical) → MainLayout:
    IDeckCardService.MoveCardsAsync(current, [cardId], pinned) + LastActionsQueue.Enqueue(Add, copies moved)
    → _kbRefreshTrigger++ → CardsListControl reload
CardsListControl (KB mode, pinned set, not locked) → OnKbAddRequested(card) → MaterializeFromIdsAsync(pinned, [cardId]) + Enqueue(Add, 1)
PrintingsView → OnPrintingSelected(card) + OnPrintingSideChanged(cardId, showBack) → CardDescriptionView
InstancesView → OnDeckSelectedFromInstance(deckId) → _currentCollection only (pinned collection untouched)
SearchControl (вверху окна CardFilterDialog) / CardsListControl / CardFilterDialog → shared `_filterViewModel` → event Changed → re-query
SearchControl → OnSearchCompleted(query) → CardFilterDialog → MainLayout._statusText
```

### 9.2 Filter Architecture

`FilterViewModel` (single instance in MainLayout, `Shared/FilterViewModel.cs`) is the central filter/sort state for card browsing. Shared between SearchControl, CardsListControl, and CardFilterDialog via `[Parameter]`. Standard C# event (`Action`) — not EventCallback — because subscribers are components, not HTML elements.

| Property | Type | Source |
|----------|------|--------|
| `FilteredSetCodes` | `List<string>` | Set-list scope: location-mode auto-fill (`SetNodeScope()`) or the explicit Sets filter in Card Filter (KB only, `SetFilteredSetCodes()`) — empty = all sets; `Clear()` resets it in KB mode only |
| `ActiveCardSets` | `List<string>?` | Clicked sidebar set — exactly one code or null → `SetActiveCardSets()`; the card query takes `GetEffectiveSetCodes()` (click ∩ scope; an empty intersection yields zero cards) |
| `SetSearchText` | `string` | Sidebar text input — narrows only the visible set list, never the cards; persisted across component remounts, reset by `Clear()` |
| `Rarity` | `List<string>` | Toolbar dropdown / CardFilterDialog MultiSelectList |
| `NameFilter` | `string` | CardsListControl debounced input (300ms, 3+ chars) |
| `SearchQuery` | `string` | SearchControl (FTS5, вверху окна CardFilterDialog) — Enter/🔍 → `Apply()` |
| `TypeFilter` | `List<string>` | Toolbar dropdown / CardFilterDialog MultiSelectList |
| `Artist` | `string` | CardFilterDialog text input |
| `ColorType` | `string` | Color type filter value |
| `ColorFilters` | `Dictionary<ColorBitFlags, bool>` | CardFilterDialog checkboxes |
| `ColorAndMode` / `ColorOnlyMode` / `ColorIdentityMode` | `bool` | CardFilterDialog color modifiers |
| `PowerOp`/`PowerValue`, `ToughnessOp`/`ToughnessValue`, `CmcOp`/`CmcValue` | `string` | CardFilterDialog property filters (operator + value) |
| `LayoutFilter` | `string` | Layout filter (e.g., double-sided) |
| `LegalityFilter` | `string` | Legality filter |
| `GameChangerFilter` | `bool` | Game changer flag |
| `CurrentCollectionId` | `int?` | Set by MainLayout on navigator selection (`OnDeckSelected`) or InstancesView jump; cleared on KB switch (`OnShowKB`) — **switches CardsListControl between KB and deck mode**. Navigation state, not a filter — excluded from `Clear()` |
| `SortField` / `SortAsc` | `string` / `bool` | Sortable CardListView column headers (values match `CardListFilter.SortFields`: Name/CmcNumber/Type/SetCode/Rarity) |
| `ClearGeneration` | `int` | Increments on every `Clear()` — CardsListControl uses it to reset local input buffers after an external Clear All |

**Methods:** `Apply()` → fires `Changed`; `ClearAndApply()` → `Clear()` + `Apply()`; `Clear()` resets all fields without notification. Set-scope helpers: `SetFilteredSetCodes`, `SetNodeScope`, `ResetSetScope`, `SetActiveCardSets`, `GetEffectiveSetCodes`. Type/rarity helpers: `SetNewTypeFilter`, `SetTypeFilters`, `GetTypesOrExpression`, `SetNewRarityFilter`, `GetRarityOrExpression`.

`Apply()` → fires `Changed` event → CardsListControl resets `_currentPage = 1` + re-queries the server via `InvokeAsync`.

**NOT through the channel (direct calls):**
- Pagination (Prev/Next, page size) — navigates within results, not a filter change
- `RefreshTrigger` parameter — external trigger after KB sync completion and after card move

### 9.3 Component Relationship Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                           MainLayout                                │
│  Header: [Legacy Import][Update DB]                                 │
│  ┌──────────┬───────────────────────────────┬────────────────────┐  │
│  │ Navigator│         Center Panel          │     Right Panel    │  │
│  │          │  ContextStrip (selected/pinned)│                    │  │
│  │DecksNav  │  TabBar (deck mode)           │ CardDescView       │  │
│  │  ├─Deck1 │  ┌──────────────────────────┐ │ (read-only, front/ │  │
│  │  └─Deck2 │  │ CardsListControl         │ │  back, languages)   │  │
│  │          │  │ [name][view mode]        │ │ ────────────────── │  │
│  │          │  │ [type][rarity][Filters]  │ │ PrintingsView      │  │
│  │          │  │ [📦 Move — deck mode]    │ │ (front/back thumbs) │  │
│  │          │  │ ┌──────────────────────┐ │ │ ────────────────── │  │
│  │          │  │ │ CardListView         │ │ │ InstancesView      │  │
│  │          │  │ │ CardGalleryView      │ │ │                    │  │
│  │          │  │ │ CardTreeView         │ │ │                    │  │
│  │          │  │ └──────────────────────┘ │ │                    │  │
│  │          │  │ (deck mode: ✎ ⫯ − +)    │ │                    │  │
│  │          │  └──────────────────────────┘ │                    │  │
│  │          │  Analysis pages               │                    │  │
│  └──────────┴───────────────────────────────┴────────────────────┘  │
│                                                                      │
│  Dialogs:                                                            │
│  ├─ SyncKnowledgeBase         (Update DB)                            │
│  ├─ SyncSelectedSetCards      (empty state → Sync)                   │
│  ├─ DeckImportWizard          (add-node menu: + → Import)           │
│  ├─ OldDeckImportWizard       (Legacy Import)                        │
│  ├─ DeckExportWizard          (⋯ context menu: Export)              │
│  ├─ EditMagicCardPhysicalDlg  (✎ in CardsListControl, deck mode)     │
│  ├─ SplitDialog               (⫯ in CardsListControl, count > 1)    │
│  ├─ CardFilterDialog          (Filters in CardsListControl;         │
│  │                            FTS5 search at top; Collection sect.)  │
│  └─ SetTypeFilterDialog       (CollectorView Type Filter)           │
└─────────────────────────────────────────────────────────────────────┘
```
