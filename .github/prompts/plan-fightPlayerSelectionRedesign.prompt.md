# Plan: Fight/Player Selection & Analysis Header Redesign

**TL;DR**: Three areas are redesigned to match WoWAnalyzer's style: grouped fight selection with boss icons, inline player selection with role-colored cards + "By Fight / By Player" toggle + preloaded data, and an analysis page header with breadcrumbs, encounter/player info, and underline tabs with icons.

---

## Phase 1 — Data Model & Query Updates

*(Steps 1–4 are independent of each other)*

### Step 1 — Add `Icon?` to actor models
- `g:\source\FellowshipAnalyzer\src\FellowshipAnalyzer.Core\FellowshipLogs\IFellowshipLogsClient.cs`
  - Add `string? Icon` property to `FellowshipLogsActor` record
- `g:\source\FellowshipAnalyzer\src\FellowshipAnalyzer.FellowshipLogs\API\FellowshipLogsResponseModels.cs`
  - Add `public string? Icon { get; set; }` to internal `FellowshipLogsReportActor` class

### Step 2 — Update masterdata GraphQL query
- `g:\source\FellowshipAnalyzer\src\FellowshipAnalyzer.FellowshipLogs\API\Functions\ReportFunction.Queries.cs`
  - Remove `type: "Player"` filter from `actors(...)` clause so NPC/boss actors are returned too

### Step 3 — Map `Icon` in the response-to-domain mapping
- Find where `FellowshipLogsReportActor` is mapped to `FellowshipLogsActor` and add `Icon = actor.Icon`
- CDN URL format for actor icons is unconfirmed; try `https://assets.rpglogs.com/img/fellowship/abilities/{icon}` (same as `SpellIcon`). Handle 404 with a styled placeholder.

### Step 4 — Create `ReportNavigationState` scoped service
- New file: `g:\source\FellowshipAnalyzer\src\FellowshipAnalyzer\FellowshipAnalyzer.Client\Services\ReportNavigationState.cs`
  - Scoped service (lives for the browser session)
  - Stores `FellowshipLogsReportInfo` and `FellowshipLogsMasterData` keyed by report code
  - Methods: `Set(string code, FellowshipLogsReportInfo, FellowshipLogsMasterData)`, `TryGet(string code, out ...)`, `Clear()`
- Register in `Client/Program.cs`: `builder.Services.AddScoped<ReportNavigationState>()`

---

## Phase 2 — ReportInfo.razor Redesign (Fight + Player Selection)

*(Depends on Phase 1)*

### Step 5 — Add hero class → role mapping
- Inline static dictionary in `ReportInfo.razor` `@code` block
- `HeroRole` enum: `Tank, Healer, Dps, Unknown`
- Static dictionary: `"rime" → Dps` (expandable as heroes are added)
- Method: `static HeroRole GetRole(string? subType)`
- Role → CSS border color: Tank = `#336699` (blue), Healer = `#4ec04e` (green), Dps = `#ac1f39` (red), Unknown = `var(--fa-border)`

### Step 6 — Preload both report + masterdata in ReportInfo
- Inject `IFellowshipLogsClient`, `ReportNavigationState`, `NavigationManager`
- On init: check `ReportNavigationState.TryGet(ReportCode)` first
- On miss: call `Report.GetAsync()` and `MasterData.GetAsync()` **in parallel**, then cache both via `Set()`
- Build boss icon lookup: `Dictionary<string, string?> _bossIconByName` from NPC actors in masterdata

### Step 7 — Redesign "By Fight" fight cards
- Group `_report.Fights` by `fight.Name` into encounter groups
- Each encounter group:
  - **Header row**: circular boss icon (from `_bossIconByName`) + encounter name
  - **Attempts row**: horizontal flex-wrap of attempt pills
    - Kill pill: gold/green, ✓ icon, duration
    - Wipe pill: red, ✗ icon, attempt number (e.g. "Wipe 3"), duration
  - Clicking a pill expands/shows the player grid for that specific fight

### Step 8 — Redesign player cards
- Role-colored left border (4px)
- Hero class icon from `actor.Icon` via CDN
- Player name (bold) + hero class/spec name (dimmed)
- Role counts header per fight: 🛡 N | ✚ N | ⚔ N

### Step 9 — Add "By Fight / By Player" toggle
- Toggle button group in the hero panel or above the fights section
- **By Fight** (default): encounter group accordions (Steps 7–8)
- **By Player**: player accordion cards sorted by role then name
  - Expanded player: horizontal list of fight pills using `FriendlyPlayers` reverse lookup

### Step 10 — Update `ReportInfo.razor.css`
- Encounter group header (flex: icon + name)
- Attempt pills row (flex-wrap, gap)
- Kill/wipe pill styles
- Role-colored player card left border
- Role counts header
- Toggle button group ("By Fight / By Player")

---

## Phase 3 — Report.razor Analysis Page Redesign

*(Depends on Phase 1 Step 4 — parallel with Phase 2 after Step 4)*

### Step 11 — Read display data from NavigationState
- Inject `ReportNavigationState` into `Report.razor`
- After fight is resolved, extract and store: `_fightName`, `_fightKill`, `_fightDuration`, `_bossIcon`, `_playerName`, `_playerClass`, `_playerIcon`
- Deep-link fallback: if NavigationState has no data, fall back to re-fetching report info (already happens via `reportTask`)

### Step 12 — Redesign hero-panel: breadcrumbs + encounter info + player portrait
- **Breadcrumbs** replace `<p class="eyebrow">Combat Analysis</p>`:
  ```
  <a href="/report/@ReportCode">@(_reportTitle ?? ReportCode)</a> › @_fightName › @_playerName
  ```
  Clickable links; data comes from NavigationState (no API call on back-navigate)
- **Left side** — encounter block:
  - Circular boss icon
  - Encounter name (large)
  - Kill/Wipe status badge + duration
- **Right side** — player portrait block:
  - Circular hero class icon
  - Player name (large, optionally class-color tinted)
  - Hero class/spec name (dimmed)
- **Remove** current `hero-meta` spans showing raw report code / fight ID / player ID

### Step 13 — Redesign tabs to underline style with icons
- Replace `<button class="report-tab ...">` with underline-style tab strip
- Each tab: inline SVG icon + label text
  - Guide: checklist/list icon
  - Statistics: bar chart icon
  - Timeline: horizontal bars icon
  - About: info circle icon
- Active tab: `border-bottom: 3px solid var(--fa-gold)`, text `var(--fa-gold-light)`
- Hover: `border-bottom: 3px solid var(--fa-gold-dim)`
- Tab strip container: `display: flex`, `border-bottom: 1px solid var(--fa-border-card)` baseline

### Step 14 — Update `Report.razor.css`
- Breadcrumb chain styles (link, separator `›`, muted vs active)
- Encounter block layout (flex row: icon + text stack)
- Player portrait block layout
- New tab strip styles (underline, icon + text alignment)
- Remove old `.report-tab`, `.report-tab--active` button-based styles
- Remove `.hero-meta` / raw ID badge styles

---

## Relevant Files

| File | Change |
|---|---|
| `src/FellowshipAnalyzer.Core/FellowshipLogs/IFellowshipLogsClient.cs` | Add `Icon?` to `FellowshipLogsActor` |
| `src/FellowshipAnalyzer.FellowshipLogs/API/FellowshipLogsResponseModels.cs` | Add `Icon?` to internal actor model |
| `src/FellowshipAnalyzer.FellowshipLogs/API/Functions/ReportFunction.Queries.cs` | Remove type filter from masterdata actors query |
| `src/FellowshipAnalyzer.FellowshipLogs/API/Functions/...` (mapper) | Map `Icon` field |
| `Client/Services/ReportNavigationState.cs` | **New file** — in-memory nav cache |
| `Client/Program.cs` | Register `ReportNavigationState` |
| `Client/Pages/ReportInfo.razor` | Full fight/player selection redesign |
| `Client/Pages/ReportInfo.razor.css` | New styles |
| `Client/Pages/Report.razor` | Header + tabs redesign |
| `Client/Pages/Report.razor.css` | New header + tab styles |

---

## Verification

1. `dotnet build` — zero errors
2. Run via Aspire; enter a real report code
3. Fights grouped by encounter, boss icons appear, kill/wipe pills render correctly
4. Expand fight → player grid shows with role colors + class icons
5. Toggle "By Player" → player accordions expand to show their fights
6. Navigate fight → player → analysis, breadcrumb back → no extra API calls in DevTools Network
7. Analysis header: breadcrumbs show fight/player names (no raw IDs), boss icon and player portrait appear
8. Tabs: underline style with icons, correct tab switching
9. Deep-link directly to `/report/{code}/{fight}/{player}` still works

---

## Further Considerations

1. **Actor icon CDN URL** — Unconfirmed. Will try same pattern as `SpellIcon` (`assets.rpglogs.com/img/fellowship/abilities/{icon}`). If boss icons use a different prefix, update during implementation; add an `<img onerror>` fallback placeholder.
2. **Wipe percentage on attempt pills** — `FellowshipLogsFight` has no explicit completion % field in current models. Check if the API returns this; if not, show attempt # + duration only.
3. **Role mapping extensibility** — Static dictionary is pragmatic for now (only Rime exists). Consider moving `Role` to `HeroAnalysisDefinition` once multiple heroes are implemented.
