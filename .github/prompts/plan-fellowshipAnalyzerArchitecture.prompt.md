# Plan: FellowshipAnalyzer Module Framework Architecture

## TL;DR

Design an extensible framework for Fellowship hero analysis modules. CombatLogParser is a scoped DI service with source-generated typed properties for each module. Modules are split into up to 3 files: pure C# analyzer (logic + events), guide Razor component, and optional statistics Razor component. Normalizers are standalone classes, not modules. Guide.razor is a mandatory per-hero page with manual composition. Statistics tab auto-collects from active modules using DynamicComponent + CascadingValue. Parser access is via DI injection throughout.

---

## Architecture Overview

### Per-Analyzer File Convention

Each analyzer can have up to 3 files:

| File | Purpose | Example |
|------|---------|---------|
| `{Name}Analyzer.cs` | Pure C# — event subscriptions, state tracking, computed results | `ElementalAssaultAnalyzer.cs` |
| `{Name}Guide.razor` | Guide tab section — manually placed in Guide.razor | `ElementalAssaultGuide.razor` |
| `{Name}Statistics.razor` | Statistics tab — auto-collected, rendered via DynamicComponent | `ElementalAssaultStatistics.razor` |

- The Guide razor component `@inject`s the parser from DI to access the analyzer's state.
- The Statistics razor component receives its analyzer via `[CascadingParameter]` (wrapped in CascadingValue by the auto-collecting statistics renderer).
- The Analyzer class references its statistics component type (for auto-collection) but has no knowledge of the guide component (guide composition is manual).

### Dependency Graph

```
FellowshipAnalyzer.Core (net10.0, NO Blazor dependency)
├── Analysis/
│   ├── Module (plain abstract class)
│   ├── EventSubscriber : Module
│   ├── Analyzer : EventSubscriber
│   ├── ResourceTracker : Analyzer (abstract)
│   ├── EventEmitter
│   ├── CombatLogParser (abstract, partial — source gen target)
│   ├── IEventNormalizer (interface)
│   ├── AddModuleAttribute<T>
│   ├── AddNormalizerAttribute<T>
│   └── HeroAnalysisResult
├── Events/ — Event types, interfaces, discriminators
├── Serialization/ — WCLJsonConverter
└── Models/

FellowshipAnalyzer.Components (Razor Class Library)
├── Shared UI widgets (CastDetail, CastOverview, GuideSection, StatCard, etc.)
├── AnalyzerStatistic<T> base class (for statistics components)
└── References: Core

FellowshipAnalyzer.Heroes.Rime (Razor Class Library)
├── Analysis/
│   ├── RimeCombatLogParser.cs (partial, [AddModule], [AddNormalizer])
│   └── RimeAnalysisDefinition.cs
├── Analyzers/ — Pure C# classes
│   ├── BasicStComboAnalyzer.cs
│   ├── WinterOrbTracker.cs
│   ├── FreezingTorrentAnalyzer.cs
│   └── Abilities.cs
├── Guides/ — Razor guide sections, manually composed
│   ├── RimeGuide.razor (the mandatory Guide page)
│   ├── BasicStComboGuide.razor
│   ├── WinterOrbGuide.razor
│   └── FreezingTorrentGuide.razor
├── Statistics/ — Razor statistics, auto-collected
│   ├── WinterOrbStatistics.razor
│   └── (etc.)
├── Normalizers/ — Standalone classes
│   └── (future normalizers)
└── References: Core, Components

FellowshipAnalyzer.Generators (netstandard2.0, source generator)
└── Processes [AddModule<T>] and [AddNormalizer<T>] attributes

FellowshipAnalyzer.FellowshipLogs.Abstractions → Core
FellowshipAnalyzer.FellowshipLogs              → Core, Abstractions
FellowshipAnalyzer.FellowshipLogs.Http         → Core, Abstractions

FellowshipAnalyzer.Client (WASM)
    References: Core, Components, Heroes.Rime, FellowshipLogs.Http

FellowshipAnalyzer (Server Host)
    References: Client, Core, FellowshipLogs, ServiceDefaults

FellowshipAnalyzer.AppHost (Aspire) → Server
```

---

## Module Base Classes (Core)

### Module (plain class, no ComponentBase)

```
Module (abstract)
├── bool Active { get; protected set; } = true
├── int Priority { get; internal set; }
├── CombatLogParser Owner { get; internal set; }
├── Type? StatisticsComponentType { get; virtual } => null
├── virtual void Initialize()
├── virtual void Complete()

EventSubscriber : Module
├── void AddEventListener<T>(EventFilter<T>, Action<T>)

Analyzer : EventSubscriber
├── const int SELECTED_PLAYER = 1
├── const int SELECTED_PLAYER_PET = 2

ResourceTracker : Analyzer (abstract)
├── int Generated, Wasted, Spent, Current
├── IReadOnlyList<ResourceEvent> ResourceEvents
```

Key change: Module does NOT extend ComponentBase. No RenderFragment, no GuideContent, no GuideOrder. The only rendering hook is `StatisticsComponentType` for auto-collected statistics.

### IEventNormalizer (standalone, not a Module)

```csharp
public interface IEventNormalizer
{
    int Priority { get; }
    IReadOnlyList<Event> Normalize(IReadOnlyList<Event> events, int playerId);
}
```

Normalizers are registered via `[AddNormalizer<T>]` on the parser. They run in Priority order before event dispatch. They are separate from the module lifecycle.

---

## Source Generator Design

### Input: Attributes on CombatLogParser subclass

```csharp
[AddNormalizer<EventOrderNormalizer>]
[AddModule<TrackedStateModule>]
[AddModule<WinterOrbTracker>]
[AddModule<BasicStComboAnalyzer>]
public sealed partial class RimeCombatLogParser : CombatLogParser
{
    // Hand-written: hero-specific config
    public override string HeroId => "rime";
    public override HeroAnalysisDefinition Definition => RimeAnalysisDefinition.Instance;
    public override Type GuideComponentType => typeof(RimeGuide);
}
```

### Output: Source-generated partial class

```csharp
// Auto-generated
public sealed partial class RimeCombatLogParser
{
    // Typed nullable properties for each module
    public TrackedStateModule? TrackedState { get; internal set; }
    public WinterOrbTracker? WinterOrbTracker { get; internal set; }
    public BasicStComboAnalyzer? BasicStCombo { get; internal set; }

    // Ordered module type list (for DI resolution + construction)
    public static IReadOnlyList<Type> RegisteredModuleTypes =>
        [typeof(TrackedStateModule), typeof(WinterOrbTracker), typeof(BasicStComboAnalyzer)];

    // Ordered normalizer type list
    public static IReadOnlyList<Type> RegisteredNormalizerTypes =>
        [typeof(EventOrderNormalizer)];

    // Assignment method: called after module construction, assigns to property if Active
    internal void AssignModule(Module module)
    {
        switch (module)
        {
            case TrackedStateModule m: TrackedState = m; break;
            case WinterOrbTracker m: WinterOrbTracker = m; break;
            case BasicStComboAnalyzer m: BasicStCombo = m; break;
        }
    }

    // Statistics collection: returns (module, componentType) for active modules with statistics
    public IEnumerable<(Module Module, Type ComponentType)> Statistics =>
        ActiveModules.Where(m => m.StatisticsComponentType is not null)
                     .Select(m => (m, m.StatisticsComponentType!));
}
```

### DI Registration (also source-generated)

```csharp
// Auto-generated extension method
public static IServiceCollection AddRimeAnalysis(this IServiceCollection services)
{
    services.AddScoped<EventEmitter>();
    services.AddScoped<RimeCombatLogParser>();
    services.AddScoped<CombatLogParser>(sp => sp.GetRequiredService<RimeCombatLogParser>());
    services.AddScoped<IHeroAnalyzer>(sp => sp.GetRequiredService<RimeCombatLogParser>());

    // Modules
    services.AddScoped<TrackedStateModule>();
    services.AddScoped<WinterOrbTracker>();
    services.AddScoped<BasicStComboAnalyzer>();

    // Normalizers
    services.AddScoped<EventOrderNormalizer>();

    return services;
}
```

---

## CombatLogParser Pipeline

```csharp
public abstract class CombatLogParser
{
    public abstract string HeroId { get; }
    public abstract HeroAnalysisDefinition Definition { get; }
    public abstract Type GuideComponentType { get; }

    public EventEmitter EventEmitter { get; }
    public IReadOnlyList<Event> Events { get; set; }
    public int PlayerId { get; set; }
    public IReadOnlyList<Module> ActiveModules { get; private set; }

    // Source-generated overrides:
    // abstract void AssignModule(Module m);
    // abstract static IReadOnlyList<Type> RegisteredModuleTypes { get; }
    // abstract static IReadOnlyList<Type> RegisteredNormalizerTypes { get; }

    public HeroAnalysisResult Analyze(IReadOnlyList<Event> events, int playerId)
    {
        Events = events;
        PlayerId = playerId;

        // 1. Run normalizers in priority order
        var normalized = events;
        foreach (var normalizerType in RegisteredNormalizerTypes)
        {
            var normalizer = (IEventNormalizer)provider.GetRequiredService(normalizerType);
            normalized = normalizer.Normalize(normalized, playerId);
        }
        Events = normalized;

        // 2. Resolve and assign modules
        var modules = RegisteredModuleTypes
            .Select(t => (Module)provider.GetRequiredService(t))
            .ToList();

        var priority = 0;
        foreach (var module in modules)
        {
            module.Owner = this;
            module.Priority = priority++;
            if (module.Active)
                AssignModule(module); // source-generated switch
        }

        ActiveModules = modules.Where(m => m.Active).ToList();

        // 3. Initialize → dispatch events → Complete
        foreach (var m in ActiveModules) m.Initialize();
        EventEmitter.SortListeners();
        foreach (var e in Events.OrderBy(e => e.Timestamp))
            EventEmitter.TriggerEvent(e);
        foreach (var m in ActiveModules) m.Complete();

        return new HeroAnalysisResult
        {
            GuideComponentType = GuideComponentType,
            Statistics = Statistics.ToList(),
            Modules = ActiveModules,
        };
    }
}
```

---

## Rendering Flow

### Report.razor (Client)

```razor
@inject IHeroAnalyzer HeroAnalyzer
@inject IFellowshipLogsClient Client

<!-- Tab: Guide -->
@if (_result is not null)
{
    <DynamicComponent Type="@_result.GuideComponentType" />
}

<!-- Tab: Statistics -->
@if (_result is not null)
{
    @foreach (var (module, componentType) in _result.Statistics)
    {
        <CascadingValue Value="@module">
            <DynamicComponent Type="@componentType" />
        </CascadingValue>
    }
}

@code {
    private HeroAnalysisResult? _result;

    protected override async Task OnInitializedAsync()
    {
        var events = await Client.GetEventsAsync(...);
        _result = HeroAnalyzer.Analyze(events, playerId);
    }
}
```

### RimeGuide.razor (Manual Composition)

```razor
@inject RimeCombatLogParser Parser

<Section Title="Single Target Combo">
    @if (Parser.BasicStCombo is not null)
    {
        <BasicStComboGuide />
    }
</Section>
<Section Title="Winter Orb Management">
    @if (Parser.WinterOrbTracker is not null)
    {
        <WinterOrbGuide />
    }
</Section>
```

### BasicStComboGuide.razor (Guide Component)

```razor
@inject RimeCombatLogParser Parser

@{ var analyzer = Parser.BasicStCombo!; }

<GuideSection Title="Single Target Combo">
    <CastOverview Stats="@analyzer.BuildOverviewStats()" />
    <CastDetail Casts="@analyzer.BuildPerCastData()" />
</GuideSection>
```

### WinterOrbStatistics.razor (Auto-Collected Statistics Component)

```razor
@inherits AnalyzerStatistic<WinterOrbTracker>

<StatCard Title="Winter Orb Efficiency">
    <p>@Analyzer.Generated generated, @Analyzer.Wasted wasted</p>
    <GradiatedPerformanceBar Score="@Analyzer.EfficiencyScore" />
</StatCard>
```

Where `AnalyzerStatistic<T>` is:
```csharp
public abstract class AnalyzerStatistic<T> : ComponentBase where T : Module
{
    [CascadingParameter] public Module Module { get; set; }
    protected T Analyzer => (T)Module;
}
```

---

## Module Dependencies

- **Required dependencies**: Constructor injection from DI. The DI container resolves module dependencies automatically.
  ```csharp
  public class ElementalAssaultAnalyzer(CombatLogParser parser, MaelstromWeaponTracker tracker) : Analyzer(parser)
  ```
- **Optional dependencies**: Via source-generated properties on the parser (nullable).
  ```csharp
  // In some analyzer that optionally uses another's data:
  var feralSpirit = Owner is RimeCombatLogParser rp ? rp.FeralSpirit : null;
  ```

---

## Steps

### Phase 1: Core Framework (blocking)

1. Remove ComponentBase from Module — make it a plain abstract class with no Blazor dependency
2. Add `StatisticsComponentType` virtual property to Module (returns null by default)
3. Add `GuideComponentType` abstract property to CombatLogParser
4. Add `IEventNormalizer` interface to Core
5. Create `AddNormalizerAttribute<T>` in Core
6. Update `HeroAnalysisResult` — add `GuideComponentType`, `Statistics` list, remove `GuideSections`
7. Remove `Microsoft.AspNetCore.Components` package reference from Core.csproj
8. Update `CombatLogParser.Analyze()` pipeline: normalizers → module resolution → assign → initialize → dispatch → complete

### Phase 2: Source Generator (depends on Phase 1)

9. Implement source generator processing `[AddModule<T>]`:
   - Generate typed nullable properties (property name = class name, stripping "Analyzer" suffix if present)
   - Generate `RegisteredModuleTypes` static property
   - Generate `AssignModule(Module)` switch method
   - Generate `Statistics` property
10. Implement source generator processing `[AddNormalizer<T>]`:
    - Generate `RegisteredNormalizerTypes` static property
11. Generate `Add{Hero}Analysis()` DI extension method per parser subclass

### Phase 3: Components Infrastructure (parallel with Phase 2)

12. Create `AnalyzerStatistic<T>` base class in Components project — CascadingParameter of Module, typed Analyzer property
13. Add Core project reference to Components.csproj (for Module type)
14. Move shared data models (ScoreCard, PerformanceTier, Finding) to Core if they aren't there already

### Phase 4: Rime Hero Migration (depends on Phases 1-3)

15. Split existing combined modules:
    - `BasicStComboGuide.razor.cs` → `Analyzers/BasicStComboAnalyzer.cs` (pure C#)
    - `BasicStComboGuide.razor` → `Guides/BasicStComboGuide.razor` (@injects parser)
    - `WinterOrbGuide.razor.cs` → delete (fold into WinterOrbTracker or separate analyzer)
    - `WinterOrbGuide.razor` → `Guides/WinterOrbGuide.razor` (@injects parser)
    - `WinterOrbTracker.cs` → `Analyzers/WinterOrbTracker.cs` (add StatisticsComponentType)
    - Create `Statistics/WinterOrbStatistics.razor` (inherits AnalyzerStatistic<WinterOrbTracker>)
16. Create `Guides/RimeGuide.razor` — mandatory guide page, manually composes guide sections
17. Update `RimeCombatLogParser` — add [AddModule], [AddNormalizer] attrs, make partial, add GuideComponentType
18. Delete manual `RimeServiceCollectionExtensions` (replaced by source-generated version)
19. Reorganize folder structure: Analyzers/, Guides/, Statistics/, Normalizers/

### Phase 5: Client Update (depends on Phase 4)

20. Update `Report.razor` — DynamicComponent for Guide tab, CascadingValue loop for Statistics tab
21. Update Client `Program.cs` — use source-generated `AddRimeAnalysis()` extension

### Phase 6: Validation

22. `dotnet build FellowshipAnalyzer.slnx` — verify Core has zero Blazor references
23. `dotnet test` — existing tests pass
24. Smoke test — guides + statistics render correctly in browser

---

## Relevant Files

**Core (modify):**
- `src/FellowshipAnalyzer.Core/Analysis/Module.cs` — Remove ComponentBase, add StatisticsComponentType
- `src/FellowshipAnalyzer.Core/Analysis/CombatLogParser.cs` — Add GuideComponentType, update Analyze() pipeline
- `src/FellowshipAnalyzer.Core/Analysis/HeroAnalysisResult.cs` — Add GuideComponentType, Statistics
- `src/FellowshipAnalyzer.Core/Analysis/EventSubscriber.cs` — Remove ComponentBase inheritance chain
- `src/FellowshipAnalyzer.Core/Analysis/AddModuleAttribute.cs` — Already exists
- `src/FellowshipAnalyzer.Core/FellowshipAnalyzer.Core.csproj` — Remove AspNetCore.Components ref
- `src/FellowshipAnalyzer.Core/Analysis/IEventNormalizer.cs` — New
- `src/FellowshipAnalyzer.Core/Analysis/AddNormalizerAttribute.cs` — New

**Components (modify):**
- `src/FellowshipAnalyzer.Components/AnalyzerStatistic.cs` — New base class
- `src/FellowshipAnalyzer.Components/FellowshipAnalyzer.Components.csproj` — Add Core reference

**Source Generator (modify):**
- `src/FellowshipAnalyzer.Generators/` — AddModule + AddNormalizer processing

**Heroes.Rime (modify + reorganize):**
- `src/FellowshipAnalyzer.Heroes.Rime/Analysis/RimeCombatLogParser.cs`
- `src/FellowshipAnalyzer.Heroes.Rime/Analyzers/*.cs` — Pure C# analyzers
- `src/FellowshipAnalyzer.Heroes.Rime/Guides/RimeGuide.razor` + per-section guide components
- `src/FellowshipAnalyzer.Heroes.Rime/Statistics/*.razor` — Auto-collected stats components
- `src/FellowshipAnalyzer.Heroes.Rime/Normalizers/*.cs` — Standalone normalizer classes

**Client (modify):**
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Client/Pages/Report.razor` — DynamicComponent rendering
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Client/Program.cs` — Use generated DI ext method

---

## Decisions

- Module is a plain C# class — no ComponentBase, zero Blazor in Core
- Up to 3 files per analyzer: Analyzer.cs, Guide.razor, Statistics.razor
- Guide.razor is mandatory per hero, manually composed (like WoWAnalyzer Guide.tsx)
- Statistics tab auto-collects from modules with StatisticsComponentType set
- Statistics components receive their analyzer via CascadingValue (not DynamicComponent Parameters)
- Guide/stats components access parser via @inject (DI), not CascadingParameter
- Normalizers are standalone IEventNormalizer classes, not Module subclasses
- Source generator processes [AddModule<T>] + [AddNormalizer<T>] to generate typed properties, DI registration, and assignment logic
- Module dependencies: required via constructor DI, optional via parser's typed nullable properties
- Existing FellowshipAnalyzer code is WIP — all current patterns may be replaced
- Source generator property naming: strip "Analyzer" suffix from class name (e.g. `BasicStComboAnalyzer` → `BasicStCombo`, `WinterOrbTracker` → `WinterOrbTracker`, `Abilities` → `Abilities`)
- Statistics are always rendered individually (one CascadingValue per DynamicComponent) — cascading as base `Module` type is sufficient, no keyed cascading needed
- Module priority is controlled by `[AddModule]` declaration order; explicit `[AddModule<T>(Priority = N)]` deferred until needed
