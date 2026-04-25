# FellowshipAnalyzer Architecture Overview

This is the current implementation reference for FellowshipAnalyzer. Keep it concise and factual; task-specific workflows live in `.github/skills/`.

## Runtime Flow

```text
FellowshipLogs API JSON
  -> event deserialization
  -> CombatLogParser.Analyze
  -> normalizers
  -> module Initialize
  -> EventEmitter dispatch
  -> module Complete
  -> HeroAnalysisResult
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

[HeroAnalyzer("rime")]
[AddModule<WinterOrbTracker>]
[AddModule<BasicStComboAnalyzer>]
[AddModule<Modules.Abilities>]
[AddModule<RimeAuras>]
public sealed partial class RimeCombatLogParser : CombatLogParser
{
    public override string HeroId => "rime";
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

Modules are scoped DI services resolved by `CombatLogParser.Analyze`. The parser assigns `Owner` and `Priority` after resolving each module.

```csharp
public abstract class Module
{
    public bool Active { get; protected set; } = true;
    public int Priority { get; set; }
    public CombatLogParser Owner { get; set; } = null!;
    public virtual Type? StatisticsComponentType => null;

    protected int PlayerId => Owner.PlayerId;

    public virtual void Initialize() { }
    public virtual void Complete() { }
}
```

Use this lifecycle:

- Declare modules with `[AddModule<T>]` on the parser. Declaration order becomes module priority.
- Put event subscriptions in `Initialize()`.
- Compute final derived metrics in `Complete()` after all events have been dispatched.
- Expose read-only state for guide and statistics components.
- Use `Owner.GetModule<T>()` or the parser's generated properties for module-to-module access.
- Do not require `CombatLogParser` in module constructors; the parser sets `Owner` after DI resolution.

`Analyzer` is a lightweight specialization of `EventSubscriber`:

```csharp
public class Analyzer : EventSubscriber
{
    public const int SELECTED_PLAYER = 1;
    public const int SELECTED_PLAYER_PET = 2;
}
```

## Event Subscriptions

Analyzers subscribe through the fluent `Events` filter API:

```csharp
public override void Initialize()
{
    AddEventListener(Events.ApplyBuff.By(SELECTED_PLAYER).Spell(Spells.WintersEmbrace), OnWintersEmbraceApplied);
    AddEventListener(Events.RemoveBuff.By(SELECTED_PLAYER).Spell(Spells.WintersEmbrace), OnWintersEmbraceRemoved);
    AddEventListener(Events.Damage.By(SELECTED_PLAYER), OnDamage);
    AddEventListener(Events.Cast.By(SELECTED_PLAYER), OnCast);
}
```

Common filters:

- `Events.Cast`, `Events.Damage`, `Events.Heal`, `Events.ApplyBuff`, `Events.RemoveBuff`, `Events.ResourceChange`, and `Events.Any`.
- `.By(SELECTED_PLAYER)` matches event sources.
- `.To(SELECTED_PLAYER)` matches event targets.
- `.Spell(spellA, spellB)` matches `IAbilityEvent.Ability.Id` against `Spell.Guid` values.
- `.ExtraSpell(spellA)` matches `IExtraAbilityEvent.ExtraAbility.Id`.

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

- It subscribes to `Events.Any` to inspect selected-player `SourceResources` or `TargetResources` snapshots.
- It subscribes to `Events.Cast.By(SELECTED_PLAYER)` to track spends.
- It stores per-resource `ResourceState` objects keyed by `ResourceTypes`.
- Hero trackers override `GetResourceCost(CastEvent, ResourceTypes)` when logs do not provide cost deltas directly.
- Hero trackers may set `MaxOverrides[ResourceTypes.X]` before `base.Initialize()`.
- Convenience properties should expose the hero-specific resource state, totals, and statistics component type.

Use the `create-resource-tracker` skill for new resource trackers.

## Hero Project Layout

Current hero projects follow the Rime layout:

```text
src/FellowshipAnalyzer.Heroes.{Hero}/
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