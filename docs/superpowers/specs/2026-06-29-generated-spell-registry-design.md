# Generated Spell Registry from the Game-Data Export

> Status: draft for further refinement. Evolves
> [2026-06-28-ability-metadata-from-export-design.md](2026-06-28-ability-metadata-from-export-design.md).

## Context

Each hero's spell registry (`FellowshipAnalyzer.Core.Common.Spells.{Hero}.Spells`)
is hand-typed: one `Spell`/`Effect` per ability with identity (id, name, icon)
and resource costs. Gameplay scalars (cooldown, range, charges, cast/channel
timing) are hand-typed again in that hero's `Modules/Abilities.cs` `Spellbook()`.
Both drift from the game and miss data the analyzer could use.

The community data repo [AngryDK/fs_tc_uploads](https://github.com/AngryDK/fs_tc_uploads)
publishes a per-season `hero_data.json` with the gameplay scalars and costs, and
the Fellowship Logs API publishes `abilities.json` with icons keyed by ability
id. Together these cover a hero's full kit. We make the export the source for the
kit and generate each hero's `Spells` registry members directly from it at compile
time. The hand-authored layer keeps ownership of what the export cannot describe:
effects/buffs that are not kit abilities, shared/cross-hero spells, and a small
overrides file for fields the export gets wrong.

## Goal

A single Roslyn incremental generator emits each hero's kit `Spell` members
directly into that hero's `Spells` registry partial, sourced from the export
(identity, icon, cooldown, range, charges, cast/channel timing, and costs). The
generator also produces the registry aggregation it produces today (`Spells.All`,
the `Guids` const tables, the central forwarding properties). `Spell` carries the
data scalars; `SpellbookAbility` carries analysis behaviour and reads scalars
through its `PrimarySpell`. Each hero's `Spellbook()` composes plainly from
`Spells.X`. The data targets **S3**.

## Source data model

Three committed inputs, all registered as `AdditionalFiles` in
`FellowshipAnalyzer.Core`:

- **`hero_data.json`** (`external/fs_tc_uploads/s3/`) — keyed by hero display
  name. Each hero has:
  - `Kit` — `{ "<FSLID>": { Name, FSLID, DevName } }`. Identity for each ability
    the hero owns. `Name` is `null` for passives/internal handlers.
  - `Constants` — `{ "<entry>": { DevName, ...gameplay fields..., Cost,
    CostType, ... } }`. The rich per-ability data, linked back to the kit by
    `DevName`. The field an ability needs can live in a dev-named sibling entry
    that shares the same `DevName`; extraction merges every `Constants` entry
    that shares the kit ability's `DevName` (e.g. Rime `Cold Snap`'s cooldown and
    charges live in the `InstantSingleDamage` entry sharing
    `GA_Rime_InstantSingleDamage`).
  - A hero-level resource model naming each resource bar (`CostType` → resource
    `Name`, e.g. `SpiritPoints` → "Spirit", `Resources` → "Winter Orbs").
- **`abilities.json`** (repo root) — a flat `[{ Id, Name, Icon }]` array keyed by
  FSLID. `Name` is frequently `null`; `Icon` is the authoritative icon source
  (e.g. `1318 → "Bowguy_Multishot.jpg"`).
- **`spell-overrides.json`** (committed) — hand corrections applied during
  generation (see "Overrides file").

The scalars and costs we keep relate to **what happened in a log** (cooldown,
range, charges, cast/channel timing, resource cost). Simulation-only fields
(coefficients, spread, proc chance, PPM, damage scalers, talent magnitude
subtrees) are not read.

## Part A — `Spell` restructure (Core, repo-wide)

`Spell` becomes a record with `init`-settable properties and no positional
constructor. Every entry — generated and hand-written — uses the object-
initializer form:

```csharp
public static Spell FreezingTorrent { get; } = new Spell
{
    Id = 1027, Name = "Freezing Torrent", Icon = "T_Rime_ChanneledBeam.jpg",
    Cooldown = 15, Range = 30, ChannelDuration = 2.0, ChannelTickInterval = 0.4,
};
```

`Spell` gains the data scalars `Cooldown`, `Range`, `Charges`, `CastDuration`,
`ChannelDuration`, `ChannelTickInterval`. Its cost properties (`SpiritCost`,
`WinterOrbCost`, `AnimaCost`, `FocusCost`) become `init`-settable. `Guid` stays
virtual; `Effect` keeps `Guid => 1_000_000 + Id` and is written
`new Effect { Id = …, … }`. Conceptually `Spell` is **identity + physical
facts**.

## Part B — `SpellbookAbility` change (Core)

`SpellbookAbility` drops the data scalars (now on `Spell`) and keeps behaviour-
only fields (`Category`, `Gcd`, `CooldownReducedByHaste`, `Enabled`,
`IsDefensive`, `Timeline*`, `CastableWhileCasting`, `AdditionalSpells`, `Name`
override, `CastEfficiency`). Its accessors read through `PrimarySpell`:

- `GetCooldown(haste)` → `PrimarySpell.Cooldown` combined with
  `CooldownReducedByHaste` (`cd / (1 + haste)` when set, else `cd`; `0` when no
  cooldown).
- `Charges` → `PrimarySpell.Charges`; cast/channel read from `PrimarySpell`.

`Spellbook()` composes plainly, with behaviour set inline and scalars inherited
from the spell:

```csharp
public override IEnumerable<SpellbookAbility> Spellbook() =>
[
    new() { PrimarySpell = Spells.FreezingTorrent, Category = SpellCategory.Rotational, Gcd = StandardGcd },
    new() { PrimarySpell = Spells.ColdSnap, Category = SpellCategory.Rotational, Gcd = StandardGcd, CooldownReducedByHaste = true },
];
```

Talent-conditional values are expressed by overriding the spell before wrapping,
e.g. `PrimarySpell = Spells.GrapplingArrow with { Cooldown = weightOfGravity ? 120 : 90 }`.

`SpellCategory` keeps an `Uncategorized` member as the sentinel a guard test
flags. `Category` stays `required`.

## Part C — The evolved generator (Generators)

`RegistryGenerator` absorbs the kit-data role. The separate
`SpellDatabaseGenerator` and the `SpellDatabase`/`with` layer are removed, so the
net generator count drops by one.

### Emission

For each hero whose `Spells` namespace keys into `hero_data.json`, the generator
emits a `Spells.{Hero}.g.cs` partial with one `public static Spell {Name} { get; }
= new Spell { … }` per qualifying kit ability. A kit entry qualifies when its
`Name` is non-`null` and it is not excluded by the overrides file. Members land in
the per-hero `Spells` class so existing `Spells.X` references and
`[On<nameof(Spells.X)>]` resolution continue to bind.

### Preserved registry semantics

The generator continues to produce everything `RegistryGenerator` produces today,
now spanning both generated kit members and hand-written members:

- The central `Spells.All` frozen dictionary keyed by `Guid`, typed at the lowest
  common ancestor of all entries.
- The central `Spells.Guids` table and each registry's nested `Guids` table of
  `const int` guids. Kit guids come from the FSLID; hand-written guids come from
  the `Id =` literal, applying the `Effect → 1_000_000 + Id` rule.
- The central forwarding properties re-exposing each registry's members.
- **FA0001** duplicate property-name detection across the flattened central
  surface.

### Diagnostics

- A kit ability whose `DevName` matches no `Constants` entry.
- Conflicting values for the same field across merged `Constants` entries.
- An overrides entry that targets no ability present in the export.

### Literal reader

The guid reader switches from "first constructor argument" to "the `Id =`
assignment in the object initializer", applying the `Effect` encoding rule.

### Inputs

`hero_data.json`, `abilities.json`, and `spell-overrides.json` are consumed via
`AdditionalTextsProvider`. JSON is parsed by the self-contained reader already in
the generator (netstandard2.0, `Microsoft.CodeAnalysis` only).

## Part D — `ModuleGenerator` update (Generators)

`ModuleGenerator` is unchanged except its same-assembly syntax-path guid reader
also switches to the object-initializer `Id =` form, applying the `Effect`
encoding rule. This covers Core's own `[On<nameof(Spells.Chronoshift)>]` usages
(the central cross-hero spells). Hero-assembly `[On<>]` continues to resolve
through the metadata path (the nested `Guids` const table), which the generator
populates for kit members.

## Part E — Hand-authored surface (Core)

Each per-hero `Spells.cs` keeps the partial class declaration and only the members
the export cannot generate: effects/buffs that are not kit abilities (e.g.
`EventHorizonBuff`, `SkystridersGraceBuff`). These use the object-initializer
form.

Genuinely shared/global spells (e.g. `VoidbringerTouch`) live in the central
hand-written `Spells` so they are referenced cross-hero through one canonical
member; the overrides file excludes them from any hero kit that also lists them.
Cross-hero spells (`Chronoshift`, `EpochBreak`, `Kindling`, `EpochBreakBuff`)
stay in the central `Spells`, converted to the object-initializer form.

## Part F — Overrides file

`spell-overrides.json` is keyed by hero and ability (by `Name`, `DevName`, or
FSLID) and supports three operations applied during generation:

- **Field override** — supply or correct a scalar or cost the export lacks or
  gets wrong. The emitted member reflects the override, so the generated source is
  authoritative.
- **Rename** — provide the explicit C# identifier for a member, resolving a
  sanitized-name collision flagged by FA0001.
- **Exclude** — skip a kit entry handled elsewhere (e.g. a shared spell declared
  centrally, or a named entry that is not a real ability).

An override that matches no export ability raises a diagnostic so it does not rot
across seasons.

## Normalization rules

Applied per kit ability after merging its `Constants` entries by `DevName`:

- **Cooldown** — `Cooldown` (seconds); for charge abilities exposing
  `RechargeTime` instead (e.g. Ice Dash), `RechargeTime` is the cooldown.
- **Range** — `MaxRange / 100` (game centimetres → yards). Only `MaxRange` is a
  targeting range.
- **Charges** — `MaxCharges`, else `NumCharges`, else `1`.
- **CastDuration** — `CastingDuration`, else `CastTime`.
- **ChannelDuration** — `ChannelingDuration`.
- **ChannelTickInterval** — `ChannelingTickInterval`.
- **Icon** — `abilities.json` `Icon` for the kit FSLID, when non-empty.
- **Costs** — the flat typed fields map to the matching `Spell` cost property
  (`SpiritCost` → `SpiritCost`, `OrbCost` → `WinterOrbCost`); the generic `Cost`
  resolves through the ability's `CostType` and the hero resource model to the
  matching cost property. Costs the export does not expose are supplied via the
  overrides file.

Doubles read as whole numbers are emitted as integral literals where the target
field is integral (`Charges`); seconds-valued fields stay `double`.

## Testing

- **Normalization unit tests** — merge-by-`DevName`, field selection, unit
  conversion, cost mapping, and override application are pure methods tested
  against the real S3 `hero_data.json` for a hero's full kit.
- **Generator test** — drives the generator over the three inputs and a sample
  compilation, asserting emitted Rime and Elarion members including icon and
  cost.
- **Cross-check test** — every generated scalar and cost equals the current
  hand-coded oracle value, per hero. This runs before any hand value is removed.
- **Category guard test** — no enabled spellbook entry is
  `SpellCategory.Uncategorized`.

## Rollout

1. Land the `Spell`/`SpellbookAbility` Core changes, the evolved generator, the
   `ModuleGenerator` reader update, and the overrides file, with **Rime fully
   migrated** and its cross-check green.
2. Fan out to the other ten heroes, each cross-checked before its hand-typed
   values are removed.
3. The whole solution builds and `dotnet test` passes; the `SpellDatabase`
   layer and its usages are removed.
