---
applyTo: 'src/**'
---

# Analyzer Patterns


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

## Analyzer Pattern

In FellowshipAnalyzer, guide components combine analysis + rendering. The guide Razor component (or its code-behind) contains:
1. Analysis logic that computes evaluations from `SpellUsable` data
2. Scorecard computation
3. Blazor rendering via the `.razor` template

Guide components receive state trackers as parameters and compute their own derived analysis (windows, combos, evaluations) without exposing these concepts outside the component.

## Module Organization (Enhancement Shaman reference)

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

## State Tracking Patterns

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

### FellowshipAnalyzer
```csharp
Events.ApplyBuff.By(Analyzer.SELECTED_PLAYER).Spell(RimeSpells.WintersEmbrace.Id)
Events.Cast.By(Analyzer.SELECTED_PLAYER)
Events.Damage.By(Analyzer.SELECTED_PLAYER).Spell(RimeSpells.GlacialBlast.Id, RimeSpells.IceComet.Id)
```

## Guide Component Patterns

```
<GuideSection>         → Top-level section with explanation + data
<CastOverview>         → Summary stats across all occurrences  
<CastDetail>           → Per-occurrence breakdown with expandable rows
<CastSequence>         → Timeline/sequence visualization
<BuffUptimeBar>        → Buff uptime visual bar
```
