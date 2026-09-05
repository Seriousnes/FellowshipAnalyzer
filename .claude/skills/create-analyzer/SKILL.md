---
name: create-analyzer
description: "Create a pure C# analyzer module that subscribes to combat log events, tracks state, and computes metrics. Use when: adding a new talent analyzer, ability analyzer, feature analyzer, or any event-driven analysis module. NOT for ResourceTrackers, guide components, or statistics components."
---

# Create Analyzer

An analyzer is a pure C# module in the `Modules/` folder. It subscribes to combat log events, tracks state, and exposes computed metrics as public properties that guide and statistics components read directly. It has no Blazor dependency and holds typed data only: counts, rates, timestamps, enums, and typed entry records. Prose, severity wording, and `PerformanceTier` ratings belong in the Razor components that consume it.

Guide rendering belongs in the `create-guide` skill. Statistics rendering belongs in the `create-statistics` skill. Resource tracking belongs in the `create-resource-tracker` skill.

Reference implementation: `src/Heroes/FellowshipAnalyzer.Heroes.Tariq/Modules/FuryEconomyAnalyzer.cs` with `Guides/FuryEconomyGuide.razor`.

## Non-negotiable rules

Check these before writing a line. Each one has been enforced by the owner deleting work that broke it.

- **One analyzer per ability or talent.** Search `Modules/` for an existing analyzer covering the ability first. If one exists, the new measurement goes **into** it. Never add a second module measuring the same ability, including a statistics-only one.
- **All damage amplification and reduction maths goes through `Core/Utility/CombatMath.cs`.** Call `CalculateEffectiveDamage(e, increase)` or `CalculateEffectiveDamageReduction(e, reduction)`. Do not write a hero-local maths helper and do not inline `raw - raw / (1 + increase)`. If `CombatMath` cannot express what you need, stop and ask.
- **Never re-derive what another module owns.** Take it as `[Dependency<T>]` and read it, for instance `ResourceTracker` exposes windowed accessors (`SpentBetween`, `TimeByHolderBetween`, `BandsBetween`) for per-pull questions.
- **`[On<Event>]` is a last resort.** An unfiltered subscription to every event is almost always the wrong tool; filter on a concrete event type. Stop and ask if you think you need `[On<Event>]` - there is almost always a better way.
- **A conditional analyzer uses `[ActiveWhen<TPredicate>]`**, never an `if` at the top of a handler. The predicate type implements `IModuleActivePredicate`. This works on pull analyzers and handles inverse gates (active when a talent is *absent*) that `[RequiresTalent]` cannot express.
- **Name it for what it assesses.** `{Ability}Analyzer`, not `{Ability}WindowAnalyzer` or `{Ability}AssignmentAnalyzer`. A qualifier the domain does not need misstates the analyzer's scope.

`[ForPull]`, `[Dependency<T>]`, `[ActiveWhen<T>]` and `[RequiresTalent]` are all **class-level** attributes, declared above the class alongside each other, never inside the body.

## Two lifetimes

Every event subscriber derives from `Analyzer` and registers with `[AddAnalyzer<T>]` (FA0019). `[ForPull]` declared directly on the class, and nothing else, chooses between the two lifetimes:

- **Pull-lifetime analyzer** (the default for gameplay analysis): carries `[ForPull(PullKind…, Boss = …)]`. A fresh instance is constructed for every matching pull, so its state is per-pull by construction. Valid only on a concrete class - an abstract base declares the shape and each concrete subclass its own filter (FA0021).
- **Dungeon-lifetime analyzer**: declares no `[ForPull]`, observes the whole dungeon, and is constructed once per run; its `Pull` property is never assigned. Use for cross-pull state, statistics sources, and infrastructure. `[AddModule<T>]` and its synonym `[AddState<T>]` register a type that subscribes to nothing at all, such as `Abilities` or `Auras`.

## Procedure

### 1. Create The Analyzer Class

Place at `src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/Modules/{Name}Analyzer.cs`.

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.{Hero};
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.{Hero}.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class {Name}Analyzer : Analyzer
{
    private readonly List<SomeWindow> _windows = [];

    public IReadOnlyList<SomeWindow> Windows => _windows;
    public int GoodCount => _windows.Count(window => window.IsGood);
    public double GoodShare => _windows.Count == 0 ? 0 : (double)GoodCount / _windows.Count;

    [On<ApplyBuffEvent>(By = Actor.Player, Spell = nameof(Spells.SomeBuff))]
    private void OnBuffApply(ApplyBuffEvent applyBuffEvent)
    {
        _windows.Add(new SomeWindow(applyBuffEvent.Timestamp));
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        _windows.LastOrDefault(window => window.EndTimestamp is null)?.Casts.Add(castEvent);
    }
}
```

Mark the class `partial` so the `ModuleGenerator` can emit its event-subscription override and any lazy-module accessors. Use simple helper records/classes in the same file unless they are large or shared.

An analyzer finalizes nothing at pull end - it exposes its metrics as **get-style** properties (or methods) evaluated when a guide reads them. An analyzer's own state is frozen once its pull ends (its listeners are cleared), so a getter always sees the final state. Aggregates over completed data are plain get-style properties. When a metric depends on an interval still open at pull end (a buff still up, a resource still capped, a DoT with no logged remove), read the boundary from the analyzer's `Pull` property inside the getter - `Pull.EndTime` is the close time. For a heavy multi-output computation, run it once behind a private nullable field (`_result ??= Compute()`) so repeated reads do not recompute. Subscribe to `[On<PullEndEvent>]` only to react to the pull ending as an event: the one thing get-style cannot do is snapshot another (dungeon-lifetime) module's live state at the instant this pull closes.

### 2. Register On The CombatLogParser

Analyzers of either lifetime use `[AddAnalyzer<T>]`; a non-subscriber uses `[AddModule<T>]` (declaration order is module priority).

```csharp
[HeroAnalyzer(HeroName.{Hero})]
[AddAnalyzer<{Name}Analyzer>]
[AddModule<Modules.Abilities>]
public sealed partial class {Hero}CombatLogParser : CombatLogParser
```

For each `[ForPull]` surface type the source generator produces three read paths plus DI wiring:

- `parser.{Name}Analyzers` - the cross-pull stream, `IReadOnlyList<PullAnalyzer<{Name}Analyzer>>` of `(Pull, Analyzer)` pairs in pull order.
- `parser.For(pull).{Name}Analyzer` and the `pull.{Name}Analyzer` extension - the retained instance for one pull (nullable).

The surface type is the analyzer's `IAnalyzerSurface` marker interface if it implements one, otherwise the topmost ancestor deriving directly from `Analyzer`. Three composition patterns cover pull-shape variation:

- **One analyzer, every shape:** a single flat analyzer with a `[ForPull]` filter that matches all shapes it runs on. It is its own surface.
- **Different questions per shape:** independent analyzers answering different questions for different shapes (e.g. `SearingBlazeUptimeAnalyzer` on boss pulls and `SearingBlazeSpreadAnalyzer` on trash pulls in Ardeos). Give both a shared **surface marker interface** - `public interface ISearingBlazeAnalyzer : IAnalyzerSurface;`, then `: Analyzer, ISearingBlazeAnalyzer` on each - so they share no base class or behaviour but expose one `parser.SearingBlazeAnalyzers` stream and `pull.SearingBlazeAnalyzer` accessor (typed as the interface). Each keeps its own disjoint `[ForPull]`; the guide switches on the concrete type per row (see create-guide). An analyzer may implement at most one surface interface (FA0017).
- **One question, shape-specific scoring:** when the shapes share subscriptions and accumulated state and differ only in finalization, put the shared machinery on an abstract base and give each concrete subclass a disjoint `[ForPull]` filter plus its own scoring strategy and output subtype (see `WintersEmbraceAnalyzer` with `SingleTargetEmbraceAnalyzer` / `AoeEmbraceAnalyzer` in Rime); the base is the single surface all read paths use.

FA0016 enforces disjoint `[ForPull]` filters across analyzers sharing a surface (a marker interface or a base class). Inheritance (the third pattern) is earned when the base owns real machinery and each subclass owns its strategy and outputs; when the analyses share nothing, prefer the marker interface. A base that implements every strategy itself while subclasses one-line-dispatch into it wants a flat pattern instead.

For a parse-lifetime registration - whichever attribute declared it - the generator emits a typed nullable parser property (`{Name}Analyzer` becomes `{Name}` - the `Analyzer` suffix is stripped).

### 3. Optionally Set StatisticsComponentType

If this module has a statistics component, expose it (a dynamic expression is fine - it is read after analysis completes):

```csharp
public override Type? StatisticsComponentType => Procs > 0 ? typeof({Name}Statistics) : null;
```

Statistics are collected only from parse-lifetime registrations: the parser builds the statistics list from its active-module set, which is every registered type with no `[ForPull]`, so a `[ForPull]` analyzer never contributes a card. Also override `StatisticCategory` and `StatisticOrder` to place it.

## Event Subscription API

Declare each handler with a `[On<TEvent>]` attribute on a private (or internal) instance method. The `ModuleGenerator` translates the attributes into a `RegisterAttributeSubscriptions` override with inlined predicates.

The handler takes the dispatched event as a single parameter, typed as `TEvent`, one of its base classes or interfaces, or a `OneOf<…>` carrying a slot for it. Declare no parameter when the handler reads nothing off the event - the attribute's own filters already select which events reach it. A second parameter, or a parameter the event is not assignable to, is FA0011.

```csharp
[On<CastEvent>(By = Actor.Player)]
private void OnCast(CastEvent e) { … }

[On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SomeBuff))]
private void OnBuffApply(ApplyBuffEvent e) { … }

[On<DamageEvent>(By = Actor.Player, Spells = new[] { nameof(Spells.A), nameof(Spells.B) })]
private void OnDamage(DamageEvent e) { … }

[On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.SomeCast))]
private void OnSomeCast() { … }
```

Supported attribute arguments:

| Argument | Effect |
|---|---|
| `By = Actor.Player` | restrict source actor (event must implement `IHasSourceEvent`) |
| `To = Actor.Player` | restrict target actor (event must implement `IHasTargetEvent`) |
| `Spell = nameof(Spells.X)` | single ability match (event must implement `IAbilityEvent`) |
| `Spells = new[] { … }` | any of several abilities |
| `ExtraSpell = …` / `ExtraSpells = new[] { … }` | filter `IExtraAbilityEvent.ExtraAbility.Id` |

Use `[On<Event>]` for an unfiltered "any event" subscription. Use `[On<DungeonStartEvent>]` / `[On<DungeonEndEvent>]` to hook the fabricated dungeon-boundary events for dungeon-lifetime setup/finalization - the `DungeonBookendNormalizer` prepends/appends those events to every analysis run. `[On<PullStartEvent>]` anchors a pull-scoped starting timestamp when you need one; pull metrics are otherwise get-style (see step 1), not computed in a handler. The `PullBookendNormalizer` fabricates a start/end pair around each pull, and the parser re-emits `PullEndEvent` to the pull's own analyzers as it closes (once per pull, even a force-close) - subscribe to it only to react to the pull ending as an event, e.g. to snapshot a dungeon-lifetime module's live value at that instant.

## Dependencies

Modules are constructed per analysis run by a generator-emitted factory, not resolved from the DI container; the parser then assigns `Owner`. Sibling-module constructor parameters resolve through the parser's own module cache, and any other parameter type falls back to the service provider. Do not require `CombatLogParser` in a constructor.

Declare a sibling-module dependency with `[Uses<TOther>]` on the class. The generator emits the `Lazy<TOther>` primary-constructor parameter and a cached PascalCase accessor named after the type, so the body reads naturally:

```csharp
[Uses<SpellUsable>]
public sealed partial class FreezingTorrentAnalyzer : Analyzer
{
    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent e)
    {
        if (SpellUsable.IsAvailable(e.Ability.Id)) { … }
    }
}
```

`Lazy<T>` resolution is deferred to dispatch time, so two modules that reference each other can both declare `[Uses<T>]` without hitting the FA0013 cycle diagnostic. A class that also needs an outer service (for example `ILogger`, as `ResourceTracker` does) keeps a hand-written constructor instead; declaring both forms reports FA0018 and the attribute is ignored. For ad-hoc lookups, use `Owner.GetModule<T>()`.

Depend on a parse-lifetime module or analyzer for point-in-time snapshots. Nothing the parser resolves - module, parse-lifetime analyzer, or normalizer - may depend on a `[ForPull]` type (FA0014), because a per-pull instance is constructed straight into the parser's per-pull cache and no resolution path reads it.

## Gating On A Talent

Gate a module on a talent with `[RequiresTalent({Hero}Talents.Name)]`, using a `using ArdeosTalents = FellowshipAnalyzer.Core.Common.Spells.ArdeosTalents;` alias (importing the whole `Core.Common.Spells` namespace collides with the `Spells` registry class). The `{Hero}Talents` constants are generated from the hand-written `Core/Common/Spells/{Hero}/Talents.cs` and carry native ids. Repeat the attribute for AND-ed talents. Do not gate an analyzer whose job is to report whether the optimal talent is taken: gating makes its build-active readout trivially true. Leave that one ungated and record `Owner.SelectedCombatant.HasTalent(...)` into a property instead.

## Key Rules

- Pull-scoped gameplay analysis extends `Analyzer` with `[ForPull]`, and every `Analyzer` registers via `[AddAnalyzer<T>]`. For resources, use `ResourceTracker` through the `create-resource-tracker` skill.
- Mark the class `partial`.
- Declare event subscriptions with `[On<TEvent>]` attributes, never in the constructor.
- Declare sibling-module dependencies with `[Uses<TOther>]` (or `Owner.GetModule<T>()` for ad-hoc reads). Do not take `CombatLogParser` in the constructor.
- Expose per-pull metrics as get-style properties (or methods) over the accumulated state; do not finalize at pull end. For an interval still open at pull end, read `Pull.EndTime` inside the getter; memoize heavy multi-output computations once behind a private field.
- Typed data only: no prose sentences, severity strings, score cards, or `QualitativePerformance` decisions inside the module - those live in the consuming Razor component.
- Keep the module pure C#: no Razor, `RenderFragment`, or Blazor component dependencies.
- Place the file in `Modules/`.

## Checklist

- [ ] File is at `Modules/{Name}Analyzer.cs`.
- [ ] Class is `partial`, extends `Analyzer`, and declares `[ForPull]` unless it is dungeon-lifetime.
- [ ] Event handlers are decorated with `[On<TEvent>]` attributes.
- [ ] Cross-module reads use `[Uses<TOther>]` (or `Owner.GetModule<T>()`), never another analyzer.
- [ ] Per-pull metrics are get-style properties over accumulated state (reading `Pull.EndTime` for still-open intervals), not finalized at pull end.
- [ ] No prose or severity strings in the module.
- [ ] `[AddAnalyzer<T>]` is added to the hero parser.
- [ ] `StatisticsComponentType` is set if a statistics component exists.
