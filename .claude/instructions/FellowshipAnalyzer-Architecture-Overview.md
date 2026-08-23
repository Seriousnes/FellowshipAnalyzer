# FellowshipAnalyzer Architecture Overview

This is the current implementation reference for FellowshipAnalyzer. Keep it concise and factual; task-specific workflows live in `.claude/skills/`.

## Runtime Flow

```text
FellowshipLogs API JSON
  -> event deserialization
  -> CombatLogParser.Analyze
  -> module construction (DI, ctor-time setup, [ActiveWhen<>] gating)
  -> normalizers (DungeonBookendNormalizer prepends DungeonStartEvent, appends DungeonEndEvent)
  -> RegisterSubscriptions on every Analyzer
  -> EventEmitter dispatch (DungeonStartEvent first, DungeonEndEvent last)
  -> HeroAnalysisResult
  -> Blazor guide/statistics components read module and analyzer state directly
```

Important projects:

- `FellowshipAnalyzer.Core` contains events, parser infrastructure, modules, normalizers, spell registries, core services, shared Razor UI under `UI/`, SCSS tokens/mixins under `Styles/`, and the Fellowship Logs client under `FellowshipLogs/`.
- `FellowshipAnalyzer.Generators` generates parser constructors, module accessors, pull-analyzer surfaces, module/normalizer type lists, spell registries, talent constants, and DI registration extensions.
- `src/Heroes/` holds one project per hero. `FellowshipAnalyzer.Heroes.Rime` is the compact reference; `FellowshipAnalyzer.Heroes.Ardeos` is the most built-out; `FellowshipAnalyzer.Heroes.Gunde` is the newest scaffold.
- `FellowshipAnalyzer.Api` / `Api.Core` / `Api.GraphQL` cover Fellowship Logs API access on the server side.

## Event Model

Combat events are mutable classes, not structs.

- The base type is `FellowshipAnalyzer.Core.Events.Event`, an abstract partial class decorated for JSON polymorphism.
- Concrete event classes such as `CastEvent`, `DamageEvent`, `ApplyBuffEvent`, and `DeathEvent` inherit from `Event`.
- Event capability interfaces such as `IAbilityEvent`, `IHasSourceEvent`, and `IHasTargetEvent` describe common properties used by filters and analyzers.
- `Ability` models the nested log ability object and maps JSON `guid` to `Ability.FSLID`; `Ability.Id` is an ignored alias for `FSLID`.
- `ActorResources` models `sourceResources` and `targetResources`, including health, position, facing, and resource snapshots.
- `ResourceNormalizer` scales log resource values before analyzers see them.

Do not hand-maintain a static event schema. Use the `analyze-event-schema` skill and `event-schema.cs` tool when validating log JSON against `src/FellowshipAnalyzer.Core/Events/`.

## Parser And Source Generation

`CombatLogParser` is the analysis orchestrator. It is an abstract partial class with base normalizers and base modules declared through attributes:

```csharp
[AddNormalizer<AbilityMasterDataNormalizer>]
[AddNormalizer<ResourceNormalizer>]
[AddNormalizer<CastLinkNormalizer>]
[AddModule<DebugAnnotations>]
[AddAnalyzer<Combatants>]
[AddAnalyzer<StatTracker>]
[AddAnalyzer<Haste>]
[AddAnalyzer<GlobalCooldown>]
[AddAnalyzer<SpellUsable>]
[AddAnalyzer<ChronoshiftAnalyzer>]
public abstract partial class CombatLogParser(EventEmitter eventEmitter, IServiceProvider provider) : IHeroAnalyzer
```

A hero parser is small and declarative:

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Guides;
using FellowshipAnalyzer.Heroes.Rime.Modules;

namespace FellowshipAnalyzer.Heroes.Rime.Analysis;

[HeroAnalyzer(HeroName.Rime)]
[AddAnalyzer<WinterOrbTracker>]
[AddAnalyzer<SingleTargetEmbraceAnalyzer>]
[AddAnalyzer<AoeEmbraceAnalyzer>]
[AddModule<Modules.Abilities>]
[AddModule<RimeAuras>]
public sealed partial class RimeCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(RimeGuide);
}
```

The generator emits:

- A constructor that passes `EventEmitter` and `IServiceProvider` to the base parser.
- Source-generated nullable module properties such as `WinterOrbTracker`, plus the pull read paths for each `[ForPull]` surface (`WintersEmbraceAnalyzers`, `pull.WintersEmbraceAnalyzer`, `Pulls`).
- `GetModuleTypes()` and `GetNormalizerTypes()` implementations.
- `Add{Hero}Analysis()` DI extension methods and keyed `IHeroAnalyzer` registration, both **transient**: a parser serves exactly one analysis, so the host resolves a fresh one per report and a report's read surfaces can never hold another's. Blazor WebAssembly runs the whole session in one DI scope, so a scoped registration would mean one parser for every report the user opens.
- `AddCoreAnalysis()` for shared analysis services, base modules, and base normalizers.

Application startup registers shared analysis services first, then every referenced hero at once through the generated manifest:

```csharp
builder.Services.AddCoreAnalysisServices();
builder.Services.AddCoreAnalysis();
builder.Services.AddFellowshipHeroAnalysis();
```

`AddFellowshipHeroAnalysis()` is emitted by `HeroManifestGenerator` from the `[GenerateHeroManifest]` marker; it scans referenced assemblies for `[HeroAnalyzer]` parsers at compile time and calls each hero's `Add{Hero}Analysis()`, so adding a hero project reference is the whole wiring step.

## Module Lifecycle

Modules are constructed per analysis run by a generator-emitted factory; sibling-module constructor parameters resolve through the parser's own module cache and any other parameter type falls back to the service provider. The parser assigns `Owner` after constructing each module. There is no `Initialize` or `Complete` virtual - setup runs in the constructor, and finalized metrics are exposed as public properties (computed on read, or set from an `[On<DungeonEndEvent>]` handler).

```csharp
public abstract class Module
{
    public bool Active { get; protected set; } = true;
    public virtual int Priority => 0;
    public CombatLogParser Owner { get; set; } = null!;
    public virtual Type? StatisticsComponentType => null;

    protected int PlayerId => Owner.PlayerId;
}
```

Use this lifecycle:

- Declare a type that subscribes to events with `[AddAnalyzer<T>]` on the parser, and one that does not with `[AddModule<T>]` or its synonym `[AddState<T>]` (FA0019). `Priority` is a design-time constant a module overrides, defaulting to 0; `[Before<T>]` / `[After<T>]` order modules that share one, and guarantee only the pairwise relation they name.
- Do setup work that needs the selected player or the raw event list in the constructor - inject `ParseContext` and/or `IReadOnlyList<Event>`.
- Subscribe to events declaratively with `[On<TEvent>]` attributes on instance methods. The `ModuleGenerator` emits the corresponding `RegisterSubscriptions` plumbing.
- Hook dungeon-boundary setup via `[On<DungeonStartEvent>]` and finalization via `[On<DungeonEndEvent>]` (the `DungeonBookendNormalizer` fabricates both).
- Expose state as public read-only properties and typed entry records; guide and statistics components read them directly. Keep prose, severity wording, and `PerformanceTier` judgments in the Razor components - modules hold typed data only.
- Declare cross-module references with `[Uses<TOther>]` on the class; the generator emits the `Lazy<TOther>` primary-constructor parameter and a cached PascalCase accessor named after the type. `Lazy<>` edges are ignored by the FA0013 cycle analyzer. A class that also needs an outer service (such as `ILogger`) keeps a hand-written constructor instead.
- Do not require `CombatLogParser` in module constructors; the parser sets `Owner` after DI resolution.

Activation is two-tiered. Use the mutable `Active` flag for dynamic deactivation that must respect mid-dungeon state. Use `[ActiveWhen<TPredicate>]` (where `TPredicate : IModuleActivePredicate`) for compile-time gating evaluated at parser construction - predicates read `ParseContext`, including `SelectedCombatant`, which the parser builds from the player's `CombatantInfoEvent` before any module is constructed.

`Analyzer` is the base for every event subscriber, and `[ForPull(PullKind…, Boss = …)]` on the analyzer is what makes one pull-lifetime. A fresh instance is constructed for every matching pull, accumulates that pull's events into private state, and is retained on the pull read surfaces; it exposes its metrics as get-style properties (reading its assigned `Pull` for boundary values such as `Pull.EndTime`) rather than finalizing at pull end. The parser still emits a `PullEndEvent` to the pull's own analyzers as it closes (once per pull, even a force-close) for anything that must react to the pull ending as an event, such as snapshotting a dungeon-lifetime module's live state:

- `parser.{Surface}s` - the cross-pull stream, an `IReadOnlyList<PullAnalyzer<T>>` of `(Pull, Analyzer)` pairs.
- `parser.For(pull).{Surface}` and the `pull.{Surface}` extension - the retained instance for one pull.

The surface type is the topmost ancestor deriving directly from `Analyzer`, so shape-specialized subclasses (disjoint `[ForPull]` filters over a shared abstract base) feed one stream. `[ForPull]` is valid only on a concrete `Analyzer` (FA0020, FA0021), and nothing the parser resolves - module, parse-lifetime analyzer, or normalizer - may depend on a `[ForPull]` type (FA0014); depend on a parse-lifetime one instead.

## Event Subscriptions

Analyzers declare event handlers with `[On<TEvent>]` attributes on instance methods. The class must be `partial`; the `ModuleGenerator` emits the corresponding `RegisterSubscriptions` plumbing.

A handler takes the dispatched event as a single parameter, typed as `TEvent`, one of its base classes or interfaces, or a `OneOf<…>` carrying a slot for it, or it takes no parameter at all when it reads nothing off the event. The generator matches the emitted call to the declared parameters, and the attribute's filters select the events either way.

```csharp
public sealed partial class WintersEmbraceAnalyzer : Analyzer
{
    [On<ApplyBuffEvent>(By = Actor.Player, Spell = SpellIds.WintersEmbrace)]
    private void OnWintersEmbraceApplied(ApplyBuffEvent e) { … }

    [On<RemoveBuffEvent>(By = Actor.Player, Spell = SpellIds.WintersEmbrace)]
    private void OnWintersEmbraceRemoved(RemoveBuffEvent e) { … }

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamage(DamageEvent e) { … }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent e) { … }
}
```

Supported attribute arguments:

- `By = Actor.Player` restricts source actor (event must implement `IHasSourceEvent`).
- `To = Actor.Player` restricts target actor (event must implement `IHasTargetEvent`).
- `Spell = SpellIds.X` or `Spells = new[] { SpellIds.X, SpellIds.Y }` filters `IAbilityEvent.Ability.Id`.
- `ExtraSpell` / `ExtraSpells` filter `IExtraAbilityEvent.ExtraAbility.Id`.

Use `[On<Event>]` for an unfiltered "any event" subscription. The fabricated `DungeonStartEvent` and `DungeonEndEvent` always dispatch first and last respectively, courtesy of `DungeonBookendNormalizer`.

## Normalizers

Normalizers implement `IEventNormalizer` and run before module initialization and event dispatch.

```csharp
public interface IEventNormalizer
{
    int Priority { get; }
    List<Event> Normalize(List<Event> events, int playerId);
}
```

Current execution follows the generated normalizer type list, which preserves `[AddNormalizer<T>]` declaration order with base normalizers before hero normalizers. Keep `Priority` aligned with the intended order because normalizer implementations and comments use it as documentation.

Normalizers may mutate the list in place or return a new list. They are appropriate for ability master-data hydration, resource scaling, cast/channel linking, event reordering, event linking, and synthetic event fabrication.

## Resource Tracking

`ResourceTracker` tracks all observed `ResourceTypes` for the selected player.

- It subscribes to `[On<Event>]` to inspect selected-player `SourceResources` or `TargetResources` snapshots.
- It subscribes to `[On<CastEvent>(By = Actor.Player)]` to track spends and `[On<ResourceChangeEvent>(By = Actor.Player)]` to track gains.
- It stores per-resource `ResourceState` objects keyed by `ResourceTypes`.
- Hero trackers override `GetResourceCost(CastEvent, ResourceTypes)` when logs do not provide cost deltas directly.
- Hero trackers may set `MaxOverrides[ResourceTypes.X]` in their constructor.
- Convenience properties should expose the hero-specific resource state, totals, and statistics component type.

Use the `create-resource-tracker` skill for new resource trackers.

## Hero Project Layout

Current hero projects follow the Rime layout:

```text
src/Heroes/FellowshipAnalyzer.Heroes.{Hero}/
  {Hero}CombatLogParser.cs
  {Hero}Guide.razor
  _Imports.razor
  sasscompiler.json
  Modules/
    Abilities.cs
    {Feature}Analyzer.cs
    {Resource}Tracker.cs
  Guides/
    {Feature}Guide.razor
  Statistics/
    {Feature}Statistics.razor
  Normalizers/
    {Feature}Normalizer.cs
```

Shared spell identity data lives in `src/FellowshipAnalyzer.Core/Common/Spells/`. Hero projects define gameplay metadata in their `Modules/Abilities.cs` module using `SpellbookAbility` entries.

## UI Integration

- The parser's `GuideComponent` points to the hero's root guide component.
- The root guide manually composes feature guide components in reading order and renders each one unconditionally.
- Feature guide components inherit `GuideComponent<{Hero}CombatLogParser>` and override `protected abstract bool IsActive()` with their own activation condition. `GuideComponent` overrides `SetParametersAsync` and returns without queueing a render when `IsActive()` is false, so an inactive guide contributes no frames and runs no lifecycle method; the guide writes its markup with no gate of its own. The root guide inherits `ReportComponent<{Hero}CombatLogParser>`; hero-agnostic report components inherit the non-generic `ReportComponent`. A parser is transient - one instance per analysis - so it reaches a component through the report shell's cascade, never through `@inject`.
- Statistics components inherit `AnalyzerStatistic<TModule>` and are auto-collected from active modules with non-null `StatisticsComponentType`.
- Component styling uses `.razor.scss`; use the `style-guide` skill for all styling work.

## Skills Are The Source For Workflows

Use the relevant skill instead of extending this architecture overview with procedural details:

- `create-hero` for new hero scaffolding.
- `create-analyzer` for event-driven modules.
- `create-resource-tracker` for resource tracking.
- `create-normalizer` for event preprocessing.
- `create-guide` for guide components.
- `create-statistics` for statistics cards.
- `analyze-event-schema` and `analyze-log-resources` for log inspection.
- `style-guide` for SCSS and component styling.