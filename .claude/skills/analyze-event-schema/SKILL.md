---
name: analyze-event-schema
description: "Analyze the unique event types and all per-type properties in a Fellowship log JSON. Use when: inspecting raw-report.json to understand what event types exist, what properties each type can have, identifying properties missing from or mismatched with C# event classes in src/FellowshipAnalyzer.Core/Events/, or diagnosing deserialization bugs."
argument-hint: "Path to the log JSON file to analyze"
---

# Analyze Event Schema

Run the `event-schema` tool against a log JSON and compare the result against the mutable C# event classes in `src/FellowshipAnalyzer.Core/Events/`.

## Use When

- A user wants to know what event types appear in a log.
- A user wants all properties for a specific event type.
- A user suspects a C# event class is missing properties or has wrong property names.
- A user wants to find properties that would silently not deserialize from JSON.
- A user is designing or reviewing changes to event classes in `Events/`.

## Command

Run from the repository root:

```powershell
dotnet run src/FellowshipAnalyzer.Tools/event-schema.cs <log-json>
```

Example:

```powershell
dotnet run src/FellowshipAnalyzer.Tools/event-schema.cs raw-reports/RaMDvgzWXBCnF4QT-f16-s25.json
```

Raw report JSON lives in the gitignored `raw-reports/` folder at the repo root, named `{code}-f{fightId}-s{sourceId}.json`. Fetch a new one with `dotnet run src/FellowshipAnalyzer.Tools/fetch-report.cs <code> <fightId> <sourceId>`.

## Input Shapes Supported

- A raw report JSON with events at `data.reportData.report.events.data`.
- A top-level array of event objects.
- Any nested JSON document where one array contains event objects.

## Expected Output

1. Summary table: all event types sorted by count, with percent of total and unique property count.
2. Per-type table: every property seen, frequency, JSON value kind(s), and child property names for object properties.

## Current Event Model Facts

- Events are mutable classes, not `record struct` types.
- The base class is `Event`, with shared fields such as `Timestamp`, `Fight`, `SourceResources`, `TargetResources`, `Prepull`, `Fabricated`, and link/normalizer metadata.
- Concrete events inherit from `Event`, for example `CastEvent`, `DamageEvent`, `HealEvent`, `ApplyBuffEvent`, and `DeathEvent`.
- Capability interfaces such as `IAbilityEvent`, `IExtraAbilityEvent`, `IHasSourceEvent`, and `IHasTargetEvent` expose common properties for filters and analyzers.
- `ActorResources` models nested `sourceResources` and `targetResources`, including hit points, max hit points, absorb, position, facing, and `resources`.
- `Ability` maps nested JSON ability objects. `guid` maps to `Ability.FSLID`; `Ability.Id` is an ignored alias.
- `Event` is `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` with `UnknownDerivedTypeHandling.FailSerialization`. `JsonDerivedTypeGenerator` emits the `[JsonDerivedType]` list: every non-abstract subclass whose name ends in `Event` maps to that name minus the `Event` suffix, lowercased (`DeathEvent` to `death`). Subclasses marked `[Fabricated]` anywhere in their base chain are excluded; they are parser-created and never appear in log JSON.

## Mapping Procedure

For each event type in the tool output:

1. Find the matching C# class in `src/FellowshipAnalyzer.Core/Events/`.
2. Confirm how the event type maps to the class: the class name stripped of `Event` and lowercased, for example `DeathEvent` maps to `death` (there is no per-class discriminator attribute). If the C# class carries `[Fabricated]`, it is not in the discriminator map at all and no JSON event will ever produce it.
3. List inherited and declared C# properties.
4. Apply JSON naming rules:
   - `JsonNamingPolicy.CamelCase` maps `SourceId` to `sourceId`.
   - `PropertyNameCaseInsensitive = true` also allows JSON names such as `sourceID`.
   - `[JsonPropertyName("...")]` overrides naming.
   - `[JsonIgnore]` excludes a property from JSON.
5. Compare the expected JSON property names with the tool output.
6. For nested objects, compare against nested classes such as `Ability`, `ActorResources`, and `ClassResource` instead of expecting flattened top-level properties.

## Flags To Raise

| Situation | Severity | Notes |
| --- | --- | --- |
| JSON property is always present but has no matching C# property | High | Data is silently dropped. |
| JSON property is sometimes present but has no matching C# property | Medium | Optional data is silently dropped. |
| C# property is non-nullable but JSON property is not always present | High | It will be default-initialized when absent. |
| JSON property type conflicts with the C# property type | High | Example: JSON `Number` mapped to C# `bool`. |
| JSON property is nested but C# expects it top-level | High | Check `sourceResources`, `targetResources`, `ability`, and `extraAbility`. |
| C# property exists but JSON property never appears | Low | It may be synthetic or populated by a normalizer. Confirm before removing. |

## Current Checks To Remember

- `fight`, `sourceResources`, and `targetResources` are modeled on the base `Event` class.
- Advanced actor details such as health, absorb, position, facing, and resources belong under `ActorResources`.
- Ability master data may be hydrated by `AbilityMasterDataNormalizer` from report master data when events only contain IDs.
- `ResourceNormalizer` divides every resource snapshot's `amount`, `max` and `cost` by 100 before analyzers see them, so a raw-JSON value is 100x the analyzer-visible one. HitPoints and MaxHitPoints stay unscaled, and the `max: -100` no-maximum sentinel becomes -1.
- Synthetic fields such as `GlobalCooldown`, `Channel`, `LinkedEvents`, `Trigger`, and `Fabricated` may be set by normalizers or the parser rather than deserialized directly.

## Response Guidance

- When the user asks what properties a given event type has, run the tool and return the matching section.
- When the user suspects a deserialization bug, run the tool and apply the mapping procedure, highlighting High-severity flags first.
- When adding a new property to a C# event class, confirm the JSON property name, frequency, and value kind from the tool output before editing.
- When the tool shows an object property with children, check whether an existing nested model should be updated before adding flattened properties.
- Do not rely on stale remembered schema snapshots; rerun the tool against the relevant log.