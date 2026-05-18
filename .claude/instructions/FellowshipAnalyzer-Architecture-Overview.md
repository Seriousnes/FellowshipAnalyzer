# FellowshipAnalyzer Architecture Overview

This is the current implementation reference for FellowshipAnalyzer. Keep it concise and factual; task-specific workflows live in `.github/skills/`.

## Runtime Flow

```text
FellowshipLogs API JSON
  -> event deserialization
  -> CombatLogParser.Analyze
  -> module construction (DI, ctor-time setup, [ActiveWhen<>] gating)
  -> normalizers (FightBookendNormalizer prepends FightStartEvent, appends FightEndEvent)
  -> RegisterSubscriptions on every EventSubscriber
  -> EventEmitter dispatch (FightStartEvent first, FightEndEvent last)
  -> HeroAnalysisResult (modules expose state via ToReport() projections)
  -> Blazor guide/statistics components
```

Important projects:

- `FellowshipAnalyzer.Core` contains events, parser infrastructure, modules, normalizers, spell definitions, and core services.
- `FellowshipAnalyzer.Generators` generates parser constructors, module accessors, module/normalizer type lists, and DI registration extensions.
- `FellowshipAnalyzer.Components` contains shared Razor components and SCSS tokens/mixins.
- `FellowshipAnalyzer.Heroes.Rime` is the current hero implementation and the best source for concrete patterns.
- `FellowshipAnalyzer.FellowshipLogs` contains Fellowship Logs API integration.

## Event Model

Combat events are mutable classes, not structs.

- The base type is `FellowshipAnalyzer.Core.Events.Event`, an abstract partial class decorated for JSON polymorphism.
- Concrete event classes such as `CastEvent`, `DamageEvent`, `ApplyBuffEvent`, and `DeathEvent` inherit from `Event`.
- Event capability interfaces such as `IAbilityEvent`, `IHasSourceEvent`, and `IHasTargetEvent` describe common properties used by filters and analyzers.
- `Ability` models the nested log ability object and maps JSON `guid` to `Ability.Guid`; `Ability.Id` is an ignored alias for `Guid`.
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
[AddModule<Combatants>]
[AddModule<StatTracker>]
[AddModule<Haste>]
[AddModule<GlobalCooldown>]
[AddModule<SpellUsable>]
[AddModule<ChronoshiftAnalyzer>]
public abstract partial class CombatLogParser(EventEmitter eventEmitter, IServiceProvider provider) : IHeroAnalyzer
```

A hero parser is small and declarative:

```csharp
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Rime.Guides;
using FellowshipAnalyzer.Heroes.Rime.Modules;

namespace FellowshipAnalyzer.Heroes.Rime.Analysis;

[HeroAnalyzer(HeroName.Rime)]
[AddModule<WinterOrbTracker>]
[AddModule<BasicStComboAnalyzer>]
[AddModule<Modules.Abilities>]
[AddModule<RimeAuras>]
public sealed partial class RimeCombatLogParser : CombatLogParser
{
    public override Type? GuideComponent => typeof(RimeGuide);
}
```

The generator emits:

- A constructor that passes `EventEmitter` and `IServiceProvider` to the base parser.
- Source-generated nullable module properties such as `BasicStCombo` and `WinterOrbTracker`.
- `GetModuleTypes()` and `GetNormalizerTypes()` implementations.
- `Add{Hero}Analysis()` DI extension methods and keyed `IHeroAnalyzer` registration.
- `AddCoreAnalysis()` for shared analysis services, base modules, and base normalizers.

Application startup should register shared analysis services first, then hero analysis services:

```csharp
builder.Services.AddCoreAnalysisServices();
builder.Services.AddCoreAnalysis();
builder.Services.AddRimeAnalysis();
```

## Module Lifecycle

Modules are scoped DI services resolved by `CombatLogParser.Analyze`. The parser assigns `Owner` and `Priority` after resolving each module. There is no `Initialize` or `Complete` virtual — setup runs in the constructor and finalization lives in a `ToReport()` projection.

```csharp
public abstract class Module
{
    public bool Active { get; protected set; } = true;
    public int Priority { get; set; }
    public CombatLogParser Owner { get; set; } = null!;
    public virtual Type? StatisticsComponentType => null;

    protected int PlayerId => Owner.PlayerId;
}
```

Use this lifecycle:

- Declare modules with `[AddModule<T>]` on the parser. Declaration order becomes module priority; `[Before<T>]` / `[After<T>]` refine it.
- Do setup work that needs the selected player or the raw event list in the constructor — inject `ParseContext` and/or `IReadOnlyList<Event>`.
- Subscribe to events declaratively with `[On<TEvent>]` attributes on instance methods. The `ModuleGenerator` emits the corresponding `RegisterSubscriptions` plumbing.
- Hook fight-boundary setup via `[On<FightStartEvent>]` and finalization via `[On<FightEndEvent>]` (the `FightBookendNormalizer` fabricates both).
- Expose finalized metrics through a `public TReport ToReport()` method. The parser source generator picks it up and includes it in the hero's typed `…AnalysisResult` record. `ToReport()` must be idempotent.
- Use `Lazy<TOther>` ctor injection for cross-module references; the generator emits a cached `_camelCaseName` accessor. `Lazy<>` edges are ignored by the FA0013 cycle analyzer.
- Do not require `CombatLogParser` in module constructors; the parser sets `Owner` after DI resolution.

Activation is two-tiered. Use the mutable `Active` flag for dynamic deactivation that must respect mid-fight state. Use `[ActiveWhen<TPredicate>]` (where `TPredicate : IModuleActivePredicate`) for compile-time gating evaluated at parser construction — predicates read `ParseContext`, including `SelectedCombatant` populated by the earlier `Combatants` module.

`Analyzer` is a lightweight specialization of `EventSubscriber`:

```csharp
public class Analyzer : EventSubscriber
{
    public const int SELECTED_PLAYER = 1;
    public const int SELECTED_PLAYER_PET = 2;
}
```

## Event Subscriptions

Analyzers declare event handlers with `[On<TEvent>]` attributes on instance methods. The class must be `partial`; the `ModuleGenerator` emits the corresponding `RegisterSubscriptions` plumbing.

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

- `By = Actor.Player` / `Actor.Pet` / `Actor.PlayerOrPet` restricts source actor (event must implement `IHasSourceEvent`).
- `To = Actor.Player` / `Actor.Pet` / `Actor.PlayerOrPet` restricts target actor (event must implement `IHasTargetEvent`).
- `Spell = SpellIds.X` or `Spells = new[] { SpellIds.X, SpellIds.Y }` filters `IAbilityEvent.Ability.Id`.
- `ExtraSpell` / `ExtraSpells` filter `IExtraAbilityEvent.ExtraAbility.Id`.

Use `[On<Event>]` for an unfiltered "any event" subscription. The fabricated `FightStartEvent` and `FightEndEvent` always dispatch first and last respectively, courtesy of `FightBookendNormalizer`.

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
- The root guide manually composes feature guide components and null-checks generated module properties.
- Feature guide components inject the hero parser and read module state.
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