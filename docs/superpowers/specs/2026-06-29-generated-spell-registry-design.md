# Spell Database: generated registry + curation studio

> Status: draft for review. Evolves
> [2026-06-28-ability-metadata-from-export-design.md](2026-06-28-ability-metadata-from-export-design.md).

## Context

Each hero's spell registry and ability scalars are hand-typed across
`Spells.cs` and `Modules/Abilities.cs`, and drift from the game. The upstream
data we could draw on is spread across sources we do not control — the
[AngryDK/fs_tc_uploads](https://github.com/AngryDK/fs_tc_uploads) export
(`spell_data.json` global ability/effect identity, `gear_data.json` weapon
scalars, `hero_data.json` hero kits/scalars/costs) and the Fellowship Logs
`abilities.json` (icons) — and none is usable directly: identity, scalars,
icons, and costs each live in a different file, names are often `null`, and the
shapes differ per source.

We reconcile these once, offline, into a single normalized database that
FellowshipAnalyzer owns, make that database the source for every spell, and give
developers a local app to browse it, see what is missing, and curate the gaps.

## Goal

A committed `spelldb.json` is the canonical, human-readable spell database. A
compile-time generator reads it and emits full `Spell` definitions into a
registry per scope — one `Spells` class per hero, plus dedicated `shared` and
`weapon` registries — together with the cross-registry aggregation (`Spells.All`
keyed by guid and the per-scope `Guids` const tables). A shared merge engine
reconciles the upstream sources and the hand-authored overrides into
`spelldb.json`; a no-argument script rebuilds it; and a developer-only studio app
makes the whole database browsable and curatable. The data targets the live
season (**s3**).

## Architecture

```
spell_data.json (identity + kind)  ┐
gear_data.json  (weapon scalars)   │
hero_data.json  (kits/scalars/cost)├─► merge engine ─► spelldb.json (committed)
abilities.json  (icons)            │       ▲                  │
overrides.json  (hand deltas)      ┘       │                  ├─► compile-time generator
                                 studio app + rebuild script  │     → Spells.{scope} full defs
                                                              │     → per-scope Guids, central All
                                                              └─► browsable in the repo
```

- **Merge engine** — a plain .NET library (`FellowshipAnalyzer.SpellData` or
  similar) that reads the upstream sources plus `overrides.json` and produces the
  normalized model, with defined source precedence, per-field provenance, and gap
  detection. Reused by the rebuild script, the studio, and tests.
- **`spelldb.json`** — the committed normalized output. The merge engine's only
  output artifact; never hand-edited (hand changes go through `overrides.json`).
- **`overrides.json`** — the committed, human-editable deltas.
- **Rebuild script** — a no-argument tool that runs the merge engine and writes
  `spelldb.json`.
- **Compile-time generator** — reads `spelldb.json` (and the minimal hand-written
  registry) and emits the C# registries.
- **Studio** — a local developer app over the merge engine.

The merge engine runs offline (full .NET, `System.Text.Json`). The compile-time
generator stays `netstandard2.0` and reads only the final `spelldb.json` with its
self-contained JSON reader — no merging happens inside the generator.

## The normalized database (`spelldb.json`)

A two-level map: the top level is keyed by **scope** — a hero name, `shared`, or
`weapon` — and each scope is keyed by **member**, the C# property name emitted
into that scope's `Spells` class. The entry body carries the native id, identity,
the data scalars, and costs:

```json
{
  "rime": {
    "FreezingTorrent": {
      "id": 1027,
      "name": "Freezing Torrent",
      "icon": "T_Rime_ChanneledBeam.jpg",
      "cooldown": 15,
      "range": 30,
      "charges": 1,
      "channelDuration": 2.0,
      "channelTickInterval": 0.4,
      "costs": { "winterOrb": 0 }
    }
  },
  "weapon": {
    "VoidbringerTouch": {
      "id": 155,
      "name": "Voidbringer's Touch",
      "icon": "T_Weapon_VoidTouch.jpg",
      "cooldown": 90,
      "range": 30
    }
  }
}
```

- **scope key** — names the registry that receives the members: a hero (`rime`),
  `shared` for global spells, or `weapon` for weapon abilities and traits. The
  generator maps it to the registry case-insensitively.
- **member key** — the emitted C# property name (a valid identifier, PascalCase).
  Unique within its scope, but **not across scopes** — two heroes may each define
  `Roll`, as distinct spells with distinct ids. A within-scope collision is
  resolved by choosing a distinct key.
- `id` / `kind` — `id` is the native game id. `kind` ∈ `ability` | `effect` |
  `talent` | `weapon` (default `ability`), set by the FSL range and **independent
  of scope** — a `weapon`-scope spell is usually `ability` kind. Kind selects the
  emitted type and guid offset: `ability` → `Spell`, guid `id`; `effect` →
  `Effect`, guid `1_000_000 + id`; `talent` → guid `2_000_000 + id`; `weapon` →
  guid `3_000_000 + id`. The id + offset guid is unique across the whole keyspace
  even when native ids repeat across ranges.
- `costs` — keyed by resource (`spirit`, `winterOrb`, `anima`, `focus`); mapped to
  the matching typed `Spell` cost property.

`spelldb.json` is pure data. Provenance (which source supplied each field) is
recomputed by the merge engine when it runs and surfaced in the studio; it does
not live in the committed file.

## Overrides (`overrides.json`)

The single human-editable file mirrors `spelldb.json`'s shape — keyed by scope,
then member — and carries only the fields a contributor sets, plus an optional
`note`. The merge applies one rule: **override the member if it already exists,
add it if it does not.**

```json
{
  "rime": {
    "FreezingTorrent": { "channelTickInterval": 0.4, "note": "export omits tick" }
  },
  "weapon": {
    "VoidbringerTouch": { "id": 155 }
  },
  "shared": {
    "EpochBreakBuff": { "id": 2613, "kind": "effect" }
  }
}
```

Patching an existing member corrects a scalar, cost, name, icon, or kind the
sources get wrong. A spell absent from the hero kits — a weapon, a shared spell,
or an effect — is added by giving its scope, member key, and `id`; the merge
engine then **enriches it from the sources by id**, so an add is usually just an
id. An add must carry an `id` so the generator can form its guid — the merge
raises a build diagnostic for one without.

## The merge engine

### Sources

- **`spell_data.json`** — the global registry: an `Abilities` map and an
  `Effects` map (native id → FSLID, DevName, Name). The identity and kind source
  for every spell.
- **`gear_data.json`** — weapon abilities and traits with the scalars
  `hero_data.json` does not carry.
- **`hero_data.json`** — each hero's `Kit` (which abilities the hero owns → its
  scope), the `Constants` scalars, and resource costs.
- **`abilities.json`** — icons by FSL guid.
- **`dev_name_mappings.md`** — DevName→display-name and hero-class→hero hints.

### Pipeline

The engine selects each hero's named `Kit` abilities into that hero's scope, then
layers `overrides.json`, which both patches entries and **names** the non-Kit
spells to include — weapons, global spells, and effects — by scope, member, and
id. Every selected entry, auto or added, is then **enriched by id** across all
sources: name and kind from `spell_data.json`, scalars from
`hero_data.json`/`gear_data.json`, icon from `abilities.json`, costs from
`hero_data.json`. Hero `Constants` data is linked to its `Kit` ability by merging
every `Constants` entry sharing the ability's `DevName`. The engine records
per-field provenance and flags entries with missing `name` or `icon` for the
studio.

### Normalization rules

- **Kind & id** — the FSL range of the source id sets `kind` (0–999,999 ability,
  1,000,000–1,999,999 effect, 2,000,000–2,999,999 talent, 3,000,000+ weapon
  trait); the native `id` is that value minus the range offset.
- **Cooldown** — `Cooldown`, else `RechargeTime` (charge abilities).
- **Range** — `MaxRange / 100` (game centimetres → yards).
- **Charges** — `MaxCharges`, else `NumCharges`, else `1`.
- **CastDuration** — `CastingDuration`, else `CastTime`.
- **ChannelDuration** — `ChannelingDuration`.
- **ChannelTickInterval** — `ChannelingTickInterval`.
- **Icon** — `abilities.json` `Icon` for the FSL guid, when non-empty.
- **Costs** — the typed fields map to the matching cost (`SpiritCost` → spirit,
  `OrbCost` → winterOrb); the generic `Cost` resolves through the ability's
  `CostType` and the hero resource model.
- **Selection** — named hero `Kit` abilities auto-select to their hero scope;
  overrides name the remaining spells (weapons, shared, effects) into their scope.

Whole-number doubles emit as integral literals where the target field is integral
(`Charges`); seconds-valued fields stay `double`.

## The rebuild script

A no-argument tool (`src/FellowshipAnalyzer.Tools/rebuild-spelldb.cs`, run via the
run-tool skill) calls the merge engine and writes the committed `spelldb.json`. A
test re-runs the engine in memory and asserts the committed file is reproducible
from the sources and overrides, so `spelldb.json` never drifts from its inputs.

## The compile-time generator

The consolidated `SpellDatabase` generator replaces both the previous
`RegistryGenerator` and `SpellDatabaseGenerator` — net one fewer generator. It
reads `spelldb.json` (as `AdditionalFiles`) and the minimal hand-written
registry, and emits:

- Per scope, a `Spells.{Scope}.g.cs` partial — `Spells.Rime`, the central
  `Spells` for `shared`, `Spells.Weapon` for weapons — with one
  `public static {Type} {member} { get; } = new {Type} { Id = …, Name = …, Icon =
  …, Cooldown = …, … };` per member. The entry's `kind` selects `{Type}`:
  `ability` → `Spell`, `effect` → `Effect`, and `talent`/`weapon` → their derived
  types, added to Core when a `kind` first needs one.
- The central `Spells.All` frozen dictionary keyed by `Guid`, aggregating every
  scope's registry, typed at the lowest common ancestor of all entries — so
  identically-named members in different scopes coexist by guid.
- Each scope's nested `Guids` const table (one `const int` per member, the guid
  from `id` plus its `kind` range offset), which resolve that scope's `[On<>]`
  references cross-assembly. The central `Spells.Guids` covers only the `shared`
  and hand-authored Core members, since member names are not unique across scopes.
- No cross-scope forwarding: a spell referenced outside its owning hero is scoped
  `shared` or `weapon` and resolves through that registry directly; per-hero
  members are reached only through their own `Spells.{Hero}`.
- **FA0001** duplicate `member`-name detection *within a scope* — chiefly a
  generated member colliding with a hand-authored Core spell of the same name.
  Names need not be unique across scopes.

Diagnostics: a scope key that names no known registry; an added member without an
`id`; a `kind` whose `Spell` type has not yet been added to Core; a duplicate guid
across the keyspace.

## Minimal hand-authored set (Core)

A spell referenced by an `[On<nameof(Spells.X)>]` in **Core itself** must stay
hand-authored, because `ModuleGenerator` and the spell generator share the Core
compilation and cannot see each other's output — so neither the generated
property nor its generated `Guids` const is visible to `ModuleGenerator` there.
These Core-internal cross-hero spells (e.g. `Chronoshift`) remain hand-written in
the central `Spells.cs` in object-initializer form, and `ModuleGenerator` resolves
them through the syntax path. Hero-, `weapon`-, and `shared`-assembly `[On<>]`
references resolve through the cross-assembly metadata path (the generated `Guids`
const tables), so those kits and effects generate normally.

The generator includes the hand-written entries in `Spells.All` and the central
`Guids` by scanning the central registry's syntax, exactly as the previous
`RegistryGenerator` did.

## `ModuleGenerator` update

Unchanged except its same-assembly syntax-path guid reader switches from "first
constructor argument" to "the `Id =` assignment in the object initializer",
applying the FSL range offset implied by the declared type (`Effect`, `Talent`,
`Weapon`). This covers the hand-authored Core set.

## `Spell` restructure (Core)

`Spell` becomes a record with `init` properties and no positional constructor;
every entry uses the object-initializer form. It gains the data scalars
(`Cooldown`, `Range`, `Charges`, `CastDuration`, `ChannelDuration`,
`ChannelTickInterval`); its cost properties (`SpiritCost`, `WinterOrbCost`,
`AnimaCost`, `FocusCost`) become `init`-settable. `Guid` stays virtual; the base
`Spell` is the `ability` kind (`Guid => Id`), and the FSL-range subtypes layer the
offset onto `Guid` — `Effect` (`1_000_000 + Id`), with `Talent` (`2_000_000 + Id`)
and `Weapon` (`3_000_000 + Id`) added when a `kind` first needs them. Each is
written in object-initializer form, e.g. `new Effect { Id = …, … }`. `Spell` =
identity + physical facts.

## `SpellbookAbility` change (Core)

`SpellbookAbility` drops the data scalars (now on `Spell`) and reads them through
`PrimarySpell` (`GetCooldown`, `Charges`, cast/channel). It keeps behaviour-only
fields (`Category`, `Gcd`, `CooldownReducedByHaste`, `Enabled`, `IsDefensive`,
`Timeline*`, `CastableWhileCasting`, `AdditionalSpells`, `Name` override,
`CastEfficiency`). `Spellbook()` composes plainly:

```csharp
new() { PrimarySpell = Spells.FreezingTorrent, Category = SpellCategory.Rotational, Gcd = StandardGcd },
new() { PrimarySpell = Spells.ColdSnap, Category = SpellCategory.Rotational, Gcd = StandardGcd, CooldownReducedByHaste = true },
```

Talent-conditional values override the spell before wrapping, e.g.
`PrimarySpell = Spells.GrapplingArrow with { Cooldown = weightOfGravity ? 120 : 90 }`.
`SpellCategory` keeps `Uncategorized` as the guard sentinel; `Category` stays
`required`.

## The studio

A developer-only local Blazor app (not shipped in the client) that runs the merge
engine and presents the database for curation:

- A browsable table of every spell with its merged fields and per-field
  provenance, and the un-included spells from `spell_data.json`/`gear_data.json`
  available to add.
- Gap highlighting for entries missing `name`, `icon`, or other expected fields.
- A per-spell edit form that writes changes to `overrides.json` as deltas, then
  re-runs the merge so the effect is immediate in the studio.
- A rebuild action that writes `spelldb.json` (the same path as the script).
- Spell previews via the existing `SpellIcon`/`SpellLink` components.

## Testing

- **Merge-engine unit tests** — link-by-`DevName`, field selection, unit
  conversion, cost mapping, kind-from-range, enrichment-by-id (including a weapon
  enriched from `gear_data.json`), override application (patch existing, add new),
  and provenance, against the real s3 sources.
- **Reproducibility test** — the committed `spelldb.json` equals a fresh in-memory
  merge of the sources and overrides.
- **Generator test** — drives the generator over `spelldb.json` plus a sample
  compilation, asserting emitted members across the `rime`, `shared`, and `weapon`
  scopes, including icon, cost, and the type selected by `kind`.
- **Cross-check test** — every generated scalar and cost equals the current
  hand-coded oracle value, per hero, run before any hand value is removed.
- **Category guard test** — no enabled spellbook entry is
  `SpellCategory.Uncategorized`.

## Rollout

Delivered on one branch, staged so each step is provable:

1. Land the `Spell`/`SpellbookAbility` Core changes, the merge engine, the compile-time generator, the `ModuleGenerator` reader update, `spelldb.json`, `overrides.json`, and the rebuild script.
2. Fan out to all heroes, each cross-checked before its hand-typed values are removed.
3. Layer the studio on the working pipeline.
4. The whole solution builds and `dotnet test` passes; the old per-hero hand-typed registries and scalars are removed.
