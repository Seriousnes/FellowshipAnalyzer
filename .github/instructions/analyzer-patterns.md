---
applyTo: 'src/**'
---

# Analyzer Patterns (WoWAnalyzer → FellowshipAnalyzer)

Reference patterns from WoWAnalyzer's TypeScript/React architecture, adapted for C#/Blazor.

## Key Principle: State Trackers vs. Analysis Modules

| Aspect | **State Tracker** | **Analysis/Guide Module** |
|--------|------------------|---------------------------|
| Purpose | Maintain game state over time | Analyze performance, render guide |
| Listens to events | Yes — buff/debuff apply/remove, casts, resource changes | Yes — specific events relevant to the feature |
| Provides | Read-only accessors (current value, history) | Computed metrics, scorecards, guide rendering |
| Shared | Yes — multiple analyzers may depend on one tracker | No — each guide owns its analysis |
| Examples (WoW) | `SpellUsable`, `Haste`, `ResourceTracker`, `Entities` | `HotHand`, `DoomWinds`, `MaelstromWeaponSpenders` |
| Examples (Fellowship) | `SpellUsable`, `GlobalCooldown` | Guide components (`.razor` / `.razor.cs`) |

## SpellUsable Responsibilities

`SpellUsable` is the core state tracker for ability cooldowns and cast history:
- Cast history: all player casts via `Casts` (list of `TrackedAbilityCast`)
- Cooldown state: `IsAvailable`, `IsOnCooldown`, `ChargesAvailable`, `CooldownRemaining`
- Fabricates `UpdateSpellUsableEvent` events as cooldowns change

`SpellUsable` should **NOT** track:
- Windows (buff windows, combo windows) — these are analysis concepts owned by guide components
- Performance evaluations — these belong in guide components

## WoWAnalyzer Analyzer Pattern (HotHand.tsx reference)

In WoWAnalyzer, a talent analyzer like HotHand:
1. Extends `Analyzer.withDependencies({...})` for typed dependency access
2. Subscribes to events in constructor: `addEventListener(Events.applybuff.by(SELECTED_PLAYER).spell(...), handler)`
3. Maintains internal state (windows, casts, counters) built from events
4. Provides a `guideSubsection` getter returning JSX for the guide tab
5. Optionally provides a `statistic()` method for the overview tab

```typescript
class HotHand extends Analyzer.withDependencies({
  spellUsable: SpellUsable,
  haste: Haste,
}) {
  private windows: HotHandWindow[] = [];
  
  constructor(options) {
    super(options);
    this.addEventListener(Events.applybuff.by(SELECTED_PLAYER).spell(HOT_HAND_BUFF), this.onApply);
    this.addEventListener(Events.removebuff.by(SELECTED_PLAYER).spell(HOT_HAND_BUFF), this.onRemove);
    this.addEventListener(Events.cast.by(SELECTED_PLAYER), this.onCast);
  }
  
  get guideSubsection() {
    return <GuideSection spell={TALENTS.HOT_HAND_TALENT} ...>
      <CastOverview stats={this.buildOverviewStats()} />
      <CastDetail casts={this.buildPerCastData()} />
    </GuideSection>;
  }
}
```

## FellowshipAnalyzer Equivalent

In FellowshipAnalyzer, guide components combine analysis + rendering. The guide Razor component (or its code-behind) contains:
1. Analysis logic that computes evaluations from `SpellUsable` data
2. Scorecard computation
3. Blazor rendering via the `.razor` template

Guide components receive state trackers as parameters and compute their own derived analysis (windows, combos, evaluations) without exposing these concepts outside the component.

## WoWAnalyzer Module Organization (Enhancement Shaman reference)

```
modules/
├── core/              # Customized shared modules (SpellUsable overrides)
├── normalizers/       # Event pre-processing, ordering, linking
├── talents/           # One analyzer per talent (analysis + guide in same file)
│   ├── HotHand.tsx
│   ├── DoomWinds.tsx
│   └── ...
├── spells/            # Core ability analyzers
├── hero/              # Hero talent analyzers
├── resourcetracker/   # Resource-specific trackers and graphs
├── features/          # Cross-cutting (AlwaysBeCasting, CooldownTracker)
└── guide/             # Guide-only assembly modules
```

Key: each talent/feature file contains both the event handling, analysis logic, AND guide rendering.

## WoWAnalyzer State Tracking Patterns

### Haste Tracking
- Maintains `current` haste percentage
- Listens to buff apply/remove/stack events for known haste buffs
- Listens to stat change events from gear/effects
- Multiplicative haste stacking: `(1 + h1) * (1 + h2) - 1`

### Resource Tracking (ResourceTracker)
- Abstract base: subclass sets `resource`, `maxResource`, `baseRegenRate`
- Tracks `builders` (generated/wasted/casts per spell) and `spenders` (spent/casts per spell)
- Maintains `resourceUpdates[]` timeline: spend, gain, drain, regenCap, rateChange, refund
- Handles multi-update buffering for events on same timestamp

### Cooldown Tracking (SpellUsable)
- Per-spell `CooldownInfo`: chargesAvailable, maxCharges, expectedEnd
- Fabricates `UpdateSpellUsableEvent` when cooldown state changes
- History accessible via `history(spellId)`

### Buff/Debuff Uptime (Entities/Enemies)
- `getDebuffHistory(spellId)` → `{start, end}[]`
- `getBuffUptime(spellId)` → milliseconds
- Automatically merges overlapping periods

## Event Listener Patterns

### WoWAnalyzer
```typescript
Events.applybuff.by(SELECTED_PLAYER).spell(SPELLS.HOT_HAND_BUFF)
Events.cast.by(SELECTED_PLAYER)
Events.damage.by(SELECTED_PLAYER).spell([spell1, spell2])
```

### FellowshipAnalyzer
```csharp
Events.ApplyBuff.By(Analyzer.SELECTED_PLAYER).Spell(RimeSpells.WintersEmbrace.Id)
Events.Cast.By(Analyzer.SELECTED_PLAYER)
Events.Damage.By(Analyzer.SELECTED_PLAYER).Spell(RimeSpells.GlacialBlast.Id, RimeSpells.IceComet.Id)
```

## Guide Component Patterns (WoWAnalyzer)

```
<GuideSection>         → Top-level section with explanation + data
<CastOverview>         → Summary stats across all occurrences  
<CastDetail>           → Per-occurrence breakdown with expandable rows
<CastSequence>         → Timeline/sequence visualization
<BuffUptimeBar>        → Buff uptime visual bar
```

Guide assembly in WoWAnalyzer (`Guide.tsx`):
```typescript
export default function Guide({ modules }: GuideProps<typeof CombatLogParser>) {
  return (
    <>
      {modules.hotHand.guideSubsection}
      {modules.doomWinds.guideSubsection}
      <SubSection title="Resources">
        {modules.maelstromDetails.guideSubsection}
      </SubSection>
    </>
  );
}
```
