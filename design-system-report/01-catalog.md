# WoWAnalyzer Guide UI — Design System Catalog

A framework-agnostic reference for the components used to render player-performance analysis. Each entry covers **purpose**, **inputs (data shape)**, **visual structure**, **when to use it**, and **composition**. Source paths are given so you can cross-reference; the report itself avoids React-isms wherever possible so it can be re-implemented in Angular / Blazor / Vue / Svelte / etc.

For exact prop/data interfaces, see [`02-data-contracts.md`](./02-data-contracts.md).

---

## 0. Cross-cutting tokens

### 0.1 QualitativePerformance — the universal performance enum

Almost every component below ties its color, icon, and copy to a 4-bucket performance grade.

| Bucket | Color token | Icon mark | Typical meaning |
|---|---|---|---|
| `Perfect` | `--guide-perfect-color` (blue) | `glyphicon-ok-circle` | Above-the-bar play, nothing to improve |
| `Good` | `--guide-good-color` (green) | `glyphicon-ok` | Acceptable / target |
| `Ok` | `--guide-ok-color` (yellow) | `glyphicon-asterisk` | Minor issue, room to improve |
| `Fail` | `--guide-bad-color` (red) | `glyphicon-remove` | Clear mistake |

Three companion utility colors exist for chart accents: `VeryBad` (deeper red), `Mediocre` (orange), `Available` (cooldown ready). All are exposed as CSS custom properties so themes can be retargeted without re-compiling components.

Two universal mark helpers exist:
- **`PerformanceMark({ perf })`** — renders the icon corresponding to a `QualitativePerformance`.
- **`PassFailCheckmark({ pass: boolean })`** — binary green/red check.

These should be treated as the canonical "performance dot" primitives — every dashboard, tooltip, and badge in the system pulls from them.

### 0.2 Box-row entry — the universal performance record

```ts
interface BoxRowEntry {
  value: QualitativePerformance;  // determines color
  tooltip?: ReactNode | string;   // hover content (typically rich)
  className?: string;             // optional state hook (e.g. "selected")
}
```

Every list-of-events visualization (cast streams, cooldown usage, ability quality) feeds an array of `BoxRowEntry` into `PerformanceBoxRow`. A re-implementation should define this shape once and reuse it everywhere.

### 0.3 Theme containers

Three reusable "panel" looks underpin everything else:

- **`SectionContainer`** — dark translucent box, 8 px radius, 10/12 padding. The default card for any guide block.
- **`RoundedPanel`** / **`StartAlignedRoundedPanel`** — same look but slightly darker (#222), grid-based content, used for data panels inside two-column layouts.
- **`PerformanceRoundedPanel`** — `StartAlignedRoundedPanel` with an inset left shadow tinted by a `QualitativePerformance` — the "this panel summarizes a graded thing" pattern.

---

## 1. Foundation: section structure

### 1.1 `Section`
**Purpose.** Top-level expandable region inside a guide (e.g. "Core Skills", "Rotation & Cooldowns"). Defaults to expanded.

**Inputs.** `{ title: ReactNode, expanded?: boolean, children }`.

**Visual.** Yellow header with a dropdown chevron, dark level-1 background, bordered. Toggling the header expands/collapses the body.

**When to use.** Once per major topic in a guide. Usually 3–6 per guide.

### 1.2 `SubSection`
**Purpose.** Non-collapsible child of a Section. Carries a smaller bold title.

**Inputs.** `{ title?: ReactNode, id?, children }`.

**Visual.** Plain padding, a sub-heading row, then the body.

**When to use.** Whenever a Section needs internal grouping (one Section often contains 3–5 SubSections — e.g. "Rotation", "Cooldowns", "Defensive cooldowns").

### 1.3 `GuideContainer`
A flex-column wrapper with large gaps that sits at the very root of any guide. Pure layout; never authored directly by spec code.

### 1.4 `GuideSection` (spell-focused)
**Purpose.** Higher-level wrapper for one *spell's* analysis. Combines a spell title, explanatory text, and a data panel in either a side-by-side or vertical layout.

**Inputs.** `{ spell, title?, explanation, children, verticalLayout?, explanationPercent? }`.

**Visual.** Horizontal default: explanation (30–40 %) on the left, data panel (rounded panel) on the right. Vertical option stacks them.

**When to use.** Per-spell analysis subsection, the canonical "talk about one ability and show its stats" block.

### 1.5 `ExplanationRow` + `Explanation` + `Para`
A small family for two-column "explainer | data" content:

- **`Explanation`** — styled container for prose. Hidden globally when the user toggles "hide explanations" (session-persisted flag).
- **`ExplanationRow`** — CSS grid with a configurable `leftPercent` (default 30 %). Collapses to a single column when explanations are hidden.
- **`Para`** — div with paragraph-style spacing. Used in places where a real `<p>` would be invalid DOM.

**When to use.** Anywhere you want side-by-side "why this matters" copy and "your numbers" data. This is the most common content pattern inside a SubSection.

### 1.6 Toggles
- **`HideExplanationsToggle`** — toolbar switch tied to the explanation-visibility context.
- **`HideGoodCastsToggle`** — toolbar switch tied to a spell-usage context that filters perfect/good casts out of detail views.

Both wrap a single labeled toggle plus an optional tooltip.

### 1.7 Foundation sections (cross-spec building blocks)
These are *complete* sections any spec can drop into its guide. They render their own data via injected analyzers.

| Component | Renders | Always present? |
|---|---|---|
| `FoundationGuide` | Bundles the three "core skills" sections + Preparation | Optional; some specs assemble manually |
| `FoundationDowntimeSection(V2)` | "Always be casting" with metrics panel + timeline diagram (boss abilities, debuffs, player gaps, melee gaps, cancel gaps) | Practically every guide |
| `FoundationCooldownSection` | Educates on CD usage + auto-builds a `CooldownGraphSubSection` from the spec's ability list | Whenever the spec has cooldowns |
| `FoundationHealerManaSection` | Mana curve chart + healing-efficiency table | Healer specs only |
| `FoundationSupportBadge` | Small "Foundational Support" pill, with optional tooltip | Labels reusable foundation analyzers |
| `ByRole` | Renders children only when the player's role matches a whitelist (`Melee`, `Caster`, `Healer`) | Wraps role-specific subsections |

A "design system" port should treat these as **opinionated section templates** — not just primitives — that any spec is expected to embed.

---

## 2. Data wrappers and stat presentation

### 2.1 `GuideDataWrapper` — the visualization frame
**Purpose.** The single chrome used to wrap *any* visualization (chart, bar, timeline). Provides a header (title + subtitle + helper text), an optional row of stat pills, and a body slot.

**Inputs.**
```ts
{
  title: string | ReactNode;
  subtitle?: string;           // small uppercase label under the title
  stats?: ReactNode;           // pills shown right-aligned in the header
  statsHelperText?: ReactNode; // italic micro-copy under the pills
  helperText?: ReactNode;      // italic micro-copy under the section
  icon?: string;               // spell icon URL, compact mode only
  bare?: boolean;              // drop the box chrome
  compact?: boolean;           // single-row layout: header | stats | body
  children?: ReactNode;        // the actual visualization
}
```

**Visual.** Standard mode = header row above body. Compact mode = everything on a single row, with an optional 36 px icon on the left.

**When to use.** As the outermost wrapper for every visualization that needs a title and a few high-level stats next to it. If you're tempted to write a `<div><h3>...</h3>...</div>`, use this instead.

### 2.2 `StatCard` family — the headline number tile
A small composable kit of styled blocks:

- **`StatCard({ color })`** — outer pill with a colored translucent border. Min-height 44 px.
- **`StatCardValue({ color })`** — large bold value (2 rem). Accepts text or an image (auto-sized).
- **`StatCardDivider({ color })`** — 1-px vertical separator.
- **`StatCardLabel`** — small uppercase muted caption on the right.
- **`StatsGrid`** — 3-column grid container that lays out 3 stat cards per row.

**Pattern.** `<StatsGrid><StatCard color={c}><StatCardValue>42</StatCardValue><StatCardDivider/><StatCardLabel>Casts</StatCardLabel></StatCard>…</StatsGrid>`

**When to use.** Headline KPI tiles in CastOverview, CastDetail, BuffUptimeBar, and dozens of spec-specific blocks. The color is typically a `QualitativePerformance` color, but free-form colors are allowed.

### 2.3 `PerfBadgeGrid` family — performance-distribution pills
Same idea as StatCard but tuned for the 4-bucket performance distribution.

- **`PerfBadgeGrid`** — 4-column grid (one slot per performance level).
- **`PerfBadgeCount`** / **`PerfBadgeDivider`** / **`PerfBadgeLabel`** — value, separator, label pieces.

**When to use.** Header strip of CastOverview / CastDetail showing "12 Perfect | 4 Good | 1 Ok | 0 Fail" at a glance. Static (display-only) variant.

### 2.4 `FilterBadge` — clickable variant of the perf pill
Same visual shape as PerfBadgeCount, but:
- Adds `active` and `disabled` states (border, fill, opacity).
- Hover-bright when enabled.
- Pointer cursor; click toggles a filter.

**When to use.** The interactive header of CastDetail — clicking a bucket filters the timeline to that performance level. Anywhere a perf-bucket is also a toggle.

### 2.5 `TipBox` and `PerformanceTipBox` — alert callouts
**Purpose.** Reusable alert with a colored left border, optional icon, optional title, and a body.

**Variants.** `info` (blue), `note` (purple), `success` (green), `warning` (amber), `error` (red). `PerformanceTipBox` chooses variant from `QualitativePerformance`.

**Visual.** Dark translucent container, 6 px radius, semantic ARIA role (`alert` for warning/error, `note` otherwise).

**When to use.** Inline guidance ("This proc should be consumed within 4 s"), warnings ("You used Brew while at full charges"), success messages, and as the "Details" body inside CastDetail cards.

### 2.6 Auxiliary
- **`GuideTooltip`** — opinionated tooltip body containing timestamp + performance mark + per-check row list. Use it as the `tooltip` content for any cast/box visualization.
- **`HelperText`** / **`HelperTextRow`** — italicized muted micro-copy under stats; pair with a counter or hint inline.
- **`SectionContainer`** / `BareSection` — see § 0.3.

---

## 3. Cast analysis components

A spec's "did the player cast this spell correctly?" story typically uses *one* of the following five components per spell — they form a ladder from "summary" → "detail":

| Component | Granularity | Interactivity | Typical role |
|---|---|---|---|
| `CastEfficiencyPanel` | aggregate | none | "Did you cast it enough?" |
| `CastOverview` | aggregate | none | Headline KPI tiles + optional extra block |
| `CastSummary` / `CastSummaryAndBreakdown` | distribution | expand to box-row | "What's the quality distribution?" |
| `CastDetail` | per-cast | filter + carousel + timeline | "Walk me through every cast" |
| `CastSequence` | per-window | nav between windows | "Was the rotation right inside this burst?" |

### 3.1 `CastOverview`
**Purpose.** Aggregate KPI tiles for one spell, plus an optional supplementary block.

**Inputs.**
```ts
{
  spell: Spell;
  stats: { value: string; label: string; tooltip: ReactNode; performance?: QualitativePerformance }[];
  additionalContent?: { title?: string; content: ReactNode };
}
```

**Visual.** GuideDataWrapper header → `StatsGrid` of `StatCard`s, each tinted by its own performance.

**When to use.** Top of a spell's section, before any detail view. Sets the "how did I do overall on this spell" frame.

### 3.2 `CastDetail`
**Purpose.** Interactive per-cast review carousel.

**Inputs.**
```ts
type PerCastStat = { value: ReactNode; label: string; tooltip?; performance?: QualitativePerformance };
type PerCastData = {
  performance: QualitativePerformance;
  stats: PerCastStat[];
  tooltip?: ReactNode;
  timestamp: string;
  details?: ReactNode;          // free-form body (often PerformanceTipBox)
  detailsIcon?: ReactNode | null;
  additionalContent?: { title?, content };
};
{ title: string; casts: PerCastData[]; description?: string }
```

**Visual.**
- Top strip: clickable `FilterBadge`s (one per perf bucket).
- Timeline row: small colored rectangles, one per cast (auto-fill grid, ~3–5 % each).
- Selected-cast card: header (← / → nav + performance badge + timestamp), `StatsGrid`, optional details box, bottom accent bar in the cast's color.
- Keyboard (arrow keys) and touch (swipe) navigation; fade-in transition.

**When to use.** Per-cast review for spells with rich per-cast decisions (proc consumption, target selection, target count, etc.). Typically follows `CastOverview` in the same SubSection.

### 3.3 `CastSummary`
**Purpose.** Lighter alternative to `CastDetail`: header pills + a gradiated bar + optional expandable box grid.

**Inputs.** `{ spell; casts: { timestamp, performance, reason: string }[]; showBreakdown?: boolean }`.

**Visual.** PerfBadgeGrid + GradiatedPerformanceBar; expandable into a PerformanceBoxRow of per-cast boxes (each tooltip showing the reason).

**When to use.** Spells where the per-cast carousel would be overkill but a one-line distribution + drill-down is welcome.

### 3.4 `CastSummaryAndBreakdown`
**Purpose.** The richest "summary + drill-down" component. Adds per-performance percentage tiles + customizable per-bucket explanation copy. Generic over the per-cast data type via `BoxRowEntry`.

**Inputs.** Highly configurable: `spell`, `castEntries: BoxRowEntry[]`, plus per-bucket `*Label` / `*ExtraExplanation` / `include*CastPercentage` flags, an `onClickBox` callback, and a `usesInsteadOfCasts` toggle (so the same component labels Mitigations, Procs, etc. correctly).

**Visual.** Per-bucket summary tiles → spell name + an inline explanation of the color legend → expandable `GradiatedPerformanceBar` that reveals a `PerformanceBoxRow` with clickable boxes.

**When to use.** Whenever spell analysis needs both a distribution view *and* a clickable per-cast detail, with the language tweaked for that spell ("uses" vs "casts" etc.).

### 3.5 `CastSequence`
**Purpose.** Filmstrip of spell icons for one or more rotational windows, navigable forward/back.

**Inputs.**
```ts
type CastInSequence = { timestamp, spellId, spellName, icon, performance?, outlineColor?, ghosted?, tooltip? };
type CastSequenceEntry<T> = { data: T; casts: CastInSequence[]; start?, end? };
{ spell; sequences: CastSequenceEntry<T>[]; description?; castTimestamp: (data) => string; iconSize? }
```

**Visual.** Header shows current window's timing → prev/next nav + counter → horizontal scroll-strip of spell icons, outlined in their performance color. Ghosted icons (e.g. untalented or weak spells) get grayscale + low opacity.

**When to use.** Burst-window / cooldown-window rotation review. Used wherever the *order* of casts is the thing being graded.

### 3.6 `CastEfficiencyPanel`
**Purpose.** Compact "did you press it enough?" summary for one spell.

**Inputs.** `{ spell, useSpellLink?, useThresholds? }`. Pulls everything else from the `CastEfficiency` analyzer.

**Visual.** RoundedPanel containing a one-line summary ("Tiger's Palm — 92 % efficiency (37 of 40 casts)") + a `CooldownBar` timeline (yellow = on CD, grey = available, red = missed window highlight, white tick = cast).

**When to use.** Where a spell's *frequency* matters more than its quality — e.g. paired alongside `CastSummaryAndBreakdown` so the reader sees both quality and quantity.

### 3.7 `CastReasonBreakdownTableContents`
**Purpose.** Tabular breakdown of "why each cast happened", grouped by categorical reason. Generic over the reason type.

**Inputs.** `{ casts; label: (reason) => ReactNode; containerType?: ElementType; possibleReasons; badReason? }`.

**Visual.** `<tbody>` of rows: reason label | count | `PassFailBar` (proportional). One reason can be marked "bad" and renders in red.

**When to use.** When the interesting axis isn't time, it's the categorical *reason* for a cast (e.g. proc vs. CD-ready vs. forced movement).

---

## 4. Visualizations: bars, charts, and timelines

### 4.1 `PerformanceBoxRow` (with `BoxRowEntry`)
**Purpose.** The workhorse "row of colored squares" visualization — one box per event.

**Inputs.** `{ values: BoxRowEntry[]; onClickBox?(index) }`.

**Visual.** Horizontal auto-fill grid, ~60 px max per box, 0.8 em tall, 2 px gap. Each box is colored by its `QualitativePerformance`; siblings desaturate on hover to focus context.

**When to use.** Per-cast quality, per-mitigation quality, per-mechanic execution. Scales well from 10 to a few hundred events.

### 4.2 `StackedBar`
**Purpose.** Single-metric composition broken into labeled segments.

**Inputs.** `{ segments: { label, value, color, tooltip? }[]; height?; hideLegend?; minSegmentPercent?; tooltipFormat? }`.

**Visual.** 100 %-width horizontal bar with adjacent legend. Hover brightens the segment 1.2×; tiny segments are hidden by default.

**When to use.** Damage-by-school breakdowns, mitigation source attribution, resource spending categories. Caller supplies the palette — no perf-color semantics.

### 4.3 `GradiatedPerformanceBar`
**Purpose.** Distribution-of-outcomes meter across the four performance buckets.

**Inputs.** `{ perfect?, good?, ok?, bad? }` — each either a count or `{ count, label }`.

**Visual.** Segmented bar (segment width ∝ count/total). Per-bucket tooltips. Hard-coded perf colors.

**When to use.** Inside `CastSummary` / `CastSummaryAndBreakdown`, or anywhere you want a single bar summarizing the quality distribution.

### 4.4 `PassFailBar`
**Purpose.** Binary success-rate meter (pass vs. fail, hit vs. miss).

**Inputs.** `{ pass, total, className?, passTooltip?, failTooltip? }`.

**Visual.** Two-segment bar, default green + red. Native browser tooltips via `title`.

**When to use.** Inside tables like `CastReasonBreakdownTableContents`, uptime/dodge displays.

### 4.5 `BuffUptimeBar`
**Purpose.** Buff/debuff uptime timeline with optional stack-progression.

**Inputs.** `{ spell; buffHistory: { start, end?, stackHistory? }[]; startTime; endTime; barColor?; backgroundBarColor?; maxStacks?; averageStacksTooltip? }`.

**Visual.** Inset dark container with `StatCard` row above (uptime %, average stacks, max stacks). Below, an SVG timeline 24 px tall — flat bars for plain buffs, area-chart heights for stacking buffs.

**When to use.** Whenever a buff or debuff with meaningful uptime needs to be visualized over a fight.

### 4.6 `DamageTakenPointChart`
**Purpose.** Mitigation-quality view of incoming hits.

**Inputs.** `{ hits: { event: DamageEvent, mitigated: QualitativePerformance }[]; tooltip: FC<{ hit }>; showSmallHits? }`.

**Visual.** Grouped by source (one row per attacker/spell, 150 px label column). Each hit is a thin vertical slice colored by mitigation quality, positioned by relative timestamp. Sub-10 %-HP hits hidden by default.

**When to use.** Tank defensive review — "for each big hit, did your defensive layer fire?"

### 4.7 `DamageMitigationChart`
**Purpose.** Multi-layer fight timeline with damage taken (area chart) + defensive cooldown windows (rectangles).

**Inputs.** `{ analyzers: MajorDefensive[]; onHover; yScale? }`.

**Visual.** Vega-Lite composite: green rectangles for active mitigation windows; cubic-interpolated area chart for DPS taken, split by school. Hover-signal pushed to the parent so external panels can highlight matching mitigations.

**When to use.** Tank/healer "where did the big hits land vs. where were your CDs up?"

### 4.8 `CooldownGraphSubSection`
**Purpose.** Renders one `CooldownBar` per cooldown ability in a SubSection layout.

**When to use.** As the "Use Your Cooldowns" content. `FoundationCooldownSection` produces this automatically from the ability list.

---

## 5. Cooldown analysis: a layered abstraction

This is the most opinionated part of the system. The flow is `Analyzer → Per-cast records → BoxRowEntry → interactive UI`. There are two parallel ladders depending on whether the cooldown's grade comes from *how it was cast* (offensive/utility) or from *how much damage it absorbed* (defensive).

### 5.1 `MajorCooldown` — cast-quality cooldowns
**Purpose.** Analyzer base class for cooldowns whose quality depends on *execution criteria* (right target, right buffs up, right phase).

**Author API.**
```ts
class MyCooldown extends MajorCooldown<MyCast extends CooldownTrigger<AnyEvent>> {
  description(): ReactNode;
  explainPerformance(cast: MyCast): SpellUse;   // returns one record per cast
}
```

**Output data shape.**
```ts
type ChecklistUsageInfo = {
  check: string;                       // stable id, e.g. "deathmark-up"
  timestamp: number;
  performance: QualitativePerformance;
  summary: ReactNode;                  // inline tooltip line
  details: ReactNode;                  // detail panel content
};
type SpellUse = {
  event: AnyEvent;
  checklistItems: ChecklistUsageInfo[];
  performance: QualitativePerformance; // combined (worst-of) across items
  performanceExplanation?: ReactNode;
  extraDetails?: ReactNode;            // free-form panels below the checklist
};
```

`spellUseToBoxRowEntry(use, fightStart)` converts each `SpellUse` into a `BoxRowEntry` whose tooltip is a small table of checklist rows.

### 5.2 `CooldownUsage` + `SpellUsageSubSection` — rendering MajorCooldown
**Purpose.** The standard UI for a `MajorCooldown` analyzer. `CooldownUsage` is the thin wrapper that:
1. Pulls actual `uses` from the analyzer.
2. Asks `CastEfficiency` for the number of *possible* casts on cooldown.
3. Pads the box row with synthetic entries — yellow ("possible miss") up to half, red ("wasted") past that — so missed casts are visible.

**Visual (via `SpellUsageSubSection`).**
- `ExplanationRow`: prose on the left, `PerformanceBoxRow` of all uses on the right.
- Clicking a box reveals a detail panel: header with timestamp + combined performance label, then a checklist table (perf mark | summary | details), then any `extraDetails`.
- Listens to a `useSpellUsageContext()` that lets the user "hide good casts" globally.

**When to use.** This is the canonical "show me every press of this cooldown and how I graded each" UI for almost every offensive cooldown in retail.

### 5.3 `MajorDefensive` + `Mitigation` — damage-mitigation cooldowns
**Purpose.** Analyzer base class for cooldowns that *reduce damage taken* over a window.

**Output data shape.**
```ts
type MitigatedEvent = { event: DamageEvent | …; mitigatedAmount: number };
type Mitigation = {
  start: number; end: number;
  mitigated: MitigatedEvent[];
  amount: number;
  maxAmount?: number;                       // for cap-bound CDs
};
type MitigationSegment = { amount; color; description };   // breakdown by source / talent
```

Subclasses override `mitigationSegments(mit)` (e.g. base contribution + talent contribution) and `explainPerformance(mit)` (which returns `{ perf, explanation? }` comparing against player max HP).

### 5.4 `AllCooldownUsagesList` + `MitigationSegments` + `MajorDefensives/Timeline`
**Visual.** A `MajorDefensive` analyzer renders as:
- a window timeline (green bars for active mitigations),
- a damage mitigation chart (`DamageMitigationChart`),
- a row of `BoxRowEntry`s for actual+missed uses,
- an expandable detail: two-column layout (Mitigation by Talent / Mitigation by Damage Source), each row showing time + amount + a stacked-bar segment for visual weight.

`AllCooldownUsagesList` is the multi-analyzer wrapper that lays this out per cooldown.

### 5.5 `CooldownExpandable` — the generic escape hatch
**Purpose.** Generic "expandable card" used when the data doesn't fit either ladder cleanly. Accepts `checklistItems: ReactNode[]` and `detailItems: ReactNode[]` and renders the same expand/collapse + checklist UI without the analyzer base classes.

**When to use.** Bespoke cooldowns whose evaluation logic doesn't fit the checklist or mitigation model.

### 5.6 Decision tree for spec authors
```
Is this a damage-reduction window?           → extend MajorDefensive + render via AllCooldownUsagesList
Else, does cast quality depend on execution? → extend MajorCooldown + render via CooldownUsage
Else, just need an expandable card?          → use CooldownExpandable
Need to summarise CD efficiency only?        → CastEfficiencyPanel (or CooldownGraphSubSection for many)
Need a multi-cooldown comparison?            → AllCooldownUsagesList
```

---

## 6. Other primitives

### 6.1 `ProblemList`
**Purpose.** Severity-sorted paginated carousel of detected problems. One "Problem N of M" header + a caller-provided renderer.

**Inputs.** `{ problems: Problem<T>[]; events; renderer: ProblemRenderer<T>; info; label? }`.

**Visual.** Prev/next nav, severity-sorted list, automatic event-context filtering (events within `[range.start - context.before, range.end + context.after]`). Empty state = green check + "No problems found."

**When to use.** Anywhere you have a list of timestamped issues and want a focused, one-at-a-time review UI.

### 6.2 APL (Action Priority List) group
A self-contained sub-system for spec rotations modeled as priority lists.

| Component | Role |
|---|---|
| `AplSectionData` | Orchestrator — 3-area grid: rules summary, violation explanations, timeline detail |
| `AplRules` | Ordered list of priority rules with optional highlight on the "active" rule |
| `AplViolationExplanations` | Deduplicated violations ranked by frequency, each with a "Show Me!" drilldown button |
| `ViolationTimeline` | Embedded mini-`Casts` timeline focused on the violating cast (~12 s window, 5 s pre) |

Uses an `ExplanationSelectionContext` so clicking "Show Me!" updates the timeline panel below.

### 6.3 Preparation group
The pre-fight readiness section. One top-level `PreparationSection` composes the subsections:

- `EnchantmentSubSection` — gear slot enchant compliance.
- `EnhancementSubSection` — weapon enhancements (retail only).
- `GemSubSection` — gem compliance (retail only).
- `ConsumablesSubSection` — flasks + food + potions + augment runes, laid out with `SideBySidePanels` and one panel component per consumable type.

Each subsection: explanatory copy → "box row" of gear slots with pass/fail or recommendation badges → optional bulleted list of recommended item IDs. Drives off `EnchantChecker` / `GemChecker` analyzer modules via `useAnalyzer`.

### 6.4 Layout primitives (`GuideDivs`)
- `RoundedPanel` — dark rounded box (the most common data container).
- `StartAlignedRoundedPanel` — same, top-aligned content.
- `PerformanceRoundedPanel` — colored inset shadow per performance.
- `SideBySidePanels` — equal-sized horizontal layout.
- `PanelHeader` — spacing for in-panel headers.

---

## 7. Composition patterns observed in real specs

### 7.1 Typical guide skeleton (across 3 sampled specs)
```
GuideContainer
├── Section "Core Skills"
│    └── FoundationDowntimeSection(V2)         (≈ every spec)
├── Section "<core mechanic>"                  (spec-specific, e.g. Stagger, Maelstrom, HoT ramp)
│    └── SubSection... custom analyzer subsections
├── Section "Rotation & Cooldowns"
│    ├── SubSection "Rotation"   → APL or TipBoxes
│    ├── SubSection "Cooldowns"  → CastEfficiencyBar × N (talent-gated)
│    └── SubSection "<talent>"    → SpellUsageSubSection / CooldownUsage
├── Section "<defensives/utility>"             (often via MajorDefensives + AllCooldownUsagesList)
└── PreparationSection
```

### 7.2 The "spell SubSection" pattern (repeated 5–15× per guide)
```
SubSection
└── ExplanationRow                       (or GuideSection)
    ├── Explanation: prose about the spell
    └── data panel (RoundedPanel)
        ├── CastOverview                  (KPI tiles)
        ├── CastSummaryAndBreakdown       (distribution + drill-down)
        └── CastDetail                    (optional per-cast carousel)
```

### 7.3 Two recurring composition rules
1. **Analyzer-owned subsections.** Specs increasingly let analyzer modules expose their own `.guideSubsection` JSX rather than rebuilding it per-spec — this is how `restoration druid` and `elemental shaman` keep `Guide.tsx` thin. The catalog ports cleanly because each `.guideSubsection` is just a function returning the components above.
2. **Talent-gated conditional rendering.** Sections wrap themselves in `info.combatant.hasTalent(...)` — port equivalent should be a `<TalentGated talent={…}>` wrapper rather than per-call ternaries.

---

## 8. Catalog summary for the design system

A faithful port needs (at minimum) the following families:

| Family | Components |
|---|---|
| **Tokens** | `QualitativePerformance` enum, color tokens (perfect/good/ok/bad + accents), spacing scale, panel shadows |
| **Marks** | `PerformanceMark`, `PassFailCheckmark` |
| **Section layout** | `Section` (expandable), `SubSection`, `GuideContainer`, `GuideSection`, `ExplanationRow`, `Explanation`, `Para` |
| **Visualization frame** | `GuideDataWrapper` (with compact + bare modes) |
| **Stat tiles** | `StatsGrid`, `StatCard{,Value,Divider,Label}`, `PerfBadgeGrid`, `PerfBadgeCount`, `FilterBadge` |
| **Callouts** | `TipBox` (5 variants), `PerformanceTipBox`, `HelperText`, `GuideTooltip` |
| **Bars / charts** | `PerformanceBoxRow` (+ `BoxRowEntry`), `StackedBar`, `GradiatedPerformanceBar`, `PassFailBar`, `BuffUptimeBar`, `DamageTakenPointChart`, `DamageMitigationChart`, `CooldownBar`, `CastEfficiencyBar`, `CooldownGraphSubSection` |
| **Cast analysis** | `CastOverview`, `CastDetail`, `CastSummary`, `CastSummaryAndBreakdown`, `CastSequence`, `CastEfficiencyPanel`, `CastReasonBreakdownTableContents` |
| **Cooldowns** | `MajorCooldown` (analyzer base), `SpellUse`/`ChecklistUsageInfo`, `CooldownUsage`, `SpellUsageSubSection`, `MajorDefensive`, `Mitigation`/`MitigationSegment`, `MitigationSegments`, `MajorDefensives/Timeline`, `AllCooldownUsagesList`, `CooldownExpandable` |
| **Problems** | `ProblemList` |
| **APL** | `AplSectionData`, `AplRules`, `AplViolationExplanations`, `ViolationTimeline` |
| **Preparation** | `PreparationSection` + enchant / gem / enhancement / consumables subsections |
| **Foundation sections** | `FoundationGuide`, `FoundationDowntimeSection(V2)`, `FoundationCooldownSection`, `FoundationHealerManaSection`, `FoundationSupportBadge`, `ByRole` |
| **Toggles / contexts** | `HideExplanationsToggle`, `HideGoodCastsToggle`, plus the underlying explanation-visibility and spell-usage contexts |
| **Layout primitives** | `RoundedPanel`, `StartAlignedRoundedPanel`, `PerformanceRoundedPanel`, `SideBySidePanels`, `PanelHeader`, `SectionContainer`, `BareSection` |

### Design-system invariants worth preserving
- **One performance taxonomy.** Everything visual hangs off `QualitativePerformance`. Don't introduce parallel grading scales.
- **One per-event record.** `BoxRowEntry` is the lingua franca for any "row of events" view.
- **Headers via `GuideDataWrapper`.** Any visualization gets the same chrome — title, optional subtitle, optional stats pills, optional helper text.
- **Two-column "explainer | data".** `ExplanationRow` (or `GuideSection`) is the canonical content layout inside a SubSection.
- **Analyzer-owned subsections.** The strongest specs let analyzer modules expose ready-rendered subsections, so the guide file is just composition. Port the catalog with that pattern in mind — a `RestorationDruidGuide` should be ~30 lines of `<Section>`/`<SubSection>` plus module references, not ~300 lines of bespoke layout.
