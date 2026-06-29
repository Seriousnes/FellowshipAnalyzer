# Spell Database: generated registry + curation studio

> Status: draft for review. Evolves
> [2026-06-28-ability-metadata-from-export-design.md](2026-06-28-ability-metadata-from-export-design.md).

## Context

Each hero's spell registry and ability scalars are hand-typed across
`Spells.cs` and `Modules/Abilities.cs`, and drift from the game. The upstream
data we could draw on is spread across sources we do not control — the
[AngryDK/fs_tc_uploads](https://github.com/AngryDK/fs_tc_uploads) export
(`hero_data.json` scalars/costs) and the Fellowship Logs `abilities.json`
(names/icons) — and none of them is usable directly: fields are missing, names
are `null`, and the shapes differ per source.

We reconcile these once, offline, into a single normalized database that
FellowshipAnalyzer owns, make that database the source for every spell, and give
developers a local app to browse it, see what is missing, and curate the gaps.

## Goal

A committed `spelldb.json` is the canonical, human-readable spell database. A
compile-time generator reads it and emits each hero's full `Spell` definitions
directly into that hero's `Spells` registry (identity, icon, cooldown, range,
charges, cast/channel timing, costs), plus the registry aggregation
(`Spells.All`, the `Guids` const tables, the central forwarding properties). A
shared merge engine reconciles the upstream sources and the hand-authored
overrides into `spelldb.json`; a no-argument script rebuilds it; and a
developer-only studio app makes the whole database browsable and curatable. The
data targets **S3**.

## Architecture

```
fs_tc_uploads (hero_data.json) ┐
Fellowship Logs (abilities.json)├─► merge engine ─► spelldb.json (committed)
overrides.json (hand deltas)   ┘        ▲                  │
                                        │                  ├─► compile-time generator
                              studio app + rebuild script  │     → Spells.X full defs
                                                           │     → Guids, All, forwarding
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
  registry) and emits the C# registry.
- **Studio** — a local developer app over the merge engine.

The merge engine runs offline (full .NET, `System.Text.Json`). The compile-time
generator stays `netstandard2.0` and reads only the final `spelldb.json` with its
self-contained JSON reader — no merging happens inside the generator.

## The normalized database (`spelldb.json`)

A map keyed by combat-log guid (effects encoded as `1_000_000 + effectId`). Each
entry carries identity, scope, the emitted C# member name, and the data scalars
and costs:

```json
{
  "1027": {
    "member": "FreezingTorrent",
    "scope": "Rime",
    "name": "Freezing Torrent",
    "icon": "T_Rime_ChanneledBeam.jpg",
    "cooldown": 15,
    "range": 30,
    "charges": 1,
    "castDuration": null,
    "channelDuration": 2.0,
    "channelTickInterval": 0.4,
    "costs": { "winterOrb": 0 }
  }
}
```

- `member` — the C# property name emitted into the registry (PascalCase, sanitized
  from `name`, overridable to resolve collisions).
- `scope` — the hero whose `Spells` class receives the member, or `Shared` for the
  central `Spells` class.
- `costs` — keyed by resource (`spirit`, `winterOrb`, `anima`, `focus`); mapped to
  the matching typed `Spell` cost property.

`spelldb.json` is pure data. Provenance (which source supplied each field) is
recomputed by the merge engine when it runs and surfaced in the studio; it does
not live in the committed file.

## Overrides (`overrides.json`)

The single human-editable file, a per-guid delta carrying only changed or added
fields, with an optional `note`. It supports four operations:

```json
{
  "1027": { "channelTickInterval": 0.4, "note": "export omits tick" },
  "155": { "add": true, "member": "VoidbringerTouch", "scope": "Shared",
           "name": "Voidbringer's Touch", "icon": "T_Weapon_VoidTouch.jpg" },
  "1311": { "exclude": true, "note": "handled centrally" },
  "1310": { "member": "LunarlightMark" }
}
```

- **Field override** — supply or correct a scalar, cost, name, or icon the sources
  lack or get wrong.
- **Add** — declare an entry absent from the parsed sources (generated into the
  registry per its `scope`).
- **Exclude** — drop a parsed entry handled elsewhere.
- **Rename** — set the explicit `member` identifier, resolving a collision.

An override that matches no parsed ability and is not an `add` raises a build
diagnostic so it does not rot across seasons.

## The merge engine

For each ability the engine: links `hero_data.json` `Kit` entries to their
`Constants` data by merging every `Constants` entry sharing the kit ability's
`DevName`; pulls the icon from `abilities.json` by FSLID; applies the
normalization rules below; then applies `overrides.json`. It records per-field
provenance and flags entries with missing `name` or `icon` for the studio.

### Normalization rules

- **Cooldown** — `Cooldown`, else `RechargeTime` (charge abilities).
- **Range** — `MaxRange / 100` (game centimetres → yards).
- **Charges** — `MaxCharges`, else `NumCharges`, else `1`.
- **CastDuration** — `CastingDuration`, else `CastTime`.
- **ChannelDuration** — `ChannelingDuration`.
- **ChannelTickInterval** — `ChannelingTickInterval`.
- **Icon** — `abilities.json` `Icon` for the FSLID, when non-empty.
- **Costs** — the typed fields map to the matching cost (`SpiritCost` → spirit,
  `OrbCost` → winterOrb); the generic `Cost` resolves through the ability's
  `CostType` and the hero resource model.
- **Selection** — kit entries with a non-`null` `Name`, minus `exclude` overrides,
  plus `add` overrides.

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

- Per hero, a `Spells.{Hero}.g.cs` partial with one full
  `public static Spell {member} { get; } = new Spell { Id = …, Name = …, Icon = …,
  Cooldown = …, … };` per entry whose `scope` is that hero; `Shared`-scope entries
  go into the central `Spells`.
- The central `Spells.All` frozen dictionary keyed by `Guid`, typed at the lowest
  common ancestor of all entries.
- The central `Spells.Guids` table and each registry's nested `Guids` const table
  (guids from the entry's key, applying the `Effect → 1_000_000 + Id` rule).
- The central forwarding properties.
- **FA0001** duplicate `member`-name detection across the flattened central
  surface (resolved by a `rename` override).

Diagnostics: an entry whose `scope` names no known hero registry; an override that
targets nothing.

## Minimal hand-authored set (Core)

A spell referenced by an `[On<nameof(Spells.X)>]` in **Core itself** must stay
hand-authored, because `ModuleGenerator` and the spell generator share the Core
compilation and cannot see each other's output — so neither the generated
property nor its generated `Guids` const is visible to `ModuleGenerator` there.
These Core-internal cross-hero spells (e.g. `Chronoshift`) remain hand-written in
the central `Spells.cs` in object-initializer form, and `ModuleGenerator` resolves
them through the syntax path. Hero-assembly `[On<>]` references resolve through the
cross-assembly metadata path (the generated `Guids` const tables), so hero kits
and effects generate normally.

The generator includes the hand-written entries in `Spells.All`, the `Guids`
tables, and the forwarding by scanning the registry syntax, exactly as the
previous `RegistryGenerator` did.

## `ModuleGenerator` update

Unchanged except its same-assembly syntax-path guid reader switches from "first
constructor argument" to "the `Id =` assignment in the object initializer",
applying the `Effect` encoding rule. This covers the hand-authored Core set.

## `Spell` restructure (Core)

`Spell` becomes a record with `init` properties and no positional constructor;
every entry uses the object-initializer form. It gains the data scalars
(`Cooldown`, `Range`, `Charges`, `CastDuration`, `ChannelDuration`,
`ChannelTickInterval`); its cost properties (`SpiritCost`, `WinterOrbCost`,
`AnimaCost`, `FocusCost`) become `init`-settable. `Guid` stays virtual; `Effect`
keeps `Guid => 1_000_000 + Id` and is written `new Effect { Id = …, … }`. `Spell`
= identity + physical facts.

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
  provenance.
- Gap highlighting for entries missing `name`, `icon`, or other expected fields.
- A per-spell edit form that writes changes to `overrides.json` as deltas, then
  re-runs the merge so the effect is immediate in the studio.
- A rebuild action that writes `spelldb.json` (the same path as the script).
- Spell previews via the existing `SpellIcon`/`SpellLink` components.

## Testing

- **Merge-engine unit tests** — link-by-`DevName`, field selection, unit
  conversion, cost mapping, override application (field/add/exclude/rename), and
  provenance, against the real S3 sources.
- **Reproducibility test** — the committed `spelldb.json` equals a fresh in-memory
  merge of the sources and overrides.
- **Generator test** — drives the generator over `spelldb.json` plus a sample
  compilation, asserting emitted Rime and Elarion members including icon and cost.
- **Cross-check test** — every generated scalar and cost equals the current
  hand-coded oracle value, per hero, run before any hand value is removed.
- **Category guard test** — no enabled spellbook entry is
  `SpellCategory.Uncategorized`.

## Rollout

Delivered on one branch, staged so each step is provable:

1. Land the `Spell`/`SpellbookAbility` Core changes, the merge engine, the
   compile-time generator, the `ModuleGenerator` reader update, `spelldb.json`,
   `overrides.json`, and the rebuild script, with **Rime fully migrated** and its
   cross-check green.
2. Fan out to the other ten heroes, each cross-checked before its hand-typed
   values are removed.
3. Layer the studio on the working pipeline.
4. The whole solution builds and `dotnet test` passes; the old per-hero
   hand-typed registries and scalars are removed.
