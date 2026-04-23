# Plan: Fight/Player Selection & Analysis Header Redesign

**TL;DR**: Three areas are redesigned to match WoWAnalyzer's style: grouped fight selection with boss icons, inline player selection with role-colored cards + "By Fight / By Player" toggle + preloaded data, and an analysis page header with breadcrumbs, encounter/player info, and underline tabs with icons.

---

## Phase 1 — Data Model & Query Updates

*(Steps 1–4 are independent of each other)*

### Step 1 — Update C# models: add `Icon?` to actors, `FightPercentage?` to fights

**`FellowshipLogsActor`** (`IFellowshipLogsClient.cs`):
- Add `string? Icon` property
- Note: schema also exposes `petOwner` (Int) — skip, not needed for this feature

**`FellowshipLogsReportActor`** (`FellowshipLogsResponseModels.cs` — internal model):
- Add `public string? Icon { get; set; }`

**`FellowshipLogsFight`** (`IFellowshipLogsClient.cs`):
- Add `double? FightPercentage`
- Schema description: *"The actual completion percentage of the fight. This is the field used to indicate how far into a fight a wipe was."*
- Note: `bossPercentage` also exists (boss HP at end of fight) but `fightPercentage` is the correct field for wipe progress display

**Internal fight response model** (`FellowshipLogsResponseModels.cs`):
- Add the corresponding `FightPercentage` property

### Step 2 — Update GraphQL queries (`ReportFunction.Queries.cs`)

There are **two separate query strings** that each need different changes:

**`ReportQueryString`** (fight list + actor display on ReportInfo page):
- Add `fightPercentage` inside the `fights { ... }` selection
- Add `icon` inside the `masterData { actors { ... } }` selection
- This query already returns all actor types (no type filter to change)

**`MasterDataQueryString`** (ability/actor metadata for the analysis page):
- Change `actors(type: "Player")` to `actors` (remove the filter) so NPC/boss actors are included
- `icon` is already selected in this query

### Step 3 — Map new fields in the response-to-domain mapping
- Where `FellowshipLogsReportActor` maps to `FellowshipLogsActor`: add `Icon = actor.Icon`
- Where the fight response model maps to `FellowshipLogsFight`: add `FightPercentage = fight.FightPercentage`
- CDN URL: schema describes actor `icon` identically to ability icons. Use `https://assets.rpglogs.com/img/fellowship/abilities/{icon}` (same as `SpellIcon`). Handle null/missing icon with a styled placeholder.

### Step 4 — Create `ReportNavigationState` scoped service
- New file: `Client/Services/ReportNavigationState.cs`
  - Scoped service (lives for the browser session)
  - Stores `FellowshipLogsReportInfo` keyed by report code
  - Methods: `Set(string code, FellowshipLogsReportInfo)`, `TryGet(string code, out FellowshipLogsReportInfo?)`, `Clear()`
- Register in `Client/Program.cs`: `builder.Services.AddScoped<ReportNavigationState>()`

---

## Phase 2 — ReportInfo.razor Redesign (Fight + Player Selection)

*(Depends on Phase 1)*

### Step 5 — Add hero class → role mapping
- Inline static dictionary in `ReportInfo.razor` `@code` block
- `HeroRole` enum: `Tank, Healer, Dps, Unknown`
- Static dictionary: `"rime" -> Dps` (expandable as heroes are added)
- Method: `static HeroRole GetRole(string? subType)`
- Role -> CSS border color: Tank = `#336699` (blue), Healer = `#4ec04e` (green), Dps = `#ac1f39` (red), Unknown = `var(--fa-border)`

### Step 6 — Preload report data in ReportInfo
- Inject `IFellowshipLogsClient`, `ReportNavigationState`, `NavigationManager`
- On init: check `ReportNavigationState.TryGet(ReportCode)` first
- On miss: call `FellowshipLogs.Report.GetAsync(ReportCode)`, then cache via `Set()`
- Build boss icon lookup: `Dictionary<string, string?> _bossIconByName` from actors where `type != "Player"` in `_report.Actors`

### Step 7 — Redesign "By Fight" fight cards
- Group `_report.Fights` by `fight.Name` into encounter groups
- Each encounter group:
  - **Header row**: circular boss icon (from `_bossIconByName[fight.Name]`) + encounter name
  - **Attempts row**: horizontal flex-wrap of attempt pills
    - Kill pill: gold/green, checkmark icon, duration
    - Wipe pill: red, X icon, attempt number (e.g. "Wipe 3"), `fightPercentage`% (e.g. "17.99%"), duration
  - Clicking a pill expands/shows the player grid for that specific fight
  - State: `_expandedFightId` (int?)

### Step 8 — Redesign player cards
- Role-colored left border (4px)
- Hero class icon from `actor.Icon` via CDN URL
- Player name (bold) + hero class/spec name (dimmed, from `actor.SubType`)
- Role counts header per fight: Tank / Healer / DPS counts

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
- After `reportInfo` is fetched (already happens via `reportTask`), store display fields:
  - `_reportTitle`, `_fightName`, `_fightKill`, `_fightDuration`, `_bossIcon`, `_playerName`, `_playerClass`, `_playerIcon`
- Deep-link fallback: `reportTask` already re-fetches report info on direct navigation — no extra handling needed

### Step 12 — Redesign hero-panel: breadcrumbs + encounter info + player portrait
- **Breadcrumbs** replace `<p class="eyebrow">Combat Analysis</p>`:
  ```
  <a href="/report/@ReportCode">@(_reportTitle ?? ReportCode)</a> > @_fightName > @_playerName
  ```
  Breadcrumb data comes from `reportInfo` (already fetched) — no extra API calls
- **Left side** — encounter block:
  - Circular boss icon (from `_bossIcon`)
  - Encounter name (large)
  - Kill/Wipe status badge + duration
- **Right side** — player portrait block:
  - Circular hero class icon (from `_playerIcon`)
  - Player name (large)
  - Hero class/spec name (dimmed, from `_playerClass`)
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
- Breadcrumb chain styles (link, separator, muted vs active)
- Encounter block layout (flex row: icon + text stack)
- Player portrait block layout
- New tab strip styles (underline, icon + text alignment)
- Remove old `.report-tab`, `.report-tab--active` button-based styles
- Remove `.hero-meta` / raw ID badge styles

---

## Relevant Files

| File | Change |
|---|---|
| `src/FellowshipAnalyzer.Core/FellowshipLogs/IFellowshipLogsClient.cs` | Add `Icon?` to `FellowshipLogsActor`; add `FightPercentage?` to `FellowshipLogsFight` |
| `src/FellowshipAnalyzer.FellowshipLogs/API/FellowshipLogsResponseModels.cs` | Add `Icon?` to internal actor model; add `FightPercentage?` to internal fight model |
| `src/FellowshipAnalyzer.FellowshipLogs/API/Functions/ReportFunction.Queries.cs` | `ReportQueryString`: add `fightPercentage` to fights + `icon` to actors; `MasterDataQueryString`: remove `type: "Player"` filter |
| `src/FellowshipAnalyzer.FellowshipLogs/API/Functions/...` (mapper) | Map `Icon` and `FightPercentage` fields |
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
3. Fights grouped by encounter, boss icons appear, kill/wipe pills render with wipe %
4. Expand fight -> player grid shows with role colors + class icons
5. Toggle "By Player" -> player accordions expand to show their fights
6. Navigate fight -> player -> analysis, breadcrumb back -> no extra API calls in DevTools Network
7. Analysis header: breadcrumbs show fight/player names (no raw IDs), boss icon and player portrait appear
8. Tabs: underline style with icons, correct tab switching
9. Deep-link directly to `/report/{code}/{fight}/{player}` still works

---

## Further Considerations

1. **Actor icon CDN URL** — Will try same pattern as `SpellIcon` (`assets.rpglogs.com/img/fellowship/abilities/{icon}`). Add an `onerror` fallback placeholder on the `<img>` element.
2. **Role mapping extensibility** — Static dictionary is pragmatic for now (only Rime exists). Consider moving `Role` to `HeroAnalysisDefinition` once multiple heroes are implemented.
