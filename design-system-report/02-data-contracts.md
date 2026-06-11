# Data Contracts Appendix

The full set of data shapes a framework port must mirror to drive the Guide UI. Interfaces are written in TypeScript because that's the source language, but the shapes are framework-neutral — translate to records / DTOs / Pydantic / etc. as appropriate.

Each entry covers:
- **Shape** — the structural type.
- **Used by** — which catalog components consume it.
- **Origin** — where the data is produced in the source pipeline (analyzer modules, helpers, manual construction).
- **Notes** — gotchas, optionality semantics, units.

---

## 1. Cross-cutting primitives

### 1.1 `QualitativePerformance`
The single performance enum every visualization grades against. Order matters: Perfect is best, Fail is worst.

```ts
enum QualitativePerformance {
  Perfect = 'perfect',
  Good = 'good',
  Ok = 'ok',
  Fail = 'fail',
}
```

**Used by.** Practically every component (color, icon, tooltip text).
**Origin.** Returned from analyzer evaluation functions; combined via a "worst-of" reducer when aggregating.
**Notes.** Treat as a *closed* set. Adding a fifth bucket means touching every color stop, mark icon, badge grid layout, and tooltip helper.

### 1.2 Color tokens
```ts
PerfectColor    // blue   --guide-perfect-color
GoodColor       // green  --guide-good-color
OkColor         // yellow --guide-ok-color
BadColor        // red    --guide-bad-color

VeryBadColor    // deeper red, used for chart highlights
MediocreColor   // orange, intermediate state
AvailableColor  // cooldown-ready highlight (CooldownBar)
```
Exposed as CSS custom properties on `:root`. Components read them at module load, so theming requires either CSS-variable swapping (live) or a rebuild if you choose to inline.

### 1.3 `Spell` reference
Universal spell reference object. Most components accept either a `Spell` or a raw `spellId: number`.

```ts
interface Spell {
  id: number;
  name: string;
  icon: string;          // icon name; full URL built via iconUrl(icon)
  rank?: number;
  manaCost?: number;     // healer/caster spells
  // ...other optional fields, not used by the design system
}
```

**Used by.** `CastOverview`, `CastDetail`, `CastSummary`, `CastSequence`, `CastEfficiencyPanel`, `CastSummaryAndBreakdown`, `BuffUptimeBar`, `GuideSection`.
**Origin.** A static spell catalog (in the source, `common/SPELLS`).
**Notes.** Ports can collapse this to `{ id, name, icon }`. The icon resolver should accept the bare icon name and produce a CDN URL.

### 1.4 `BoxRowEntry`
The universal "one event, graded" record.

```ts
interface BoxRowEntry {
  value: QualitativePerformance;
  tooltip?: ReactNode | string;
  className?: string;        // optional state hook, e.g. "selected"
}
```

**Used by.** `PerformanceBoxRow`, `CastSummary` (in breakdown), `CastSummaryAndBreakdown`, all `MajorCooldown` / `MajorDefensive` UIs.
**Origin.** Hand-rolled, or via `spellUseToBoxRowEntry(spellUse, fightStart)` for cooldown analyzers.
**Notes.** Make this the lingua franca for any "row of events" view.

### 1.5 Tooltip content
Wherever a `tooltip` field accepts `ReactNode`, the data contract is "renderable content" — strings, nodes, structured components. A port should accept either:
- a plain string, or
- a structured object the renderer knows how to compose (e.g. `{ kind: 'GuideTooltip', timestamp, performance, items: [...] }`).

The most common structured tooltip is `GuideTooltip` (see § 6.1).

---

## 2. Visualization frame

### 2.1 `GuideDataWrapperProps`
```ts
interface GuideDataWrapperProps {
  title: string | ReactNode;
  subtitle?: string;
  stats?: ReactNode;           // typically a row of StatCard or PerfBadge
  statsHelperText?: ReactNode;
  helperText?: ReactNode;
  icon?: string;               // icon name (compact mode only)
  bare?: boolean;              // drop the box chrome
  compact?: boolean;           // single-row layout
  className?: string;
  children?: ReactNode;
}
```
**Used by.** Wraps `CastOverview`, `CastDetail`, `CastSummary`, `CastSequence`, and arbitrary spec visualizations.
**Notes.** `compact: true` switches to a single-row layout where `stats` sits to the right of the title block. `icon` is only honored in compact mode.

---

## 3. Cast analysis shapes

### 3.1 `StatisticData` (CastOverview)
```ts
interface StatisticData {
  value: string;                          // displayed in StatCardValue
  label: string;                          // displayed in StatCardLabel
  tooltip: ReactNode;
  performance?: QualitativePerformance;   // tints the card
}
```
**Used by.** `CastOverview.stats[]`.
**Notes.** When `performance` is omitted, the card renders white/neutral. Treat as required for any KPI that has a graded outcome.

### 3.2 `PerCastStat` (CastDetail)
```ts
interface PerCastStat {
  value: ReactNode;            // string, number, image, etc.
  label: string;
  tooltip?: ReactNode;
  performance?: QualitativePerformance;
}
```
Same idea as `StatisticData`, but per-cast and richer in value type (can be an image).

### 3.3 `PerCastData` (CastDetail)
```ts
interface PerCastData {
  performance: QualitativePerformance;    // overall grade for this cast
  stats: PerCastStat[];                   // populates the StatsGrid
  tooltip?: ReactNode;
  timestamp: string;                      // pre-formatted (e.g. "1:23.4")
  details?: ReactNode;                    // free-form body, typically PerformanceTipBox
  detailsIcon?: ReactNode | null;         // override the default tip icon; null hides it
  additionalContent?: { title?: string; content: ReactNode };
}
```
**Used by.** `CastDetail.casts[]`.
**Notes.** Timestamps arrive pre-formatted. A port should keep timestamp formatting at the boundary (one helper) rather than re-deriving inside the component.

### 3.4 `CastEvaluation` (CastSummary)
```ts
interface CastEvaluation {
  timestamp: number;                      // milliseconds, fight-relative
  performance: QualitativePerformance;
  reason: string;                         // short description shown in box tooltip
}
```
**Used by.** `CastSummary.casts[]`.

### 3.5 `CastSequenceEntry<T>` + `CastInSequence` (CastSequence)
```ts
interface CastInSequence {
  timestamp: number;
  spellId: number;
  spellName: string;
  icon: string;                           // icon name
  performance?: QualitativePerformance;   // sets outline color
  outlineColor?: string;                  // override
  ghosted?: boolean;                      // grayscale + low opacity
  tooltip?: ReactNode;
}

interface CastSequenceEntry<T> {
  data: T;                                // arbitrary window context (e.g. phase config)
  casts: CastInSequence[];
  start?: number;
  end?: number;
}
```
**Used by.** `CastSequence.sequences[]`.
**Notes.** Generic `<T>` is for window-level data only — `castTimestamp: (data: T) => string` turns it into a header label.

### 3.6 `CastReasonBreakdownTableContents` props
```ts
interface CastReasonBreakdownProps<Data, Reason> {
  casts: Data[];
  label: (reason: Reason) => ReactNode;
  containerType?: 'tbody' | string;       // default 'tbody'
  possibleReasons: Reason[];
  badReason?: Reason;
}
```
**Used by.** Cast-reason breakdown tables. Each row = one reason + count + a `PassFailBar`.

### 3.7 `CastSummaryAndBreakdown` props (compressed)
```ts
interface CastSummaryAndBreakdownProps {
  spell: Spell | number;
  castEntries: BoxRowEntry[];
  perfectLabel?: string;          // override "Perfect" copy
  goodLabel?: string;
  okLabel?: string;
  badLabel?: string;
  perfectExtraExplanation?: ReactNode;
  goodExtraExplanation?: ReactNode;
  okExtraExplanation?: ReactNode;
  badExtraExplanation?: ReactNode;
  includePerfectCastPercentage?: boolean;
  includeGoodCastPercentage?: boolean;
  includeOkCastPercentage?: boolean;
  includeBadCastPercentage?: boolean;
  usesInsteadOfCasts?: boolean;   // labels "uses" instead of "casts"
  onClickBox?: (index: number) => void;
}
```
**Notes.** The bucket-level labels and explanations are the main customization surface. A port can drop the per-bucket flags by always showing all enabled buckets.

---

## 4. Visualization data shapes

### 4.1 `StackedBarSegment`
```ts
interface StackedBarSegment {
  label: string;
  value: number;
  color: string;            // free-form, caller-supplied
  tooltip?: ReactNode;
}

interface StackedBarProps {
  segments: StackedBarSegment[];
  height?: number;                                       // default 35
  hideLegend?: boolean;
  minSegmentPercent?: number;                            // hide <0.5% by default
  tooltipFormat?: (seg: StackedBarSegment, percent: number) => ReactNode;
}
```
**Notes.** No `QualitativePerformance` coupling — this is the only major bar that's pure categorical composition.

### 4.2 `GradiatedPerformanceBar` props
```ts
type Slot = number | { count: number; label: ReactNode };

interface GradiatedPerformanceBarProps {
  perfect?: Slot;
  good?: Slot;
  ok?: Slot;
  bad?: Slot;
}
```
**Notes.** Omitting a slot drops it from the bar.

### 4.3 `PassFailBar`
```ts
interface PassFailBarProps {
  pass: number;
  total: number;
  className?: string;
  passTooltip?: string;
  failTooltip?: string;
}
```
**Notes.** Width clamped to 1.0 (pass cannot exceed total). Native `title` attribute for tooltips.

### 4.4 `BuffUptimeBar` data
```ts
interface TrackedBuffEvent {
  start: number;                                // ms, absolute
  end?: number;                                 // ms; if absent, treat as until fight end
  stackHistory?: { timestamp: number; stacks: number }[];
}

interface BuffUptimeBarProps {
  spell: Spell;
  buffHistory: TrackedBuffEvent[];
  startTime: number;
  endTime: number;
  barColor?: string;                            // default purple
  backgroundBarColor?: string;
  maxStacks?: number;
  averageStacksTooltip?: ReactNode;
}
```
**Notes.** When `stackHistory` is present, the timeline renders as an area chart whose height varies with stack count. When absent, flat bars indicate buff present/absent.

### 4.5 `DamageTakenPointChart` data
```ts
interface TrackedHit {
  event: DamageEvent;
  mitigated: QualitativePerformance;
}

interface DamageTakenPointChartProps {
  hits: TrackedHit[];
  tooltip: (hit: TrackedHit) => ReactNode;       // mandatory custom tooltip
  showSmallHits?: boolean;                        // include <10% HP hits
}
```
**Notes.** Hits are grouped internally by source. `DamageEvent` is the parsed combat-log event; a port needs at minimum `{ timestamp, sourceID, ability: { guid }, amount, hitPoints, maxHitPoints }`.

### 4.6 `DamageMitigationChart` data
Backed by the `MajorDefensive` analyzer instances themselves, not flat data:

```ts
interface DamageMitigationChartProps {
  analyzers: MajorDefensive<Apply, Remove>[];
  onHover: SignalListener;                       // Vega hover signal
  yScale?: number;                               // y-axis scale factor
}
```
**Notes.** A port can substitute Vega for any other charting library, but the data it expects is `{ timestamp, dpsTaken, school }` time series + a list of `{ start, end, label }` rectangles.

---

## 5. Cooldown / SpellUse contracts

### 5.1 `CooldownTrigger<E>`
```ts
interface CooldownTrigger<E extends AnyEvent> {
  event: E;                       // the triggering event (typically CastEvent)
  // Subclasses extend with context: enemy?, targetCount?, etc.
}
```
**Used by.** Subclasses of `MajorCooldown` extend this to attach per-cast context.

### 5.2 `ChecklistUsageInfo`
```ts
interface ChecklistUsageInfo {
  check: string;                              // stable id, e.g. "deathmark-up"
  timestamp: number;                          // absolute ms
  performance: QualitativePerformance;
  summary: ReactNode;                         // shown inline in box tooltip
  details: ReactNode;                         // shown in detail panel
}
```

### 5.3 `SpellUse`
```ts
interface SpellUse {
  event: AnyEvent;
  checklistItems: ChecklistUsageInfo[];
  performance: QualitativePerformance;        // worst-of items, with overrides
  performanceExplanation?: ReactNode;         // override default label
  extraDetails?: ReactNode;                   // free-form panels below checklist
}
```
**Origin.** Produced by `MajorCooldown.explainPerformance(cast)`.
**Notes.** `SpellUse → BoxRowEntry` via `spellUseToBoxRowEntry(use, fightStart)`.

### 5.4 Synthetic missed-cast entries
For padding the box row to cover *possible* casts:

```ts
const MissingCastBoxEntry = {
  value: QualitativePerformance.Fail,
  tooltip: <…>,  // "Potential cast went unused"
};
const PossibleMissingCastBoxEntry = {
  value: QualitativePerformance.Ok,
  tooltip: <…>,  // "Potential cast went unused, but may have been intentionally saved"
};
```
Logic (per `CooldownUsage`):
- If `actualCasts === 0` and `possibleUses > 1`, fill all possible uses with `MissingCastBoxEntry`.
- Else, fill missing uses with `PossibleMissingCastBoxEntry` up to half (`possibleUses / 2`), rest with `MissingCastBoxEntry`.

### 5.5 `Mitigation` + `MitigationSegment`
```ts
interface MitigatedEvent {
  event: DamageEvent | AnyEvent;
  mitigatedAmount: number;
}

interface Mitigation {
  start: number;                              // ms, absolute
  end: number;
  mitigated: MitigatedEvent[];
  amount: number;                             // sum of mitigatedAmount
  maxAmount?: number;                         // cap for absorb-style CDs
}

interface MitigationSegment {
  amount: number;                             // contribution amount
  color: string;                              // segment fill
  description: ReactNode;                     // human-readable source
}
```
**Used by.** `MajorDefensive` subclasses; `MitigationSegments`, `AllCooldownUsagesList`, `DamageMitigationChart`.

### 5.6 `MajorDefensive` analyzer surface (the parts the UI consumes)
```ts
abstract class MajorDefensive<Apply, Remove> {
  readonly mitigations: Mitigation[];
  mitigationSegments(m: Mitigation): MitigationSegment[];
  explainPerformance(m: Mitigation): { perf: QualitativePerformance; explanation?: ReactNode };
  mitigationPerformance(maxHp: number): BoxRowEntry[];   // includes missed-use padding
}
```

### 5.7 `MajorCooldown` analyzer surface
```ts
abstract class MajorCooldown<Cast extends CooldownTrigger<AnyEvent>> {
  readonly uses: SpellUse[];
  readonly spell: Spell;
  description(): ReactNode;
  explainPerformance(cast: Cast): SpellUse;
  cooldownPerformance(): BoxRowEntry[];      // built from uses; not yet padded
}
```

### 5.8 `CooldownExpandable` props
```ts
interface CooldownExpandableProps {
  header: ReactNode;
  checklistItems: ReactNode[];                // each pre-rendered (mark + text)
  detailItems: ReactNode[];                   // shown in the expanded body
  perf?: QualitativePerformance;              // border / mark color
}
```
**Notes.** This is the escape hatch — bypasses analyzer base classes.

---

## 6. Auxiliary structured content

### 6.1 `GuideTooltip` body
```ts
interface GuideTooltipProps {
  formatTimestamp: (ts: number) => string;
  performance: QualitativePerformance;
  tooltipItems: { perf: QualitativePerformance; detail: string }[];
  timestamp: number;
}
```
**Used by.** Standard tooltip for cast / mitigation visualizations.

### 6.2 `TipBox` / `PerformanceTipBox`
```ts
interface TipBoxProps {
  children: ReactNode;
  icon?: ReactNode;
  title?: string;
  type?: 'info' | 'note' | 'success' | 'warning' | 'error';   // default 'info'
  hideIcon?: boolean;
}

interface PerformanceTipBoxProps extends Omit<TipBoxProps, 'type'> {
  performance: QualitativePerformance;        // chooses icon + accent
}
```
**Notes.** A port should keep the type variants stable — they map to ARIA roles (`alert` for warning/error, `note` for others).

### 6.3 `ProblemList`
```ts
interface Problem<T> {
  data: T;                                    // payload for the renderer
  range: { start: number; end: number };      // timestamp window
  severity: number;                           // higher = more important
  context: { before: number; after: number }; // ms padding for event slice
}

type ProblemRenderer<T> = (props: {
  problem: Problem<T>;
  events: AnyEvent[];                         // filtered to range ± context
  info: Info;
}) => ReactNode;

interface ProblemListProps<T> {
  problems: Problem<T>[];
  events: AnyEvent[];
  renderer: ProblemRenderer<T>;
  info: Info;
  label?: string;
}
```
**Notes.** Sort is by `severity` descending. Empty state is its own rendered state.

### 6.4 `Info` context object
A grab-bag of fight metadata that many components consume implicitly:

```ts
interface Info {
  playerId: number;
  fightStart: number;
  fightEnd: number;
  combatant: Combatant;                       // talents, gear, race
  // ...spec/role/abilities references
}
```
**Notes.** A port should establish a single ambient "fight info" context (provider / DI / global) rather than threading these through every component.

### 6.5 `Casts` timeline embedding (APL `ViolationTimeline`)
```ts
interface ViolationTimelineProps {
  violation: AplViolation;
  events: AnyEvent[];
  apl: Apl;
  results: AplResult;
  secondsShown?: number;                      // default 12s; 5s pre-violation
}
```

---

## 7. Preparation contracts

```ts
interface PreparationSectionProps {
  recommendedEnchantments?: Record<SlotKey, number[]>;        // item IDs per slot
  recommendedWeaponEnhancements?: Record<SlotKey, number[]>;
  recommendedFlasks?: number[];
  recommendedFoods?: number[];
  recommendedGems?: number[];
  expansion?: Expansion;                                       // gates retail-only subsections
}
```
**Notes.** All recommendation arrays are item IDs into a static catalog. A port should keep this as a flat catalog lookup rather than nesting recommendation objects.

---

## 8. Analyzer-emitted JSX (port note)

Several specs in the source rely on **analyzer-owned subsections** — i.e. an analyzer module exposes:
```ts
class MyAnalyzer extends Analyzer {
  get guideSubsection(): ReactNode { /* returns the spec subsection */ }
}
```

A port can mirror this with any DI mechanism — analyzers expose a `render()` / `subsection()` method that returns the framework's view type. The data contracts above are the only thing the analyzer needs to produce; the view itself composes them.

This is the highest-leverage pattern in the codebase: it keeps spec `Guide` files small and pushes presentation into the same module that owns the analysis logic.

---

## 9. Quick reference — what produces what

| Producer | Output type | Consumed by |
|---|---|---|
| `MajorCooldown.explainPerformance(cast)` | `SpellUse` | `spellUseToBoxRowEntry` → `BoxRowEntry` → `PerformanceBoxRow` |
| `MajorCooldown.cooldownPerformance()` | `BoxRowEntry[]` | `CooldownUsage` (pads missed) → `SpellUsageSubSection` |
| `MajorDefensive` (instance) | `Mitigation[]`, `MitigationSegment[]` | `AllCooldownUsagesList`, `DamageMitigationChart` |
| `CastEfficiency.getCastEfficiencyForSpell(spell)` | `{ maxCasts, casts, efficiency }` | `CastEfficiencyPanel`, `CooldownUsage` padding logic |
| `AlwaysBeCasting` / `MeleeUptimeAnalyzer` / `DowntimeDebuffAnalyzer` | `{ uptime%, gaps[], segments[] }` | `FoundationDowntimeSectionV2` |
| `EnchantChecker` / `GemChecker` | `{ slot → status }` | Preparation subsections |
| `Apl.checker(events)` | `AplResult` (successes, violations) | `AplSectionData`, `AplRules`, `AplViolationExplanations` |

These are the seven main "data pipes" a port needs. Everything else in the catalog is presentation around these shapes.
