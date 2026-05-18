# Plan — Remove `Module.Initialize()` and `Module.Complete()`

## Context

The lively-pelican redesign (`fellowship-analyzer-is-a-lively-pelican.md` §2) proposes deleting the `Initialize() / Complete()` lifecycle in favor of: `[On<TEvent>]` source-generated subscriptions, constructor-injected dependencies (with `Lazy<TOther>` for cycles), and a pure `ToReport()` projection. The new surface already exists alongside the old, but only one module (`BasicStComboAnalyzer`) has `ToReport()` and even that one still overrides `Complete()`. Removing the two lifecycle hooks requires migrating every remaining module — 8 production `Initialize()` overrides, 2 production `Complete()` overrides, plus test fixtures — and removing the calls in `CombatLogParser.Analyze`.

Two design pivots from the original §2 sketch:
- `Complete()` work cannot move to `[On<FightEndEvent>]` because `ToReport()` must be idempotent and re-invokable against arbitrary accumulator state (future time-filtered reports). Post-event computation folds **into** `ToReport()`.
- Mutable public properties that today are populated by `Complete()` (e.g. `BasicStCombo.EvaluatedWindows`) become computed `=> ToReport().X` delegates — UI keeps reading `Analyzer.EvaluatedWindows` and the typed-report-aware UI rework stays a separate effort.

`Module.Active` and `[ActiveWhen<>]` are out of scope **except** for `ChronoshiftAnalyzer`, where the gate read of `SelectedCombatant.HasGear(...)` blocks ctor-time setup. That one module migrates to `[ActiveWhen<HasChronoshiftGear>]` here so its `Initialize()` can be deleted.

## Critical files

| File | Role |
|---|---|
| `src/FellowshipAnalyzer.Core/Analysis/Module.cs` | Delete `Initialize()` / `Complete()` virtuals at the end |
| `src/FellowshipAnalyzer.Core/Analysis/EventSubscriber.cs` | `Initialize()` → `RegisterSubscriptions()` public method |
| `src/FellowshipAnalyzer.Generators/ModuleGenerator.cs` | Rename + scope-expand the current `EventSubscriptionGenerator` into a single `ModuleGenerator` that emits one generated partial per hand-written module file, covering event subscriptions, `Lazy<TModule>` accessors, and any future module-level concerns |
| `src/FellowshipAnalyzer.Core/Analysis/CombatLogParser.cs` | Replace Initialize loop with RegisterSubscriptions loop; delete Complete loop; add `IReadOnlyList<Event>` DI; sever `SelectedCombatant` setter |
| `src/FellowshipAnalyzer.Core/Events/FightStartEvent.cs` | New `[Fabricated]` event mirroring `FightEndEvent` |
| `src/FellowshipAnalyzer.Core/Analysis/Events.cs` | Add `FightStart` filter |
| `src/FellowshipAnalyzer.Core/Analysis/Normalizers/FightBookendNormalizer.cs` | New normalizer that prepends FightStart, appends FightEnd |
| `src/FellowshipAnalyzer.Core/Analysis/Combatants.cs` | Pre-scan + prepull seed → ctor; subscriptions → `[On<>]` |
| `src/FellowshipAnalyzer.Core/Analysis/StatTracker.cs` | Seed → `[On<FightStartEvent>]`; subscriptions → `[On<>]` |
| `src/FellowshipAnalyzer.Core/Analysis/Haste.cs` | Initial `ChangeHasteEvent` emit → `[On<FightStartEvent>]`; subscriptions → `[On<>]` |
| `src/FellowshipAnalyzer.Core/Analysis/SpellUsable.cs` | Sibling deps → `Lazy<>` ctor injection; subscriptions → `[On<>]` |
| `src/FellowshipAnalyzer.Core/Analysis/GlobalCooldown.cs` | Same pattern |
| `src/FellowshipAnalyzer.Core/Analysis/Abilities.cs` | `Spellbook()` → dict in ctor; no listeners |
| `src/FellowshipAnalyzer.Core/Analysis/ResourceTracker.cs` | Subscriptions → `[On<>]` |
| `src/FellowshipAnalyzer.Core/Analysis/ChronoshiftAnalyzer.cs` | `[ActiveWhen<HasChronoshiftGear>]`; subscriptions → `[On<>]`; `Complete()` body → `ToReport()` |
| `src/FellowshipAnalyzer.Core/Analysis/ChronoshiftReport.cs` | New record returned by `ChronoshiftAnalyzer.ToReport()` |
| `src/Heroes/FellowshipAnalyzer.Heroes.Rime/Modules/WinterOrbTracker.cs` | Delete the trivial `Initialize()` override |
| `src/Heroes/FellowshipAnalyzer.Heroes.Rime/Modules/BasicStComboAnalyzer.cs` | `Complete()` body → `ToReport()`; UI-facing fields become computed |
| `tests/FellowshipAnalyzer.Core.Tests/Analysis/*Tests.cs` | Fixture migrations (6 simple `Initialize` + 1 `Complete` + `HasteConfigWrapper`) |

## Existing facilities to reuse

- `[On<TEvent>]` attribute — `src/FellowshipAnalyzer.Core/Analysis/OnAttribute.cs` (supports `By`, `To`, `Spell`, `Spells[]`, `ExtraSpell`, `ExtraSpells[]`). Every filter shape used by today's `Initialize()` bodies is already expressible.
- `EventSubscriptionGenerator` — `src/FellowshipAnalyzer.Generators/EventSubscriptionGenerator.cs`. Emits `RegisterAttributeSubscriptions()` per partial class. This file is **renamed to `ModuleGenerator`** and extended in this work (see "Source-generator consolidation" below) — its existing `[On<>]` handling is preserved as-is.
- `CombatLogParserGenerator` `TryGetReportType` — `src/FellowshipAnalyzer.Generators/CombatLogParserGenerator.cs:229-248`. Already scans modules for `ToReport()` and emits typed result records. Adding `ToReport()` to `ChronoshiftAnalyzer` automatically extends `RimeAnalysisResult`.
- `FightEndEvent` — `src/FellowshipAnalyzer.Core/Events/FightEndEvent.cs`. Already exists `[Fabricated]`-marked; `Combatants.cs:68` already subscribes (dead code today — no normalizer fabricates it).
- `EventEmitter.FabricateEvent` — `src/FellowshipAnalyzer.Core/Analysis/EventEmitter.cs:91-98`. Used by Haste's initial `ChangeHasteEvent` emit (`Haste.cs:200-211`).
- `AnalysisRunServiceProvider` DI — `CombatLogParser.cs:243-345`. Already supports `Lazy<>` (lines 282-289) and module resolution. Add an `IReadOnlyList<Event>` branch (returning `owner.Events`) for `Combatants` ctor injection.
- `ModuleCycleAnalyzer` FA0013 — `src/FellowshipAnalyzer.Analyzers/ModuleCycleAnalyzer.cs`. Ignores `Lazy<>` edges, so the new ctor injections won't trigger cycle diagnostics.

## Source-generator consolidation

Rename `EventSubscriptionGenerator` → `ModuleGenerator` and have it own **all** code-generation concerns for partial classes deriving (transitively) from `EventSubscriber`. The invariant: **one hand-written module file produces exactly one generated partial file** (`{ModuleName}.g.cs`), containing one partial declaration that consolidates every generator-emitted member for that module. No second generator targets module partials; future module-level concerns (e.g. typed `ToReport()` plumbing, activation-gate caching, diagnostics) are added inside `ModuleGenerator` rather than as new generators.

For this work the generator emits two kinds of members per partial class:

1. **Event subscriptions** — `RegisterAttributeSubscriptions()` as today, unchanged.
2. **Lazy module accessors** — for every primary-ctor parameter of type `Lazy<TModule>`:

   ```csharp
   private TModule _camelCaseName => field ??= camelCaseName.Value;
   ```

   - Property name: parameter name with a leading underscore (`statTracker` → `_statTracker`); skip generation if the parameter already begins with `_`.
   - Property type: the `T` of `Lazy<T>`.
   - Body uses the C# 14 `field` keyword and `??=`, so the `Lazy<T>` is dereferenced exactly once per instance and subsequent accesses are a direct backing-field read.
   - Visibility: `private`. Generated declarations never widen the surface — call sites are always inside the same partial.

This replaces the manual `private T Foo => _foo.Value` pattern the original §2 sketch implied: handler bodies just write `_statTracker.CurrentHastePercentage` and the caching is invisible.

`CombatLogParserGenerator` stays separate — it operates on `[HeroAnalyzer]`-marked parser types, not on modules, and its outputs (parser ctor, typed module accessors, DI extensions) target a different consumer.

## Migration strategy per module

### Pure-subscription bodies (no setup)

`ResourceTracker.Initialize()` and `WinterOrbTracker.Initialize()` (`base.Initialize()` only): convert each `AddEventListener(Events.Foo.By(SELECTED_PLAYER)...)` call to `[On<FooEvent>(By = Actor.Player)]` on the handler. Delete the override. `[On<Event>]` is the no-filter equivalent of `Events.Any`.

### Setup that's safe in the constructor

`Abilities`: move the `Spellbook()` → dict build into the ctor body. No listeners. Verify hero `Spellbook()` overrides return literal/static collections that don't depend on derived instance state (`Spellbook` is virtual-from-ctor).

`Combatants`: inject `ParseContext` and `IReadOnlyList<Event>`. The ctor runs the same pre-scan loop and prepull seeding currently in `Combatants.cs:19-55`. Sever the `Owner.SelectedCombatant = ...` back-assignment; expose `Selected` on `Combatants` and change `CombatLogParser.SelectedCombatant` to `=> GetModule<Combatants>()?.Selected`. The 11 imperative listeners become `[On<>]` attributes (including `[On<FightEndEvent>]` for the prepull-buff closer that's wired but never fires today).

`SpellUsable`, `GlobalCooldown`, `ChronoshiftAnalyzer`: replace `Owner.GetModule<T>()` reads with `Lazy<T>` ctor injection. The new `LazyModuleAccessorGenerator` emits the cached `_camelCaseName` accessor for each `Lazy<TModule>` parameter, so handler bodies reference modules directly (`_spellUsable.IsAvailable(...)`) with no `.Value` noise. Field initializers replace the `_lastGcdEnd = 0` lines.

### Setup that must observe ordered module state

`StatTracker` needs `Combatants.Selected` to be populated. `Haste` needs `StatTracker.CurrentHastePercentage` to be populated AND must fabricate a `ChangeHasteEvent` that listeners see. Both happen after construction but before normal events dispatch — that's the gap a new `FightStartEvent` fills.

Introduce a `FightBookendNormalizer` (`[AddNormalizer<>]` on `CombatLogParser` ahead of the existing `[AddNormalizer<AbilityMasterDataNormalizer>]` at line 18) that:
- Prepends `FightStartEvent` at `parseContext.FightStartTime`.
- Appends `FightEndEvent` at `parseContext.FightEndTime`.

`StatTracker` becomes:

```csharp
public sealed partial class StatTracker(Lazy<Combatants> combatants) : Analyzer
{
    // generator emits: private Combatants _combatants => field ??= combatants.Value;

    [On<FightStartEvent>]
    private void OnFightStart(FightStartEvent _)
    {
        if (_combatants.Selected is not { } c) return;
        _pullStats = /* new PlayerStats { ... StatTracker.cs:36-45 verbatim ... } */;
        _currentStats = _pullStats.Clone();
    }
    // 8 [On<ApplyBuffEvent/RemoveBuffEvent/...>(To = Actor.Self)] handlers
}
```

`Haste` becomes:

```csharp
[After<StatTracker>]
public sealed partial class Haste(Lazy<StatTracker> statTracker) : Analyzer
{
    // generator emits: private StatTracker _statTracker => field ??= statTracker.Value;

    [On<FightStartEvent>]
    private void OnFightStart(FightStartEvent e)
    {
        Current = _statTracker.CurrentHastePercentage;
        TriggerChangeHaste(e, null, Current);
    }
    // 9 [On<>] buff/debuff handlers reading _statTracker.CurrentHastePercentage directly
}
```

`[After<StatTracker>]` (generator already supports per-handler ordering — see `CombatLogParserGenerator.cs:213-216`) plus `StatTracker`'s earlier index in `[AddModule<>]` order ensures `StatTracker.OnFightStart` runs first.

### `Complete()` bodies fold into `ToReport()`

`BasicStComboAnalyzer.Complete()` (`BasicStComboAnalyzer.cs:114-162`) becomes a private `ComputeReport()` invoked from `ToReport()`. The current mutable fields (`ScoreCard`, `EvaluatedWindows`, `SuccessfulWindows`, `PartialWindows`, `IgnoredAoeWindows`, `Findings`) become `=> ToReport().X` computed getters so `BasicStComboGuide.razor` keeps compiling unchanged. `Windows` already accumulates during dispatch and stays a mutable list (evaluation runs on a copy inside `ComputeReport`).

`ChronoshiftAnalyzer.Complete()` (`ChronoshiftAnalyzer.cs:143-159`) follows the same pattern. Add `ChronoshiftReport(IReadOnlyDictionary<int,int> TotalAppliedBySpell, IReadOnlyDictionary<int,int> TotalWastedBySpell)` and `public ChronoshiftReport ToReport()`. The existing public `TotalAppliedBySpell` / `TotalWastedBySpell` properties become `=> ToReport().TotalAppliedBySpell` etc. Adding `ToReport()` automatically extends the source-generated `RimeAnalysisResult` record.

`ChronoshiftAnalyzer`'s activation gate (`ChronoshiftAnalyzer.cs:52`: `Active = false` if no Chronoshift gear) becomes `[ActiveWhen<HasChronoshiftGear>]`. The predicate reads `ParseContext` — needs `SelectedCombatant` exposed on `ParseContext` (add it: ctor-resolvable because Combatants seeds in its own ctor before any other module's ctor that depends on it).

### Test fixtures

Six `Initialize()` overrides in `tests/FellowshipAnalyzer.Core.Tests/Analysis/` (`CombatLogParserTests.cs` lines 351/370/403/422, `SpellUsableTests.cs:223`, `HasteTests.cs:488`) are single- or few-handler subscriptions — direct `[On<>]` translation. `ProbeModule.Complete()` (`CombatLogParserTests.cs:380`) becomes part of a `ToReport()`-style assertion (or — since it's a test fixture — keep the field populated by an `[On<FightEndEvent>]` handler; the time-filter constraint is irrelevant to fixtures).

`HasteConfigWrapper` (`HasteTests.cs:478`) currently runs `configuration.Configure?.Invoke(haste)` inside `Initialize()`. Move the invocation into the wrapper's ctor — `Haste` is injectable and configuration is applied before any event dispatch.

### Final teardown

- Delete `Module.Initialize()` and `Module.Complete()` in `Module.cs:20-26`.
- Rename `EventSubscriber.Initialize()` (`EventSubscriber.cs:16-19`) to `public void RegisterSubscriptions()`. It cannot move into the ctor because `RegisterAttributeSubscriptions()` reads `Owner.EventEmitter`, and `Owner` is assigned by `AnalysisRunServiceProvider.GetOrCreate` (`CombatLogParser.cs:316`) AFTER `ActivatorUtilities.CreateInstance` (`CombatLogParser.cs:312`) returns.
- In `CombatLogParser.Analyze`, replace the `Initialize()` loop (`CombatLogParser.cs:173-176`) with:

  ```csharp
  foreach (var m in _activeModules.Values)
      if (m is EventSubscriber es) es.RegisterSubscriptions();
  ```

  Delete the `Complete()` loop (`CombatLogParser.cs:196-199`).
- Update `.claude/skills/create-analyzer/SKILL.md`, `.claude/skills/create-resource-tracker/SKILL.md`, and the architecture overview at `.claude/instructions/FellowshipAnalyzer-Architecture-Overview.md` to remove `Initialize`/`Complete` from the documented module surface.

## Sequencing — discrete PR-sized steps

Each step lands green (build + tests pass) on its own.

| # | Scope | Files |
|---|---|---|
| 1 | Add `FightStartEvent`, `FightBookendNormalizer`, `Events.FightStart`; register normalizer on `CombatLogParser`. No module migrations. | New event, new normalizer, `Events.cs`, `CombatLogParser.cs`. New `FightBookendNormalizerTests`. |
| 2 | DI plumbing: add `IReadOnlyList<Event>` branch to `AnalysisRunServiceProvider`; add `SelectedCombatant` to `ParseContext`; convert `CombatLogParser.SelectedCombatant` to a computed getter; remove its setter. Rename `EventSubscriptionGenerator` → `ModuleGenerator` and extend it with `Lazy<TModule>` accessor emission, keeping the single-generated-partial-per-source-file invariant, so PRs 4-6 can use `Lazy<TModule>` injection idiomatically. | `CombatLogParser.cs`, `ParseContext.cs`, `ModuleGenerator.cs` (renamed from `EventSubscriptionGenerator.cs`), generator snapshot tests. |
| 3 | Migrate leaf modules with no cross-module reads or `Complete()`: `Abilities`, `ResourceTracker`, `WinterOrbTracker`, plus the simple test fixtures. | 3 production files + ~3 test files. |
| 4 | Migrate `Combatants` (pre-scan + prepull seed → ctor; listeners → `[On<>]`). | `Combatants.cs`. Verify prepull-buff-close via `Events.FightEnd` now fires (was dead code). |
| 5 | Migrate `StatTracker`, `Haste`, `SpellUsable`, `GlobalCooldown`. Add `[After<StatTracker>]` to `Haste`. Update `HasteTests.HasteConfigWrapper` to ctor-time configuration. | 4 production + 1 test file. |
| 6 | Add `[ActiveWhen<HasChronoshiftGear>]` infrastructure; migrate `ChronoshiftAnalyzer` (listeners + `Complete()` → `ToReport()` + activation gate). | `ChronoshiftAnalyzer.cs`, new `ChronoshiftReport.cs`, new `HasChronoshiftGear.cs`. |
| 7 | Migrate `BasicStComboAnalyzer.Complete()` body into `ToReport()`; convert mutable result fields to computed getters. | `BasicStComboAnalyzer.cs`. |
| 8 | Final teardown: delete `Module.Initialize/Complete`, rename `EventSubscriber.Initialize → RegisterSubscriptions`, update `CombatLogParser.Analyze`, update skills/architecture docs. | `Module.cs`, `EventSubscriber.cs`, `CombatLogParser.cs`, 3 doc files. |

## Risks

- **Latent SpellUsable bug fix.** Today `Haste.Initialize()` emits the initial `ChangeHasteEvent` before `SpellUsable.Initialize()` has subscribed, so SpellUsable misses it. In the new world, all subscriptions are wired before any dispatch, so SpellUsable receives the initial change. This is a correctness improvement but may invalidate `SpellUsableTests` assertions pinned to current behavior. Audit during PR 5.
- **Source-generator partial dedup.** `ModuleGenerator` (renamed from `EventSubscriptionGenerator`) dedupes by earliest file path (current logic at `EventSubscriptionGenerator.cs:108-121`). Keep every module's `[On<>]` methods *and* `Lazy<T>` ctor parameters in a single file per partial — splitting them across files silently drops the second file's members. Since there is only one module generator, there is no cross-generator output collision to worry about.
- **`Lazy<T>` accessor name collision.** `ModuleGenerator` produces `_camelCase` properties for `Lazy<TModule>` parameters. If a module already defines a private field or property with the same name, the generated partial won't compile. Audit pre-existing `_statTracker` / `_combatants` / `_spellUsable` etc. fields during the PR that introduces each injection — most will be the very fields being replaced.
- **`Spellbook()` virtual-from-ctor.** `Abilities.Initialize()` calls `Spellbook()`; moving to ctor preserves the virtual call but earlier in object construction. Hero `Spellbook()` overrides must return data that doesn't depend on derived instance fields. Spot-check `RimeAbilities` etc. in PR 3.
- **`FightBookendNormalizer` ordering.** Must run before downstream normalizers that read fight boundaries. Register it first in `CombatLogParser`'s `[AddNormalizer<>]` chain.
- **`Lazy<>` cycle audit.** Every new `Lazy<TOther>` injection (Haste→StatTracker, SpellUsable→Abilities/DebugAnnotations, GlobalCooldown→Abilities/DebugAnnotations/Haste, StatTracker→Combatants, ChronoshiftAnalyzer→SpellUsable) is ignored by FA0013. Spot-check `ModuleCycleAnalyzer.cs` to confirm.
- **Sub-fight time-filtering future-proofing.** `Complete()`-derived state is now `=> ToReport().X` computed on each access. For UI bindings that read these properties many times per render, this means re-running the projection. Acceptable today (Windows is small; aggregation is O(n)); memoization can be added later if profiling shows it.
- **Combatants prepull buff seeding.** Today mutates `combatant.Buffs` directly without fabricating `ApplyBuffEvent`. Behavior preserved verbatim in the new ctor body.
- **`StatTracker.OnFightStart` null guard.** If a fight has no `CombatantInfoEvent` for the selected player, `Combatants.Selected` is null and `StatTracker._pullStats` stays at its default `new()`. Same behavior as today's `Initialize()` early return — verified safe.

## Verification

After each PR:
- `dotnet build FellowshipAnalyzer.slnx -nologo --verbosity minimal` — must succeed with no new warnings.
- `dotnet test FellowshipAnalyzer.slnx --no-build` — full suite green.
- Targeted runs for the module(s) changed in that PR, e.g. `dotnet test tests/FellowshipAnalyzer.Core.Tests/FellowshipAnalyzer.Core.Tests.csproj --no-build --filter "FullyQualifiedName~StatTracker|Haste|SpellUsable"`.

End-to-end after PR 7 and PR 8:
- Run the app via Aspire (`dotnet run --project src/FellowshipAnalyzer.AppHost/FellowshipAnalyzer.AppHost.csproj`).
- Open a Rime report end-to-end; verify the Guide tab (`BasicStComboGuide`, `WinterOrbGuide`) renders identical numbers to a master-branch baseline (`EvaluatedWindows`, `SuccessfulWindows`, `Windows`, score values).
- Verify Statistics tab (`WinterOrbStatistics`) renders unchanged.
- Open a report for a non-Rime hero (any shipped hero with `Combatants` / `StatTracker` / `Haste` consumers) and confirm the analyzer pipeline still produces non-null `SelectedCombatant`, valid haste percentages, and SpellUsable cooldown info.
- Confirm `Module.Initialize` / `Module.Complete` references no longer exist: `Grep` for `\boverride\s+(void\s+)?(Initialize|Complete)\s*\(` in `src/` — must return zero matches.
