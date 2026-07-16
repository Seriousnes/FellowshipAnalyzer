---
name: create-analyzer
description: "Create a pure C# analyzer module that subscribes to combat log events, tracks state, and computes metrics. Use when: adding a new talent analyzer, ability analyzer, feature analyzer, or any event-driven analysis module. NOT for ResourceTrackers, guide components, or statistics components."
---

# Create Analyzer

An analyzer is a pure C# module in the `Modules/` folder. It subscribes to combat log events, tracks state, and exposes computed metrics as public properties that guide and statistics components read directly. It has no Blazor dependency and holds typed data only: counts, rates, timestamps, enums, and typed entry records. Prose, severity wording, and `PerformanceTier` judgments belong in the Razor components that consume it.

Guide rendering belongs in the `create-guide` skill. Statistics rendering belongs in the `create-statistics` skill. Resource tracking belongs in the `create-resource-tracker` skill.

Reference implementation: `src/Heroes/FellowshipAnalyzer.Heroes.Tariq/Modules/FuryEconomyAnalyzer.cs` with `Guides/FuryEconomyGuide.razor`.

## Two lifetimes

- **Pull-lifetime analyzer** (the default for gameplay analysis): derives from `Analyzer`, is declared with `[AddAnalyzer<T>]` on the parser, and carries `[ForPull(PullKind…, Boss = …)]`. A fresh instance is constructed for every matching pull, so its state is per-pull by construction.
- **Fight-lifetime module**: derives from `EventSubscriber`, is declared with `[AddModule<T>]` (or `[AddState<T>]` when pull analyzers depend on it), and observes the whole fight. Use for cross-pull state, statistics sources, and infrastructure.

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

An analyzer finalizes nothing at pull end - it exposes its metrics as **get-style** properties (or methods) evaluated when a guide reads them. An analyzer's own state is frozen once its pull ends (its listeners are cleared), so a getter always sees the final state. Aggregates over completed data are plain get-style properties. When a metric depends on an interval still open at pull end (a buff still up, a resource still capped, a DoT with no logged remove), read the boundary from the analyzer's `Pull` property inside the getter - `Pull.EndTime` is the close time. For a heavy multi-output computation, run it once behind a private nullable field (`_result ??= Compute()`) so repeated reads do not recompute. Subscribe to `[On<PullEndEvent>]` only to react to the pull ending as an event: the one thing get-style cannot do is snapshot another (fight-lifetime) module's live state at the instant this pull closes.

### 2. Register On The CombatLogParser

Pull-lifetime analyzers use `[AddAnalyzer<T>]`; fight-lifetime modules use `[AddModule<T>]` (declaration order is module priority).

```csharp
[HeroAnalyzer(HeroName.{Hero})]
[AddAnalyzer<{Name}Analyzer>]
[AddModule<Modules.Abilities>]
public sealed partial class {Hero}CombatLogParser : CombatLogParser
```

For each `[AddAnalyzer]` surface type the source generator produces three read paths plus DI wiring:

- `parser.{Name}Analyzers` - the cross-pull stream, `IReadOnlyList<PullAnalyzer<{Name}Analyzer>>` of `(Pull, Analyzer)` pairs in pull order.
- `parser.For(pull).{Name}Analyzer` and the `pull.{Name}Analyzer` extension - the retained instance for one pull (nullable).

The surface type is the analyzer's `IAnalyzerSurface` marker interface if it implements one, otherwise the topmost ancestor deriving directly from `Analyzer`. Three composition patterns cover pull-shape variation:

- **One analyzer, every shape:** a single flat analyzer with a `[ForPull]` filter that matches all shapes it runs on. It is its own surface.
- **Different questions per shape:** independent analyzers answering different questions for different shapes (e.g. `SearingBlazeUptimeAnalyzer` on boss pulls and `SearingBlazeSpreadAnalyzer` on trash pulls in Ardeos). Give both a shared **surface marker interface** - `public interface ISearingBlazeAnalyzer : IAnalyzerSurface;`, then `: Analyzer, ISearingBlazeAnalyzer` on each - so they share no base class or behaviour but expose one `parser.SearingBlazeAnalyzers` stream and `pull.SearingBlazeAnalyzer` accessor (typed as the interface). Each keeps its own disjoint `[ForPull]`; the guide switches on the concrete type per row (see create-guide). An analyzer may implement at most one surface interface (FA0017).
- **One question, shape-specific scoring:** when the shapes share subscriptions and accumulated state and differ only in finalization, put the shared machinery on an abstract base and give each concrete subclass a disjoint `[ForPull]` filter plus its own scoring strategy and output subtype (see `BasicStComboAnalyzer` with `SingleTargetRimeCombo` / `AoERimeCombo` in Rime); the base is the single surface all read paths use.

FA0016 enforces disjoint `[ForPull]` filters across analyzers sharing a surface (a marker interface or a base class). Inheritance (the third pattern) is earned when the base owns real machinery and each subclass owns its strategy and outputs; when the analyses share nothing, prefer the marker interface. A base that implements every strategy itself while subclasses one-line-dispatch into it wants a flat pattern instead.

For `[AddModule]` modules the generator emits a typed nullable parser property (`{Name}Analyzer` becomes `{Name}` - the `Analyzer` suffix is stripped).

### 3. Optionally Set StatisticsComponentType

If this module has a statistics component, expose it (a dynamic expression is fine - it is read after analysis completes):

```csharp
public override Type? StatisticsComponentType => Procs > 0 ? typeof({Name}Statistics) : null;
```

## Event Subscription API

Declare each handler with a `[On<TEvent>]` attribute on a private (or internal) instance method. The `ModuleGenerator` translates the attributes into a `RegisterAttributeSubscriptions` override with inlined predicates.

```csharp
[On<CastEvent>(By = Actor.Player)]
private void OnCast(CastEvent e) { … }

[On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.SomeBuff))]
private void OnBuffApply(ApplyBuffEvent e) { … }

[On<DamageEvent>(By = Actor.Player, Spells = new[] { nameof(Spells.A), nameof(Spells.B) })]
private void OnDamage(DamageEvent e) { … }
```

Supported attribute arguments:

| Argument | Effect |
|---|---|
| `By = Actor.Player` / `Actor.Pet` / `Actor.PlayerOrPet` | restrict source actor (event must implement `IHasSourceEvent`) |
| `To = Actor.Player` / `Actor.Pet` / `Actor.PlayerOrPet` | restrict target actor (event must implement `IHasTargetEvent`) |
| `Spell = nameof(Spells.X)` | single ability match (event must implement `IAbilityEvent`) |
| `Spells = new[] { … }` | any of several abilities |
| `ExtraSpell = …` / `ExtraSpells = new[] { … }` | filter `IExtraAbilityEvent.ExtraAbility.Id` |

Use `[On<Event>]` for an unfiltered "any event" subscription. Use `[On<FightStartEvent>]` / `[On<FightEndEvent>]` to hook the fabricated fight-boundary events for fight-lifetime setup/finalization - the `FightBookendNormalizer` prepends/appends those events to every analysis run. `[On<PullStartEvent>]` anchors a pull-scoped starting timestamp when you need one; pull metrics are otherwise get-style (see step 1), not computed in a handler. The `PullBookendNormalizer` fabricates a start/end pair around each pull, and the parser re-emits `PullEndEvent` to the pull's own analyzers as it closes (once per pull, even a force-close) - subscribe to it only to react to the pull ending as an event, e.g. to snapshot a fight-lifetime module's live value at that instant.

## Dependencies

Modules are resolved from DI, then the parser assigns `Owner`. Do not require `CombatLogParser` in an analyzer constructor.

For module-to-module access, prefer `Lazy<TOther>` constructor injection. The `ModuleGenerator` emits a cached `_camelCaseName` private accessor for every primary-ctor parameter of type `Lazy<TModule>`:

```csharp
public sealed partial class FreezingTorrentAnalyzer(Lazy<SpellUsable> spellUsable) : Analyzer
{
    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent e)
    {
        if (_spellUsable.IsAvailable(e.Ability.Id)) { … }
    }
}
```

`Lazy<T>` defers resolution to dispatch time, so two modules that reference each other can ctor-inject through `Lazy<>` without hitting the FA0013 cycle diagnostic. Plain (non-Lazy) module-to-module ctor injection is fine for acyclic dependencies. For ad-hoc lookups, use `Owner.GetModule<T>()`.

A pull-lifetime analyzer may depend on `[AddState]` fight-lifetime modules for point-in-time snapshots, but never on another analyzer (FA0014).

## Key Rules

- Pull-scoped gameplay analysis extends `Analyzer` with `[ForPull]` and registers via `[AddAnalyzer<T>]`. For resources, use `ResourceTracker` through the `create-resource-tracker` skill.
- Mark the class `partial`.
- Declare event subscriptions with `[On<TEvent>]` attributes, never in the constructor.
- Use `Lazy<TOther>` ctor injection to break dependency cycles. Do not take `CombatLogParser` in the constructor.
- Expose per-pull metrics as get-style properties (or methods) over the accumulated state; do not finalize at pull end. For an interval still open at pull end, read `Pull.EndTime` inside the getter; memoize heavy multi-output computations once behind a private field.
- Typed data only: no prose sentences, severity strings, score cards, or `QualitativePerformance` decisions inside the module - those live in the consuming Razor component.
- Keep the module pure C#: no Razor, `RenderFragment`, or Blazor component dependencies.
- Place the file in `Modules/`.

## Checklist

- [ ] File is at `Modules/{Name}Analyzer.cs`.
- [ ] Class is `partial`, extends `Analyzer`, and declares `[ForPull]` (or is a fight-lifetime `EventSubscriber` registered with `[AddModule]`).
- [ ] Event handlers are decorated with `[On<TEvent>]` attributes.
- [ ] Cross-module reads use `Lazy<TOther>` ctor injection (or `Owner.GetModule<T>()`), never another analyzer.
- [ ] Per-pull metrics are get-style properties over accumulated state (reading `Pull.EndTime` for still-open intervals), not finalized at pull end.
- [ ] No prose or severity strings in the module.
- [ ] `[AddAnalyzer<T>]` / `[AddModule<T>]` is added to the hero parser.
- [ ] `StatisticsComponentType` is set if a statistics component exists.
