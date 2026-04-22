---
name: analyze-event-schema
description: "Analyze the unique event types and all per-type properties in a Fellowship log JSON. Use when: inspecting raw-report.json to understand what event types exist, what properties each type can have, identifying properties missing from or mismatched with C# event classes in src/FellowshipAnalyzer.Core/Events/, or diagnosing deserialization bugs."
argument-hint: "Path to the log JSON file to analyze"
---

# Analyze Event Schema

Run the `event-schema` tool against a log JSON and compare the results against the C# event model in `src/FellowshipAnalyzer.Core/Events/` to find deserialization mismatches.

## Use When

- A user wants to know what event types appear in a log.
- A user wants all properties for a specific event type.
- A user suspects a C# event class is missing properties or has wrong property names.
- A user wants to find properties that would silently not deserialize from the JSON.
- A user is designing or reviewing changes to event classes in `Events/`.

## Command

Run from the repository root:

```powershell
dotnet run src/FellowshipAnalyzer.Tools/event-schema.cs <log-json>
```

Example:

```powershell
dotnet run src/FellowshipAnalyzer.Tools/event-schema.cs src/FellowshipAnalyzer.FellowshipLogs/raw-report.json
```

## Input Shapes Supported

- A raw report JSON with events at `data.reportData.report.events.data`
- A top-level array of event objects
- Any nested JSON document where one array contains event objects

## Expected Output

1. **Summary table** — all event types sorted by count, with % of total and unique property count.
2. **Per-type table** — for each event type: every property seen, its frequency (`always` = present in every event, otherwise `N/total (X%)`), the JSON value kind(s) (`Number`, `String`, `Boolean`, `Object`, `Array`), and for object-type properties: all child property names observed.

## Comparing Against C# Event Classes

### Deserialization Configuration

The system uses:
- `JsonNamingPolicy.CamelCase` — maps C# `SourceId` → JSON `sourceId`
- `PropertyNameCaseInsensitive = true` — `sourceID` in JSON also matches `SourceId` in C#
- `FSLJsonConverter<Event>` — dispatches to the concrete subtype based on the `type` discriminator field

The C# class for a given `type` value is found by:
1. `[FSLEventDiscriminator("type")]` attribute on the class, OR
2. Class name stripped of "Event" suffix, lowercased (e.g. `DeathEvent` → `"death"`)

### Mapping Procedure

For each event type in the tool output:

1. Find the matching C# class in `src/FellowshipAnalyzer.Core/Events/`.
2. List the C# class's properties, applying `JsonNamingPolicy.CamelCase` to get expected JSON property names.
   - Exception: properties with `[JsonPropertyName("...")]` use that name instead.
   - `[JsonIgnore]` properties are excluded from JSON entirely.
3. Compare against the tool output's property table for that event type.

### Flags to Raise

| Situation | Severity | Notes |
| --- | --- | --- |
| JSON property `always` present but no matching C# property | High | Data is silently dropped |
| JSON property sometimes present but no matching C# property | Medium | Optional data silently dropped |
| C# property is non-nullable but JSON property is not `always` | High | Will be default-initialized to 0/false/null instead of failing |
| C# property is non-nullable but JSON property is `sometimes` | Medium | Same — will be 0/false when absent |
| JSON property is `Number` but C# property is `bool` (or vice versa) | High | Type mismatch |
| C# property exists but JSON property never appears for that type | Low | Likely synthetic/computed; confirm it is set by a normalizer or analyzer |

---

## Known Mismatches (from `raw-report.json`)

The following are confirmed structural mismatches between the C# event model and the actual JSON as of the last analysis run. Run the tool again to check if they have been resolved.

### Universal properties not modeled anywhere

Every event in the log has these properties, but no C# base class models them:

| JSON Property | Type | Notes |
| --- | --- | --- |
| `fight` | `Number` | Fight ID within the report — present on every non-combatantinfo event |
| `sourceMarker`, `targetMarker` | `Number` | Present on many event types; marker/raid-target assignments |

### Advanced details are nested, not flat

The JSON stores advanced combat details (hit points, facing, position) inside a nested `sourceResources` or `targetResources` object:

```json
"sourceResources": {
  "hitPoints": 42000,
  "maxHitPoints": 50000,
  "absorb": 0,
  "facing": 1.57,
  "x": 123.4,
  "y": 456.7,
  "resources": { ... }
}
```

However, the C# classes (`DamageEvent`, `HealEvent`, `BaseCastEvent`, etc.) model these as **direct top-level properties** (`HitPoints`, `MaxHitPoints`, `Facing`, `X`, `Y`, etc.). Because no `[JsonPropertyName]` mapping exists to bridge them, these properties will **never deserialize** — they will silently remain `0` or `null`.

Affected C# properties: `HitPoints`, `MaxHitPoints`, `Absorb` (on damage types), `Facing`, `X`, `Y`.

### `BaseCastEvent` has many properties absent from `cast` events

The JSON `cast` event schema is minimal:

```
ability, activation, fake, fight, sourceID, sourceInstance, sourceMarker,
sourceResources, targetID, targetInstance, targetMarker, timestamp, type
```

`BaseCastEvent` declares many additional properties that do **not** appear as top-level JSON properties on cast events:

| C# Property | JSON Present? | Likely Reality |
| --- | --- | --- |
| `Absorb` | No | Nested inside `sourceResources` for other types; not on cast |
| `Armor` | No | Same — only a `damage` advanced-detail |
| `AttackPower` | No | Same |
| `ClassResources` | No | Not present on cast events at all |
| `Facing`, `X`, `Y` | No (nested) | Inside `sourceResources.facing`, not top-level |
| `HitPoints`, `MaxHitPoints` | No (nested) | Inside `sourceResources.hitPoints` etc. |
| `ItemLevel` | No | Not present on cast events |
| `MapID` | No | Not present on any observed events |
| `RawResourceCost`, `ResourceCost` | No | Not present on any observed events |
| `ResourceActor` | No | Not present on cast events |
| `SpellPower` | No | Not present on cast events |
| `AbilityGameId` | No | Not a JSON property; would need to be copied from `ability.guid` |
| `Target` (`ICastTarget`) | No | Not a JSON property; synthetic |
| `GlobalCooldown`, `Channel`, `Meta` | No | Synthetic, set by normalizers |

### `BuffEvent` properties absent from buff event types

`BuffEvent` declares `AbilityGameId`, `TargetIsFriendly`, `SourceIsFriendly` — none of these appear in JSON `applybuff`, `removebuff`, etc. events.

JSON buff events also have properties not modeled in C#:
- `duration` (`Number`) — on `applybuff`, `applydebuff`, `refreshbuff`, `refreshdebuff`
- `extraAbility` (`Object`) — on `applybuff`, `removebuff`, `applydebuff` (the triggering ability)
- `targetResources` (`Object`) — same shape as `sourceResources`; present on nearly all buff events

### `DeathEvent` missing `killScore`

JSON `death` events always include `killScore` (`Number`), which is not modeled in `DeathEvent`.

### Event types in C# that do not appear in this log

These classes exist in `Events/` but were not observed in `raw-report.json`. They may be valid Fellowship log types that simply did not occur in this fight, or they may be WoWAnalyzer-era remnants that Fellowship does not emit:

`FreeCastEvent`, `LeechEvent`, `FilterCooldownInfoEvent`, `DispelEvent`, `DrainEvent`,
`ExtraAttacksEvent`, `ResurrectEvent`, `SummonEvent`, `FilterBuffInfoEvent`, `HealthEvent`,
`MaxChargesChangedEvent`, `UpdateSpellUsableEvent`, `ChangeStatsEvent`, `PhaseEvent`,
`AutoAttackCooldownEvent`, `SpendResourceEvent`

---

## Response Guidance

- When the user asks what properties a given event type has, run the tool and return the matching section.
- When the user suspects a deserialization bug, run the tool and apply the Mapping Procedure above, highlighting any High-severity flags.
- When the user is adding a new property to a C# event class, check the tool output to confirm the JSON property name and frequency before writing the property.
- When the tool shows a property that is `Object`-type with children, the children are candidates for a nested record — but check whether the parent object itself changes type (e.g., `Number` sometimes) before modeling it as a record.
