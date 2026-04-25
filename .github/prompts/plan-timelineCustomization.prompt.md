# Plan: Timeline Customization (Auras & Cooldown Lanes)

Users can show/hide and reorder cooldown lanes and aura priority levels on the Timeline.
Configuration is persisted per-hero in `localStorage` and remembered across sessions.

---

## Design Decisions

| Question | Answer |
|---|---|
| Available pool — cooldowns | Any spell with a `UpdateSpellUsableEvent` where `SourceId == PlayerId` |
| Available pool — auras | Any `TrackedBuffEvent` targeting the player that is not a debuff (`IsDebuff == false`); any source |
| Config UI location | Separate settings modal triggered by a cog button on the timeline |
| Cog button position | Same position as WoWA's `TimelineConfiguration` (top-right of the timeline area) |
| Sort order scope | Aura level priority (numeric field per aura) + cooldown lane vertical order (drag-and-drop and order field in modal) |
| Persistence scope | Per-hero (keyed by hero class name, e.g. `Rime`) |
| Persistence mechanism | `localStorage`, key: `timeline-config:{heroClassName}` |
| Stored shape | Full per-spell record: `{ spellId, visible, order }` |
| Modal layout | Two tabs: **Auras** and **Cooldowns** |
| Aura ordering in modal | Numeric priority input field per row (lower = bottom/closest to cast bar) |
| Cooldown lane ordering | Drag-and-drop (HTML5 drag events) *and* numeric order field in modal |

---

## New Files

### `FellowshipAnalyzer.Components/Timeline/TimelineConfig.cs`
```csharp
namespace FellowshipAnalyzer.Components.Timeline;

public sealed record AuraConfigEntry(int SpellId, bool Visible, int Priority);
public sealed record CooldownConfigEntry(int SpellId, bool Visible, int SortOrder);

public sealed class TimelineConfig
{
    public List<AuraConfigEntry> Auras { get; init; } = [];
    public List<CooldownConfigEntry> Cooldowns { get; init; } = [];
}
```

### `FellowshipAnalyzer.Components/Timeline/TimelineConfigService.cs`
- Injected as scoped DI service.
- Reads/writes `localStorage` via `IJSRuntime` (calls `localStorage.getItem` / `localStorage.setItem`).
- Key: `timeline-config:{heroClassName}` (hero class name = hero `Auras` type's declaring assembly simple name, e.g. `"Rime"`).
- `Task<TimelineConfig> LoadAsync(string heroKey)` — deserializes JSON, returns empty config if null.
- `Task SaveAsync(string heroKey, TimelineConfig config)` — serializes to JSON and stores.
- `TimelineConfig MergeWithDefaults(TimelineConfig stored, IEnumerable<int> allAuraIds, IEnumerable<int> allCooldownIds, IReadOnlySet<int> defaultHighlightedAuras, IEnumerable<SpellbookAbility> spellbook)`:
  - Auras seen in log but not in stored → added with `Visible = defaultHighlightedAuras.Contains(spellId)`, `Priority = 0`.
  - Cooldowns seen in log but not in stored → added with `Visible = spellbook entry has TimelineSortIndex != null`, `SortOrder = spellbook entry's TimelineSortIndex ?? int.MaxValue`.

### `FellowshipAnalyzer.Components/Timeline/TimelineSettingsModal.razor`
- Parameters:
  - `bool IsOpen` / `EventCallback OnClose`
  - `TimelineConfig Config` / `EventCallback<TimelineConfig> OnConfigChanged`
  - `IReadOnlyList<(int SpellId, string Name, string? IconUrl)> AvailableAuras`
  - `IReadOnlyList<(int SpellId, string Name, string? IconUrl)> AvailableCooldowns`
- Two tabs: **Auras** and **Cooldowns**.
- **Auras tab**: sorted list by `Priority`; each row: spell icon + name, visibility checkbox, numeric priority `<input type="number">`.
- **Cooldowns tab**: sorted list by `SortOrder`; each row: spell icon + name, visibility checkbox, numeric order field, HTML5 drag handle (`draggable="true"`, `@ondragstart` / `@ondragover` / `@ondrop`).
- Emits `OnConfigChanged` on every change (no explicit Save button — live update; auto-saves via `TimelineConfigService`).

---

## Modified Files

### `Timeline.razor`

1. **Gear button** — add a `<button>` with a cog icon in the top-right of the timeline header, opening `TimelineSettingsModal`.
2. **Aura data** — broaden the aura query from self-buffs only to **all `TrackedBuffEvent`s targeting the player that are not debuffs** (any source).
3. **Apply config to cooldown rows**:
   - Filter to `Visible == true` entries in config (or show by default if not yet in config).
   - Sort by `config.Cooldowns[spellId].SortOrder`.
4. **Apply config to AuraBar** — pass `AuraPriorities` (a `Dictionary<int, int>` from config) and a `VisibleAuraIds` set to `AuraBar`.
5. **Load config on `OnParametersSetAsync`** via `TimelineConfigService`, using the hero key derived from the `Auras` module type.
6. **Build available lists** (for the modal) from the full event set before filtering.

### `AuraBar.razor`

- Add `[Parameter] public IReadOnlyDictionary<int, int>? AuraPriorities { get; set; }` — maps spellId → priority level.
- Add `[Parameter] public IReadOnlySet<int>? VisibleAuraIds { get; set; }` — when set, only renders auras in this set (replaces/extends existing `HighlightedSpellIds`).
- In `OnParametersSet`, when sorting auras for greedy level assignment, sort by `AuraPriorities[spellId]` ascending first (lower priority = lower level = bottom row).

---

## Potential Blockers

- **`SpellbookAbility` icon/name resolution** — `Timeline.razor` already resolves icons via the `Abilities` module; confirm `SpellbookAbility.PrimarySpell` carries the icon URL needed for the modal rows.
- **`TrackedBuffEvent` source** — confirm that buffs from friendly players (not self) appear in `combatants.Selected.Buffs` or whether a different collection is needed for cross-player auras.
- **`TimelineSortIndex` property** — confirm it exists on `SpellbookAbility` (seen referenced in `Timeline.razor` but not yet read in full).
- **JS interop during SSR** — `TimelineConfigService` calls `localStorage` via JS. Since the app uses Interactive-Auto (Blazor Server first), `IJSRuntime` may not be available on first render. Guard with `OperationCanceledException` / `JSDisconnectedException` and apply defaults until client-side hydration.

---

## Out of Scope

- Debuffs / enemy auras.
- Global (cross-hero) config.
- Per-report config (all reports for the same hero share the same config).
- Drag-and-drop for the Auras tab (numeric field only, as decided).
