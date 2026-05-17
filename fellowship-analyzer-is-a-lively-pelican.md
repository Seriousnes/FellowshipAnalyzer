# FellowshipAnalyzer — blue-sky redesign

## Context

FellowshipAnalyzer is a structural port of [WoWAnalyzer](https://github.com/WoWAnalyzer/WoWAnalyzer) (TypeScript/React, ~10 years old) to modern C# .NET 10 / Blazor WASM. The port is competent — source generators, polymorphic JSON, per-run scoped DI, CPM, .slnx — but several load-bearing patterns are faithful ports of OOP/React idioms that a modern C# WASM design wouldn't reach for first.

The codebase is still early (one mostly-complete hero, Rime; ten skeleton heroes). The cost of architectural change is at its lowest point. **This document is a vision, not a migration plan** — it describes what the project would look like if rebuilt today, given the constraints below.

**Constraints (locked by user):**

- **Pure WASM execution** — all parse/normalize/dispatch runs in the browser. No server offload, no Blazor Server hybrid.
- WASM bundle size is the dominant performance dimension. AOT compatibility is non-negotiable.
- Reflection over user types is fine at compile time (source gen); avoid at runtime.

---

## Verdict on what exists today

**Keep as-is — already modern:**

- Polymorphic event JSON via `[JsonPolymorphic]` + source-gen-discovered derived types (`src/FellowshipAnalyzer.Core/Events/Event.cs:5`). AOT-friendly and idiomatic.
- Per-run `AnalysisRunServiceProvider` scope (`src/FellowshipAnalyzer.Core/Analysis/CombatLogParser.cs:252`). Isolates parser state cleanly; better than what WoWAnalyzer can express.
- Roslyn diagnostic `FA0001` enforcing polymorphic-type registration. This is exactly the right place to put AOT constraints — compile-time, not "discover at runtime".
- File-based `dotnet` scripts in `src/FellowshipAnalyzer.Tools/` for spell/ability ingestion. Modern, not a port artifact.
- IndexedDB raw-bytes cache (`IndexedDbReportCacheService.cs:11`) that never deserializes on hit. Right call.
- `.slnx` + layered `Directory.Packages.props`. Nothing to change.
- Per-hero RCLs (with revisions, see below).

**Faithful ports worth rethinking:**

1. Mutable `Module` actors with `Owner.GetModule<T>()` service-locator lookup.
2. `Events.Cast.By(...).Spell(...)` fluent builder that compiles `Expression<Func<Event, bool>>` per subscription, per analysis run.
3. Event fabrication living inside modules (`ResourceTracker.cs:116`) rather than in the normalizer phase.
4. `int SELECTED_PLAYER = 1` magic-number actor constants (`Analyzer.cs`).
5. Manual eleven-line DI list in `src/FellowshipAnalyzer/HeroAnalysisServiceCollectionExtensions.cs`.
6. All eleven hero assemblies loaded into the initial WASM bundle regardless of which report is opened.
7. Results exposed as `IReadOnlyList<Module>` + tuple of `(Module, Type)` statistics — UI casts and reaches into mutable module state.

---

## Proposed design

### 1. Dispatch: source-generated subscription tables, no expression trees

**Today** — `Initialize()` calls `AddEventListener(Events.Cast.By(SELECTED_PLAYER).Spell(123), handler)`. Each call builds an `Expression<Func<Event, bool>>` and compiles it at subscription time (`src/FellowshipAnalyzer.Core/Analysis/EventFilter.cs:53`). The expression-tree machinery ships into the WASM bundle and pays compile cost per analysis run.

**Proposal** — attribute-declared handlers; the source generator emits the dispatch table:

```csharp
public sealed partial class BasicStComboAnalyzer : Analyzer
{
    [On<ApplyBuffEvent>(By = Actor.Self, Spell = SpellIds.WintersEmbrace)]
    private void OnEmbraceApplied(ApplyBuffEvent e) { ... }

    [On<DamageEvent>(By = Actor.Self)]
    private void OnSelfDamage(DamageEvent e) { ... }
}
```

The generator emits a `RegisterSubscriptions(EventEmitter, ParseContext)` partial method per analyzer with monomorphized delegates and inlined predicate checks. No `Expression.Compile()`, no LINQ tree allocation, no `Initialize()` ceremony.

**Why it pays off in WASM:**

- Removes `System.Linq.Expressions` from the trim graph for analyzer code paths.
- Predicate checks become normal IL the AOT compiler can inline.
- Subscription cost moves from per-run to zero — the table is static.
- Authoring is more declarative (closer to how a reader thinks about "subscribe to embrace applied").

**Cost** — the fluent builder's runtime composability is lost. A scan of the current heroes shows zero subscriptions built dynamically; the loss is theoretical.

---

### 2. Module shape: mutable accumulator + immutable Report record

**Today** — UI components reach into mutable module fields (`Statistics/WinterOrbStatistics.razor` reads tracker state directly). The module *is* its result.

**Proposal** — separate the accumulator from the result surface:

```csharp
public sealed partial class WinterOrbTracker : ResourceTracker
{
    // mutable accumulator stays private
    private int _totalGenerated, _wasted, _spent;

    // generator-friendly result projection
    public WinterOrbReport ToReport() => new(_totalGenerated, _wasted, _spent, ...);
}

public sealed record WinterOrbReport(int Generated, int Wasted, int Spent, ...);
```

`HeroAnalysisResult` carries the `*Report` records, not the module instances. Components bind to the report.

**Why:**

- Lets the *result* (not the raw modules) be cached, snapshotted, serialized, or sent over the wire later if the constraint loosens.
- Decouples render shape from accumulator shape — refactor a module without breaking the guide.
- Makes "go back to a previous report" a memcache hit instead of a re-parse (see §7).
- Reports are records → trivially diffable, trivially testable, trivially equality-comparable in xUnit.

---

### 3. Module dependencies: constructor injection, no service locator

**Today** — modules call `Owner.GetModule<T>()` to find siblings (`Module.cs:77`). `Owner` is set externally after DI resolution, so it cannot appear in the constructor. This is a port artifact of WoWAnalyzer's `this.owner.getModule(Foo)`.

**Proposal** — modules take their dependencies as ctor args:

```csharp
public sealed class BasicStComboAnalyzer(WinterOrbTracker orbs, Abilities abilities) : Analyzer
{
    // generator wires this up; ParseContext is injected separately via interface
}
```

Per-run `AnalysisRunServiceProvider` already scopes everything correctly. The source generator builds the DAG at compile time and:

- Emits the parser's instantiation order from a topological sort.
- Reports a diagnostic (FA0010) on cycles instead of throwing at startup.
- Eliminates the `Owner` / `Priority` mutable-after-construction dance.

`ParseContext` (player id, fight bounds, master data) becomes a value-type record passed via a single context interface, not a back-reference to the parser.

---

### 4. Fabrication-free dispatch: normalizers do all event mutation

**Today** — `ResourceTracker.cs:116` fabricates `ResourceChangeEvent` mid-dispatch when it spots a snapshot delta. This conflates two phases (mutation, observation) and means modules see a stream that's still being rewritten.

**Proposal** — every event in the dispatch loop is final. Resource-delta fabrication moves into a `ResourceDeltaNormalizer` that runs in the existing pre-dispatch normalizer pass. Modules become pure observers.

**Why:**

- Cleaner separation of "what happened" (events) vs "what we measured" (modules).
- Enables a debug mode that dumps the post-normalization stream for inspection — currently impossible without re-running everything.
- Removes the awkward "I'm a module but I'm also fabricating" responsibility from `ResourceTracker`.

---

### 5. Actors: typed singletons, not int constants

`SELECTED_PLAYER = 1`, `SELECTED_PLAYER_PET = 2` is a literal WoWAnalyzer transcription. Replace with:

```csharp
public abstract record Actor
{
    public static Actor Self { get; } = new SelfActor();
    public static Actor Pet  { get; } = new PetActor();
    public static Actor Boss { get; } = new BossActor();
    // …
}
```

Used as `[On<CastEvent>(By = Actor.Self, ...)]`. Better intellisense, no magic numbers, and `Actor` becomes a discriminator the generator can pattern-match against to emit specialized predicates.

---

### 6. Hero packaging: per-RCL **with** auto-discovery and lazy WASM loading

The recommendation here is mine. Two real options:

| | Collapse to one Heroes project | Keep per-RCL + lazy load |
|---|---|---|
| Hero authoring isolation | Folder-level | Project-level (cleaner) |
| Eliminates 11-line DI chain | Yes (single assembly scan) | Yes (manifest, generator) |
| Removes csproj boilerplate | Yes | No — but boilerplate is small and centrally managed |
| **WASM bundle: open Rime report** | All 11 heroes downloaded | **Only Rime downloaded** |
| Cross-hero accidental coupling | Easier (same project) | Architecturally prevented |
| First-paint on cold visit | Same large bundle | Tiny core bundle + ~1 hero on demand |

**Recommendation: keep per-RCL, add two things.**

1. **Compile-time hero manifest.** A `HeroDiscoveryGenerator` scans all referenced assemblies for `[HeroAnalyzer]` and emits a single `HeroManifest` record at compile time: `{ HeroName, AssemblyName, ParserType, GuideComponentType }`. The main app uses the manifest for the picker UI; `HeroAnalysisServiceCollectionExtensions.cs` is deleted.
2. **`BlazorWebAssemblyLazyLoad` per hero RCL.** Configure in the main app csproj. When a report for hero X is opened, the loader fetches `FellowshipAnalyzer.Heroes.X.wasm` on demand. The manifest stays small (records only; no parser types referenced statically).

The lazy-load story is the single biggest WASM win available under the pure-WASM constraint. Eleven RCLs goes from "boilerplate" to "load-isolation boundaries we already paid for".

---

### 7. Result cache: skip re-analysis on revisit

**Today** — navigating away and back to the same report re-deserializes raw bytes from IndexedDB and re-runs the full pipeline. Raw bytes are cached; *results* are not.

**Proposal** — a second IndexedDB store keyed by `(reportCode, fightId, playerId, heroAssemblyVersion)` holding the serialized `HeroAnalysisResult`. Source-generated `JsonSerializerContext` makes the round-trip AOT-safe (this is *why* §2 separates Report records from mutable modules — the records are serializable, the modules don't need to be).

`heroAssemblyVersion` keys the cache to the hero's build, so a hero analyzer update invalidates stale snapshots automatically. On miss, fall through to today's path.

**Why:** dominant UX pain in any analyzer site is "I tabbed back and waited five seconds for the same answer". Solve once, applies to every hero.

---

### 8. Typed results: generator-emitted accessor

**Today** — `HeroAnalysisResult.Modules` is `IReadOnlyList<Module>`; UI does `Modules.OfType<WinterOrbTracker>().FirstOrDefault()` or relies on a `[CascadingParameter] Module` cast.

**Proposal** — the parser generator emits a typed result record alongside the parser:

```csharp
public sealed record RimeAnalysisResult(
    WinterOrbReport WinterOrb,
    BasicStComboReport BasicStCombo,
    AbilitiesReport Abilities) : IHeroAnalysisResult;
```

Components bind to `Result.WinterOrb` directly. No casts, no `OfType<>`, no nullable lookups for modules that the parser declared. AOT-friendly because everything is statically typed.

---

### 9. Stronger Roslyn diagnostics replace documentation

Today: CLAUDE.md tells hero authors "do not accept `CombatLogParser` in module constructors". This is exactly the kind of rule a Roslyn analyzer should enforce. New rules worth adding:

- **FA0010** — module ctor must not accept `CombatLogParser` or `EventEmitter`.
- **FA0011** — `[On<TEvent>]` handler signature mismatch.
- **FA0012** — `[HeroAnalyzer]` class must be partial.
- **FA0013** — circular module dependency (from §3 DAG).
- **FA0014** — module declares `[On<TEvent>]` for an event type not in the assembly's known event graph.

The existing `FellowshipAnalyzer.Analyzers` project already has the scaffolding; this is incremental.

---

### 10. Naming: "Analyzer" overload

Two unrelated things are called "analyzer": gameplay modules (`Analyzer : EventSubscriber`) and Roslyn diagnostics (`FellowshipAnalyzer.Analyzers`). In a from-scratch world, gameplay modules become `Module` end-to-end (or `AnalysisModule` if disambiguation is needed). `FellowshipAnalyzer.Analyzers` becomes `FellowshipAnalyzer.Diagnostics`. Cosmetic, but the conflation comes up every time someone reads the architecture doc.

---

## Out of scope (intentionally)

- **API layer architecture.** Server-side concerns don't bind on the pure-WASM constraint; `BlobPersistentCache` is fine as it's evolving.
- **Aspire wiring.** Already idiomatic.
- **Tooling.** File-based scripts are the right shape.
- **Test framework.** xUnit + Shouldly + fixture loader is fine; nothing in the proposals breaks it.
- **Event class hierarchy.** Could be records, but the marker-interface + sealed-class approach is pragmatic, well-trimmed, and works with `[JsonPolymorphic]`. Not worth churning.

---

## How to validate the direction before committing

A blue-sky doc is cheap; the moves above range from "fix in an afternoon" to "rewrite the dispatch core". Before any of them ship, prove the WASM bundle / perf claim with a single-dimension spike:

1. Pick **§1 (source-gen subscriptions)** as the first prototype — it's the most architecturally invasive but also the most measurable.
2. Port `BasicStComboAnalyzer` to the attribute style on a branch.
3. Measure: WASM publish size delta (`bin/Release/.../wwwroot/_framework` total), first-analysis time on a fixed fixture, and trimmer warnings.
4. If the numbers favor it, §3 (constructor injection) and §8 (typed results) come almost for free since the same generator owns them.
5. §6 (lazy hero load) is independently measurable — try it standalone with two heroes and confirm the loader works under AOT before refactoring discovery.

Anything that doesn't justify itself on those numbers shouldn't ship just because the design doc said so.
