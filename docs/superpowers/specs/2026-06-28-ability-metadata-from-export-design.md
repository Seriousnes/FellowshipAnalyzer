# Accurate Ability Metadata from the Game-Data Export

## Context

Each hero's gameplay metadata — cooldown, range, charges, cast and channel
timings — is hand-typed in that hero's `Modules/Abilities.cs` `Spellbook()`.
Hand-typed values drift from the game and miss data the analyzer could use
(channel duration and per-tick interval are not modelled at all today).

The community data repo [AngryDK/fs_tc_uploads](https://github.com/AngryDK/fs_tc_uploads)
publishes a per-season `hero_data.json` containing this metadata. We make that
export the source of the data-derived scalars, while the hand-authored spellbook
keeps ownership of behaviour the export cannot describe.

## Goal

Source every hero's ability scalars (cooldown, range, charges, cast duration,
channel duration, channel tick interval) from `hero_data.json` via a Roslyn
incremental generator, and compose each hero's spellbook from the generated facts
using `with`. Simplify `SpellbookAbility` so the function-valued cooldown and
charge forms are replaced by data-friendly scalars plus a single haste flag. The
generator emits an `AbilityFacts` class per hero **in that hero's own `Spells`
namespace** (`FellowshipAnalyzer.Core.Common.Spells.{Hero}`), so the generated
kits never collide.

## Source data model

`hero_data.json` is keyed by hero display name. Each hero has:

- `Kit` — `{ "<FSLID>": { Name, FSLID, DevName } }`. The abilities the hero owns.
  Sparse: identity only.
- `Constants` — `{ "<entry name>": { DevName, ...gameplay fields..., Talent: {…} } }`.
  The rich per-ability data. Entries are keyed by display name or by an internal
  dev label, and are linked back to the kit through `DevName`.
- `Talents`, `Attributes` — not consumed by this slice.

The scalars we keep relate to **what happened in a log** (cooldown, range,
charges, cast/channel timing). Simulation-only fields (coefficients, spread,
proc chance, PPM, damage scalers, target-count thresholds, talent magnitude
subtrees) are not read.

The `Constants` link is not one-to-one with display names. The field an ability
needs can live in a dev-named sibling entry that shares the same `DevName`.
Example: live `Cold Snap` carries no `Cooldown`; its cooldown and charges live in
the `InstantSingleDamage` entry, and both share `DevName`
`GA_Rime_InstantSingleDamage`. Extraction links **by `DevName`**, merging every
`Constants` entry that shares the kit ability's `DevName`.

The export gives the **base** scalar and the **magnitude** of talent/buff
modifiers. It does not encode whether a baseline cooldown is reduced by haste,
nor the conditional "if this talent is picked, the cooldown becomes X". Those
remain hand-authored. The data is current-season; we target **S3**.

## Part A — `SpellbookAbility` schema simplification (Core, repo-wide)

`CooldownValue`, `ChargesValue`, and their `OneOf` machinery are removed in
favour of scalars. Every haste-reduced cooldown in the repo uses exactly
`base / (1 + haste)` (the only three function-cooldowns are Rime Cold Snap and
Elarion Highwind Arrow / Grappling Arrow), so the formula is centralised behind a
flag rather than repeated per ability.

`SpellbookAbility` changes:

- `CooldownValue? Cooldown` → `double? Cooldown` (base seconds).
- New `bool CooldownReducedByHaste` (default `false`).
- `ChargesValue Charges` → `int Charges` (default `1`).
- New `double? CastDuration` — cast time in seconds.
- New `double? ChannelDuration` — total channel time in seconds.
- New `double? ChannelTickInterval` — seconds between channel ticks.
- `GetCooldown(double haste)` →
  `CooldownReducedByHaste ? Cooldown / (1 + haste) : (Cooldown ?? 0)`.
  This reproduces the current lambdas exactly with the same `haste` argument.
- `GetCharges()` → returns `Charges`. The `Combatant` parameter is dropped from
  both accessors; talent-conditional charges are resolved when the spellbook is
  built (below).

`SpellCategory` gains an `Uncategorized` member so generated facts can be emitted
with a sentinel that a guard test flags if a `with` composition forgets to set a
real category. `Category` stays `required`, so hand-authored `new()` entries
still get a compile error when omitted.

`Core/Analysis/Abilities.cs` updates its two call sites
(`GetExpectedCooldown`, `GetMaxCharges`) to the new accessor signatures.

The three function-cooldown call sites are rewritten:

- Rime `ColdSnap`: `Cooldown = 12, CooldownReducedByHaste = true, Charges = 2`.
- Elarion `HighwindArrow`: `Cooldown = 15, CooldownReducedByHaste = true, Charges = 3`.
- Elarion `GrapplingArrow`: expressed in `Spellbook()` as a combatant-aware
  branch (next paragraph).

### Combatant-aware `Spellbook()`

`Spellbook()` is an instance method on the `Abilities` module, which exposes
`Owner.SelectedCombatant`. The parser builds `SelectedCombatant` from the
player's `CombatantInfoEvent` **before any module is constructed**
(`ParseContext.SelectedCombatant`; the same guarantee Elarion's
`[ActiveWhen<HasEmpoweredMultishot>]` predicate already relies on). The spellbook
is built lazily and cached on first access, which can only happen after module
construction — so the combatant, and therefore the player's talents, are known
when the cache is built and stay fixed for the analysis.

Talent-conditional abilities therefore read the combatant directly and branch
with `with`, replacing the `Func<Combatant, …>` forms. Grappling Arrow:

```csharp
public override IEnumerable<SpellbookAbility> Spellbook()
{
    var weightOfGravity = Owner.SelectedCombatant.HasTalent(Talents.TheWeightOfGravity.Id);
    return
    [
        // …other abilities…
        AbilityFacts.GrapplingArrow with
        {
            Category = SpellCategory.Utility,
            Gcd = null,
            Cooldown = weightOfGravity ? 120 : 90,
            CooldownReducedByHaste = weightOfGravity,
            Charges = weightOfGravity ? 2 : 1,
        },
    ];
}
```

## Part B — Ability-facts source generator (Core, all heroes)

A new Roslyn `IIncrementalGenerator` in `FellowshipAnalyzer.Generators` emits an
`AbilityFacts` class per hero, each in that hero's own
`FellowshipAnalyzer.Core.Common.Spells.{Hero}` namespace so the generated kits
never collide. It runs in the **Core** compilation, where the `Spells` registry
classes exist as source. A hero is emitted when its `Spells` namespace has a
matching key in `hero_data.json`; heroes without a registry yet (e.g. Gunde) are
skipped.

Inputs:

- `hero_data.json` (S3) vendored into Core and registered as `AdditionalFiles`.
- The hero `Spells` registry syntax, read the same way `RegistryGenerator`
  already reads `new(<id>, …)` initializers, to map an FSLID to its
  `Spells.<Property>`.

For each kit ability the generator emits one `SpellbookAbility`:

```csharp
namespace FellowshipAnalyzer.Core.Common.Spells.Rime;

public static class AbilityFacts
{
    public static SpellbookAbility FreezingTorrent { get; } = new()
    {
        PrimarySpell = Spells.FreezingTorrent,
        Category = SpellCategory.Uncategorized,
        Cooldown = 15,
        Range = 30,
        ChannelDuration = 2.0,
        ChannelTickInterval = 0.4,
    };
    // …one property per kit ability…
}
```

Only data-derived scalars and `PrimarySpell` are set. Behaviour fields
(`Category`, `Gcd`, `AdditionalSpells`, `CooldownReducedByHaste`, timeline hints,
`Enabled`, talent branches) are left to the hand-authored spellbook.

JSON is parsed by a small purpose-built reader inside the generator. The
Generators project is `netstandard2.0` with only `Microsoft.CodeAnalysis`
referenced; a self-contained reader covers the nested-object / string / number
subset we need without bundling `System.Text.Json` and its transitive
dependencies into the analyzer.

The generator reports diagnostics for: a kit ability whose `DevName` matches no
`Constants` entry; conflicting values for the same field across merged entries;
and a kit FSLID with no matching `Spells` property.

## Part C — Rime spellbook composition (`with`)

`Rime/Modules/Abilities.cs` `Spellbook()` is rewritten to compose from
`RimeAbilityFacts`, applying behaviour via `with`:

```csharp
AbilityFacts.FreezingTorrent with { Category = SpellCategory.Rotational, Gcd = StandardGcd },
AbilityFacts.ColdSnap        with { Category = SpellCategory.Rotational, Gcd = StandardGcd, CooldownReducedByHaste = true },
```

Hidden, non-kit, or otherwise data-less abilities (e.g. `VoidbringerTouch`,
`Kindling`) stay hand-authored with `new()`.

## Normalization rules

Applied per kit ability after `DevName`-merging its `Constants` entries:

- **Cooldown** — `Cooldown` (seconds). For charge abilities exposing
  `RechargeTime` instead (e.g. Ice Dash), `RechargeTime` is the cooldown.
- **Range** — `MaxRange / 100` (game centimetres → yards). Only `MaxRange` is a
  targeting range; `ConeRange`, `AoeRadius`, `MaxSpreadRange`, `HealingRange`
  are not read.
- **Charges** — `MaxCharges`, else `NumCharges`, else `1`.
- **CastDuration** — `CastingDuration`, else `CastTime`.
- **ChannelDuration** — `ChannelingDuration`.
- **ChannelTickInterval** — `ChannelingTickInterval`.

Doubles read as whole numbers (`15.0`) are emitted as `int`-friendly literals
where the target field is integral (`Charges`); seconds-valued fields stay
`double`.

## Validation

The generated Rime facts are cross-checked against the current hand-coded Rime
values before any hand-coded number is deleted. By inspection the export already
agrees with every Rime entry — Brain Freeze 20, Bursting Ice 10, Cold Snap base
12 / 2 charges (via the `InstantSingleDamage` `DevName` merge), Freezing Torrent
15 plus channel 2.0 / tick 0.4, Ice Dash 25 / 2 charges (from `RechargeTime` and
`NumCharges`), Flight of the Navir 60, Frost Ward 30, Ice Blitz 120, Winter's
Blessing 60. The cross-check formalises this as a test.

Tests:

- **Normalization unit tests** — the merge + field-selection + unit-conversion
  logic is a pure method tested directly against the real S3 `hero_data.json`
  for Rime's full kit.
- **Generator test** — drives the generator over a sample compilation plus the
  vendored JSON and asserts the emitted `RimeAbilityFacts` values.
- **Cross-check test** — asserts each generated Rime fact's scalar equals the
  expected oracle value.
- **Category guard test** — asserts no enabled spellbook entry is
  `SpellCategory.Uncategorized`.

## Build and rollout

The whole solution builds with the new generator active in Core, and
`dotnet test` passes including the hero tests. The generator and normalization are
hero-agnostic; every hero's `Abilities.cs` composes from its generated
`AbilityFacts`, and each hero's generated scalars are cross-checked against its
current hand-coded values before those values are removed.
