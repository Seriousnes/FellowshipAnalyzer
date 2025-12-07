# FellowshipAnalyzer Architecture Overview

A C# architectural reference based on WoWAnalyzer's TypeScript/React patterns, optimized for Blazor Hybrid (Server + WASM).

---

## Table of Contents

1. [High-Level Architecture](#high-level-architecture)
2. [Core Abstractions](#core-abstractions)
3. [Event System](#event-system)
4. [Module System](#module-system)
5. [Dependency Injection](#dependency-injection)
6. [Event Processing Pipeline](#event-processing-pipeline)
7. [Spec/Class Configuration](#specclass-configuration)
8. [C# Performance Optimizations](#c-performance-optimizations)
9. [Blazor Integration](#blazor-integration)
10. [Authentication & Authorization](#authentication--authorization)
11. [Implementation Roadmap](#implementation-roadmap)

---

## High-Level Architecture

### WoWAnalyzer Flow (Reference)

```
WarcraftLogs API → Report Data → CombatLogParser → Normalizers → EventEmitter → Analyzers → UI Components
```

### FellowshipAnalyzer Flow (Target)

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              BLAZOR SERVER                                       │
│  ┌─────────────────┐    ┌──────────────────┐    ┌─────────────────────────────┐ │
│  │ OAuth2 Handler  │───▶│ FellowshipLogs   │───▶│ Report Cache / Data Store   │ │
│  │                 │    │ GraphQL Client   │    │                             │ │
│  └─────────────────┘    └──────────────────┘    └─────────────────────────────┘ │
└────────────────────────────────────────┬────────────────────────────────────────┘
                                         │ Serialized Events (JSON/MessagePack)
                                         ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              BLAZOR WASM (CLIENT)                                │
│  ┌─────────────────────────────────────────────────────────────────────────────┐│
│  │                        CombatLogParser                                      ││
│  │  ┌─────────────┐   ┌──────────────┐   ┌────────────┐   ┌────────────────┐  ││
│  │  │ Normalizers │──▶│ EventEmitter │──▶│ Analyzers  │──▶│ ParseResults   │  ││
│  │  │ (reorder,   │   │ (dispatch)   │   │ (process)  │   │ (statistics,   │  ││
│  │  │  fabricate) │   │              │   │            │   │  suggestions)  │  ││
│  │  └─────────────┘   └──────────────┘   └────────────┘   └────────────────┘  ││
│  └─────────────────────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────────────────────┐│
│  │                        Blazor Components (UI)                               ││
│  │  Statistics | Timeline | Suggestions | Cast Efficiency | Guide             ││
│  └─────────────────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## Core Abstractions

### Class Hierarchy (WoWAnalyzer → FellowshipAnalyzer)

```
WoWAnalyzer (TypeScript)              FellowshipAnalyzer (C#)
─────────────────────────             ──────────────────────────
Module                          →     Module (abstract class)
  └── EventSubscriber           →       └── EventSubscriber
        └── Analyzer            →             └── Analyzer
              └── [SpecAnalyzer]→                   └── [SpecAnalyzer]

EventsNormalizer                →     EventsNormalizer (abstract)
  └── EventLinkNormalizer       →       └── EventLinkNormalizer
```

### Module Base Class

**WoWAnalyzer (Module.ts)**
```typescript
class Module {
  static dependencies: Record<string, typeof Module> = {};
  protected readonly owner!: CombatLogParser;
  active = true;
  priority = 0;
  key!: string;
}
```

**FellowshipAnalyzer (C#)**
```csharp
public abstract class Module
{
    /// <summary>
    /// Declares dependencies that will be injected via DI.
    /// Override in derived classes to declare required modules.
    /// </summary>
    public static IReadOnlyDictionary<string, Type> Dependencies { get; } 
        = new Dictionary<string, Type>();

    protected CombatLogParser Owner { get; }
    
    /// <summary>
    /// Whether this module should process events.
    /// Set to false to disable based on talents/items/etc.
    /// </summary>
    public bool Active { get; set; } = true;
    
    /// <summary>
    /// Execution priority - lower values execute first.
    /// Automatically calculated based on dependency order.
    /// </summary>
    public int Priority { get; internal set; }
    
    /// <summary>
    /// Module identifier key for lookup.
    /// </summary>
    public string Key { get; internal set; } = string.Empty;

    protected Combatant SelectedCombatant => Owner.SelectedCombatant;

    protected Module(ModuleOptions options)
    {
        Owner = options.Owner;
        Priority = options.Priority;
    }
}

public readonly record struct ModuleOptions(
    CombatLogParser Owner,
    int Priority
);
```

### EventSubscriber

**WoWAnalyzer (EventSubscriber.ts)**
```typescript
class EventSubscriber extends Module {
  addEventListener<ET extends EventType, E extends AnyEvent<ET>>(
    eventFilter: ET | EventFilter<ET>,
    listener: EventListener<ET, E>,
  ) {
    if (!this.active) return;
    this.owner.addEventListener(eventFilter, listener.bind(this), this);
  }
}
```

**FellowshipAnalyzer (C#)**
```csharp
public abstract class EventSubscriber : Module
{
    protected EventSubscriber(ModuleOptions options) : base(options) { }

    /// <summary>
    /// Subscribe to events of a specific type.
    /// </summary>
    protected void AddEventListener<TEvent>(Action<TEvent> listener) 
        where TEvent : ICombatEvent
    {
        if (!Active) return;
        Owner.AddEventListener(EventFilter<TEvent>.Create(), listener, this);
    }

    /// <summary>
    /// Subscribe with an event filter for more specific matching.
    /// </summary>
    protected void AddEventListener<TEvent>(
        EventFilter<TEvent> filter, 
        Action<TEvent> listener) 
        where TEvent : ICombatEvent
    {
        if (!Active) return;
        Owner.AddEventListener(filter, listener, this);
    }
}
```

### Analyzer

**FellowshipAnalyzer (C#)**
```csharp
public abstract class Analyzer : EventSubscriber
{
    protected Analyzer(ModuleOptions options) : base(options) { }

    /// <summary>
    /// Returns a statistic component for the overview/statistics page.
    /// </summary>
    public virtual RenderFragment? Statistic() => null;

    /// <summary>
    /// Returns suggestions based on analysis.
    /// </summary>
    public virtual IEnumerable<Suggestion> GetSuggestions(SuggestionContext when)
        => Enumerable.Empty<Suggestion>();

    /// <summary>
    /// Returns a custom tab for detailed analysis.
    /// </summary>
    public virtual ParseResultsTab? Tab() => null;
}

public record Suggestion(
    string Text,
    SuggestionImportance Importance,
    int? SpellId = null,
    string? Icon = null,
    string? Actual = null,
    string? Recommended = null
);

public enum SuggestionImportance { Major, Regular, Minor }
```

---

## Event System

### Event Types (Partial Mapping)

WoWAnalyzer defines events as interfaces with a discriminated union pattern. In C#, we use a base interface with concrete record structs.

```csharp
/// <summary>
/// Marker interface for all combat events.
/// </summary>
public interface ICombatEvent
{
    EventType Type { get; }
    long Timestamp { get; }
    bool Prepull { get; }
}

/// <summary>
/// Base event with common properties.
/// Using record struct for value semantics and memory efficiency.
/// </summary>
public readonly record struct BaseEvent(
    EventType Type,
    long Timestamp,
    bool Prepull = false
) : ICombatEvent;

/// <summary>
/// Events that have an associated ability.
/// </summary>
public interface IAbilityEvent : ICombatEvent
{
    Ability Ability { get; }
}

/// <summary>
/// Events that have a source entity.
/// </summary>
public interface ISourcedEvent : ICombatEvent
{
    int SourceId { get; }
    int? SourceInstance { get; }
    bool SourceIsFriendly { get; }
}

/// <summary>
/// Events that have a target entity.
/// </summary>
public interface ITargetedEvent : ICombatEvent
{
    int TargetId { get; }
    int? TargetInstance { get; }
    bool TargetIsFriendly { get; }
}

public readonly record struct Ability(
    string Name,
    int Guid,
    int Type,  // Magic school
    string AbilityIcon
);
```

### Concrete Event Types (Examples)

```csharp
public readonly record struct CastEvent(
    long Timestamp,
    Ability Ability,
    int SourceId,
    int? SourceInstance,
    bool SourceIsFriendly,
    int? TargetId,
    int? TargetInstance,
    bool TargetIsFriendly,
    ImmutableArray<ClassResource>? ClassResources,
    int? HitPoints,
    int? MaxHitPoints,
    bool Prepull = false
) : IAbilityEvent, ISourcedEvent, ITargetedEvent
{
    public EventType Type => EventType.Cast;
}

public readonly record struct DamageEvent(
    long Timestamp,
    Ability Ability,
    int? SourceId,
    int? SourceInstance,
    bool SourceIsFriendly,
    int TargetId,
    int TargetInstance,
    bool TargetIsFriendly,
    int HitType,
    long Amount,
    long? Absorbed,
    long? Mitigated,
    long? Overkill,
    bool Tick,
    bool Prepull = false
) : IAbilityEvent, ITargetedEvent
{
    public EventType Type => EventType.Damage;
}

public readonly record struct HealEvent(
    long Timestamp,
    Ability Ability,
    int SourceId,
    int? SourceInstance,
    bool SourceIsFriendly,
    int TargetId,
    int? TargetInstance,
    bool TargetIsFriendly,
    int HitType,
    long Amount,
    long? Overheal,
    long? Absorbed,
    bool Tick,
    int HitPoints,
    int MaxHitPoints,
    bool Prepull = false
) : IAbilityEvent, ISourcedEvent, ITargetedEvent
{
    public EventType Type => EventType.Heal;
}
```

### Event Extensions (Pattern Matching Helpers)

```csharp
public static class EventExtensions
{
    /// <summary>
    /// Check if event has an ability with pattern matching.
    /// </summary>
    public static bool HasAbility(this ICombatEvent e, out Ability ability)
    {
        if (e is IAbilityEvent ae)
        {
            ability = ae.Ability;
            return true;
        }
        ability = default;
        return false;
    }

    /// <summary>
    /// Check if event has a source.
    /// </summary>
    public static bool HasSource(this ICombatEvent e, out int sourceId)
    {
        if (e is ISourcedEvent se)
        {
            sourceId = se.SourceId;
            return true;
        }
        sourceId = default;
        return false;
    }

    /// <summary>
    /// Check if event targets a specific entity.
    /// </summary>
    public static bool HasTarget(this ICombatEvent e, out int targetId)
    {
        if (e is ITargetedEvent te)
        {
            targetId = te.TargetId;
            return true;
        }
        targetId = default;
        return false;
    }
}
```

### Event Linking

WoWAnalyzer uses `_linkedEvents` to associate related events. In C#, we can use a more efficient approach:

```csharp
/// <summary>
/// Stores relationships between events.
/// Uses indices for memory efficiency.
/// </summary>
public sealed class EventLinks
{
    private readonly Dictionary<int, List<LinkedEvent>> _links = new();

    public void AddLink(int eventIndex, string relation, int relatedEventIndex)
    {
        if (!_links.TryGetValue(eventIndex, out var list))
        {
            list = new List<LinkedEvent>(4); // Most events have few links
            _links[eventIndex] = list;
        }
        list.Add(new LinkedEvent(relation, relatedEventIndex));
    }

    public IEnumerable<int> GetRelatedEventIndices(int eventIndex, string relation)
    {
        if (_links.TryGetValue(eventIndex, out var list))
        {
            foreach (var link in list)
            {
                if (link.Relation == relation)
                    yield return link.RelatedEventIndex;
            }
        }
    }

    private readonly record struct LinkedEvent(string Relation, int RelatedEventIndex);
}
```

---

## Module System

### Module Registration

**WoWAnalyzer Pattern**
```typescript
class CombatLogParser {
  static internalModules: DependenciesDefinition = { ... };
  static defaultModules: DependenciesDefinition = { ... };
  static specModules: DependenciesDefinition = { ... };
}
```

**FellowshipAnalyzer Pattern**

Uses Microsoft DI extension methods for familiar, contributor-friendly registration:

```csharp
// Registration via extension methods
public static IServiceCollection AddFellowshipAnalyzer(this IServiceCollection services)
{
    // See Dependency Injection section for full implementation
    services.AddScoped<CombatLogParser>();
    services.AddModule<Combatants>();
    services.AddModule<DamageDone>();
    // ...
    return services;
}

// Spec registration
public static IServiceCollection AddFireMage(this IServiceCollection services)
{
    services.AddModule<CombustionAnalyzer>();
    services.AddSpec<FireMageCombatLogParser, FireMageConfig>();
    return services;
}
```

See [Dependency Injection](#dependency-injection) for complete implementation details.

---

## Dependency Injection

### Design Goals

1. **Use Microsoft.Extensions.DependencyInjection** - Native Blazor support, familiar to contributors
2. **Maintain WoWAnalyzer's module flexibility** - Active state toggling, priority ordering
3. **Support per-fight scoping** - Each fight analysis gets fresh module instances
4. **Easy for contributors** - Simple constructor injection, no magic

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        IServiceProvider (Root)                               │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │  Singleton Services                                                     ││
│  │  • IFellowshipLogsClient (API client)                                   ││
│  │  • ISpellDatabase (static game data)                                    ││
│  │  • IAuthStateProvider (user auth state)                                 ││
│  │  • SpecConfigRegistry (all spec configurations)                         ││
│  └─────────────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                    CreateScope() per fight analysis
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      IServiceScope (Per-Fight)                               │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │  Scoped Services                                                        ││
│  │  • AnalysisContext (fight, player, report metadata)                     ││
│  │  • CombatLogParser (orchestrates analysis)                              ││
│  │  • EventEmitter                                                         ││
│  │  • All Modules/Analyzers (one instance per fight)                       ││
│  └─────────────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────────────┘
```

### Module Registration

Modules are registered via extension methods, making it easy to add new specs:

```csharp
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers core analysis infrastructure.
    /// </summary>
    public static IServiceCollection AddFellowshipAnalyzer(this IServiceCollection services)
    {
        // Core services (singleton)
        services.AddSingleton<ISpellDatabase, SpellDatabase>();
        services.AddSingleton<SpecConfigRegistry>();
        
        // Per-fight scoped services
        services.AddScoped<AnalysisContext>();
        services.AddScoped<CombatLogParser>();
        services.AddScoped<EventEmitter>();
        
        // Register all core modules
        services.AddModule<Combatants>();
        services.AddModule<Enemies>();
        services.AddModule<AbilityTracker>();
        services.AddModule<DamageDone>();
        services.AddModule<HealingDone>();
        services.AddModule<DamageTaken>();
        services.AddModule<DeathTracker>();
        services.AddModule<StatTracker>();
        services.AddModule<Haste>();
        services.AddModule<CastEfficiency>();
        services.AddModule<AlwaysBeCasting>();
        
        // Register normalizers
        services.AddNormalizer<FightEndNormalizer>();
        services.AddNormalizer<ApplyBuffNormalizer>();
        services.AddNormalizer<CancelledCastsNormalizer>();
        
        return services;
    }

    /// <summary>
    /// Registers a spec-specific module set.
    /// </summary>
    public static IServiceCollection AddSpec<TParser, TConfig>(this IServiceCollection services)
        where TParser : CombatLogParser
        where TConfig : SpecConfig, new()
    {
        var config = new TConfig();
        services.AddSingleton(config);
        
        // Register the spec's parser as a keyed service
        services.AddKeyedScoped<CombatLogParser, TParser>(config.Spec.Id);
        
        return services;
    }

    /// <summary>
    /// Registers a module with automatic dependency resolution.
    /// </summary>
    public static IServiceCollection AddModule<TModule>(this IServiceCollection services)
        where TModule : Module
    {
        services.AddScoped<TModule>();
        services.AddScoped<Module, TModule>(sp => sp.GetRequiredService<TModule>());
        return services;
    }

    /// <summary>
    /// Registers an event normalizer.
    /// </summary>
    public static IServiceCollection AddNormalizer<TNormalizer>(this IServiceCollection services)
        where TNormalizer : EventsNormalizer
    {
        services.AddScoped<TNormalizer>();
        services.AddScoped<EventsNormalizer, TNormalizer>(sp => sp.GetRequiredService<TNormalizer>());
        return services;
    }
}
```

### Module Base Class (Updated for MS DI)

```csharp
public abstract class Module
{
    /// <summary>
    /// The analysis context for the current fight.
    /// </summary>
    protected AnalysisContext Context { get; }
    
    /// <summary>
    /// Whether this module is active for the current analysis.
    /// Override to disable based on talents, items, etc.
    /// </summary>
    public virtual bool Active => true;
    
    /// <summary>
    /// Execution priority - lower values execute first.
    /// Default is 100; normalizers should use lower values.
    /// </summary>
    public virtual int Priority => 100;

    protected Combatant SelectedCombatant => Context.SelectedCombatant;
    protected long FightDuration => Context.FightDuration;
    protected long CurrentTimestamp => Context.CurrentTimestamp;

    protected Module(AnalysisContext context)
    {
        Context = context;
    }
}

/// <summary>
/// Shared context for a single fight analysis.
/// Scoped service - one instance per fight.
/// </summary>
public sealed class AnalysisContext
{
    public Report Report { get; private set; } = null!;
    public Fight Fight { get; private set; } = null!;
    public PlayerInfo SelectedPlayer { get; private set; } = null!;
    public Combatant SelectedCombatant { get; internal set; } = null!;
    
    public long CurrentTimestamp { get; internal set; }
    public long FightDuration => CurrentTimestamp - Fight.StartTime;
    public bool IsFinished { get; internal set; }

    /// <summary>
    /// Initialize the context for a specific fight/player.
    /// Called by CombatLogParser before analysis begins.
    /// </summary>
    public void Initialize(Report report, Fight fight, PlayerInfo player)
    {
        Report = report;
        Fight = fight;
        SelectedPlayer = player;
        CurrentTimestamp = fight.StartTime;
    }
}
```

### Analyzer with Constructor Injection

Contributors use familiar constructor injection patterns:

```csharp
public class MyAnalyzer : Analyzer
{
    private readonly Combatants _combatants;
    private readonly AbilityTracker _abilityTracker;
    private readonly ISpellDatabase _spellDb;

    public MyAnalyzer(
        AnalysisContext context,
        EventEmitter emitter,
        Combatants combatants,
        AbilityTracker abilityTracker,
        ISpellDatabase spellDb) 
        : base(context, emitter)
    {
        _combatants = combatants;
        _abilityTracker = abilityTracker;
        _spellDb = spellDb;

        // Subscribe to events
        AddEventListener<CastEvent>(OnCast);
        AddEventListener(
            EventFilter<DamageEvent>.Create()
                .By(PlayerFilter.SelectedPlayer)
                .Spell(SPELLS.Fireball, SPELLS.Frostbolt),
            OnDamageSpell);
    }

    /// <summary>
    /// Disable this analyzer if the player doesn't have the required talent.
    /// </summary>
    public override bool Active => SelectedCombatant.HasTalent(TALENTS.SomeTalent);

    private void OnCast(CastEvent e)
    {
        // Analysis logic using injected dependencies
        var casterInfo = _combatants.GetCombatant(e.SourceId);
    }

    private void OnDamageSpell(DamageEvent e)
    {
        var spellInfo = _spellDb.GetSpell(e.Ability.Guid);
    }

    public override RenderFragment? Statistic() => builder =>
    {
        // Render statistics
    };
}
```

### CombatLogParser as Orchestrator

```csharp
public class CombatLogParser
{
    private readonly AnalysisContext _context;
    private readonly EventEmitter _emitter;
    private readonly IEnumerable<Module> _modules;
    private readonly IEnumerable<EventsNormalizer> _normalizers;
    private readonly ILogger<CombatLogParser> _logger;

    public CombatLogParser(
        AnalysisContext context,
        EventEmitter emitter,
        IEnumerable<Module> modules,
        IEnumerable<EventsNormalizer> normalizers,
        ILogger<CombatLogParser> logger)
    {
        _context = context;
        _emitter = emitter;
        _modules = modules.OrderBy(m => m.Priority).ToList();
        _normalizers = normalizers.OrderBy(n => n.Priority).ToList();
        _logger = logger;
    }

    public Combatant SelectedCombatant => _context.SelectedCombatant;

    public async Task<ParseResults> AnalyzeAsync(
        Report report, 
        Fight fight, 
        PlayerInfo player,
        IReadOnlyList<ICombatEvent> events,
        CancellationToken ct = default)
    {
        _context.Initialize(report, fight, player);

        // 1. Run normalizers
        var normalizedEvents = await NormalizeEventsAsync(events, ct);

        // 2. Dispatch events to analyzers
        foreach (var evt in normalizedEvents)
        {
            ct.ThrowIfCancellationRequested();
            _emitter.TriggerEvent(evt);
        }

        _context.IsFinished = true;

        // 3. Collect results
        return BuildResults();
    }

    private async Task<IReadOnlyList<ICombatEvent>> NormalizeEventsAsync(
        IReadOnlyList<ICombatEvent> events,
        CancellationToken ct)
    {
        var result = events.ToList();
        
        foreach (var normalizer in _normalizers.Where(n => n.Active))
        {
            ct.ThrowIfCancellationRequested();
            result = normalizer.Normalize(result);
        }

        return result;
    }

    private ParseResults BuildResults()
    {
        var analyzers = _modules.OfType<Analyzer>().Where(a => a.Active);
        
        return new ParseResults
        {
            Statistics = analyzers
                .Select(a => a.Statistic())
                .Where(s => s != null)
                .ToList()!,
            Suggestions = analyzers
                .SelectMany(a => a.GetSuggestions(new SuggestionContext()))
                .OrderByDescending(s => s.Importance)
                .ToList(),
            Tabs = analyzers
                .Select(a => a.Tab())
                .Where(t => t != null)
                .ToList()!,
        };
    }

    public T GetModule<T>() where T : Module
        => _modules.OfType<T>().First();

    public T? GetOptionalModule<T>() where T : Module
        => _modules.OfType<T>().FirstOrDefault();
}
```

### Spec-Specific Registration

Each spec extends the base registration:

```csharp
// In FellowshipAnalyzer.Heroes.Rime
public static class RimeServiceExtensions
{
    public static IServiceCollection AddRime(this IServiceCollection services)
    {
        // Hero-specific normalizers
        services.AddNormalizer<WrathOfWinterNormalizer>();        
        
        // Cooldown analyzers
        services.AddModule<WrathOfWinterAnalyzer>();
        services.AddModule<IceBlitzAnalyzer>();
        services.AddModule<FlightOfTheNavirAnalyzer>();
        // Spell-usage analyzers
        services.AddModule<IceCometAnalyzer>();
        services.AddModule<GlacialBlastAnalyzer>();                
        
        // Register the spec config
        services.AddSpec<RimeCombatLogParser, RimeConfig>();
        
        return services;
    }
}

// Program.cs / Startup
services.AddFellowshipAnalyzer()
    .AddRime()
    .AddVigour()
    .AddMeiko()
    // ... etc
```

### Analysis Factory for Per-Fight Scoping

```csharp
public interface IAnalysisFactory
{
    Task<ParseResults> AnalyzeFightAsync(
        string reportCode,
        int fightId,
        int playerId,
        CancellationToken ct = default);
}

public class AnalysisFactory : IAnalysisFactory
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFellowshipLogsClient _apiClient;
    private readonly SpecConfigRegistry _specRegistry;

    public AnalysisFactory(
        IServiceScopeFactory scopeFactory,
        IFellowshipLogsClient apiClient,
        SpecConfigRegistry specRegistry)
    {
        _scopeFactory = scopeFactory;
        _apiClient = apiClient;
        _specRegistry = specRegistry;
    }

    public async Task<ParseResults> AnalyzeFightAsync(
        string reportCode,
        int fightId,
        int playerId,
        CancellationToken ct = default)
    {
        // Fetch data from API
        var report = await _apiClient.GetReportAsync(reportCode, ct);
        var fight = report.Fights.First(f => f.Id == fightId);
        var player = report.Players.First(p => p.Id == playerId);
        var events = await _apiClient.GetEventsAsync(reportCode, fightId, ct);

        // Create a new scope for this analysis
        await using var scope = _scopeFactory.CreateAsyncScope();

        // Get the appropriate parser for this spec
        var specConfig = _specRegistry.GetConfig(player.SpecId);
        var parser = scope.ServiceProvider
            .GetRequiredKeyedService<CombatLogParser>(specConfig.Spec.Id);

        // Run analysis
        return await parser.AnalyzeAsync(report, fight, player, events, ct);
    }
}
```

---

## Event Processing Pipeline

### Pipeline Overview

```
Raw Events (from API)
    │
    ▼
┌─────────────────────────────────────────┐
│           NORMALIZATION PHASE           │
│  ┌───────────────────────────────────┐  │
│  │  1. FightEndNormalizer            │  │
│  │  2. ApplyBuffNormalizer           │  │
│  │  3. CancelledCastsNormalizer      │  │
│  │  4. PrePullCooldownsNormalizer    │  │
│  │  5. EventLinkNormalizers          │  │
│  │  6. Spec-specific Normalizers     │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
    │
    ▼
Normalized Events (reordered, linked, fabricated)
    │
    ▼
┌─────────────────────────────────────────┐
│            DISPATCH PHASE               │
│  EventEmitter iterates events and       │
│  dispatches to registered listeners     │
│  in priority order                      │
└─────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────┐
│            ANALYSIS PHASE               │
│  Each Analyzer processes events via     │
│  its subscribed listeners, building     │
│  up statistics and tracking state       │
└─────────────────────────────────────────┘
    │
    ▼
ParseResults (Statistics, Suggestions, Tabs)
```

### EventEmitter

```csharp
public sealed class EventEmitter : Module
{
    private readonly Dictionary<EventType, List<BoundListener>> _listenersByType = new();
    private readonly List<BoundListener> _catchAllListeners = new();

    public int NumTriggeredEvents { get; private set; }
    public int NumListenersCalled { get; private set; }

    public EventEmitter(ModuleOptions options) : base(options)
    {
        // Initialize listener lists for all event types
        foreach (EventType type in Enum.GetValues<EventType>())
        {
            _listenersByType[type] = new List<BoundListener>();
        }
    }

    public void AddEventListener<TEvent>(
        EventFilter<TEvent> filter,
        Action<TEvent> listener,
        Module module) where TEvent : ICombatEvent
    {
        var boundListener = new BoundListener(
            filter.EventType,
            CreateCompiledListener(filter, listener, module),
            module
        );

        _listenersByType[filter.EventType].Add(boundListener);
        
        // Sort by priority
        _listenersByType[filter.EventType]
            .Sort((a, b) => a.Module.Priority.CompareTo(b.Module.Priority));
    }

    public void TriggerEvent<TEvent>(in TEvent @event) where TEvent : ICombatEvent
    {
        NumTriggeredEvents++;

        // Update timestamp tracking
        Owner.CurrentTimestamp = @event.Timestamp;

        // Call type-specific listeners
        if (_listenersByType.TryGetValue(@event.Type, out var listeners))
        {
            foreach (var listener in listeners)
            {
                if (!listener.Module.Active) continue;
                
                NumListenersCalled++;
                try
                {
                    listener.Invoke(@event);
                }
                catch (Exception ex)
                {
                    HandleListenerError(ex, listener.Module);
                }
            }
        }
    }

    private Action<ICombatEvent> CreateCompiledListener<TEvent>(
        EventFilter<TEvent> filter,
        Action<TEvent> listener,
        Module module) where TEvent : ICombatEvent
    {
        // Compile filter checks into the listener for performance
        return (ICombatEvent e) =>
        {
            if (e is not TEvent typed) return;
            if (!filter.Matches(typed, Owner)) return;
            listener(typed);
        };
    }

    private readonly record struct BoundListener(
        EventType EventType,
        Action<ICombatEvent> Invoke,
        Module Module
    );
}
```

### EventFilter

```csharp
public sealed class EventFilter<TEvent> where TEvent : ICombatEvent
{
    public EventType EventType { get; }
    
    private PlayerFilter? _by;
    private PlayerFilter? _to;
    private SpellFilter? _spell;

    private EventFilter(EventType eventType)
    {
        EventType = eventType;
    }

    public static EventFilter<TEvent> Create()
    {
        var eventType = GetEventTypeForGeneric();
        return new EventFilter<TEvent>(eventType);
    }

    /// <summary>
    /// Filter to events caused by the selected player or their pets.
    /// </summary>
    public EventFilter<TEvent> By(PlayerFilter filter)
    {
        _by = filter;
        return this;
    }

    /// <summary>
    /// Filter to events targeting the selected player or their pets.
    /// </summary>
    public EventFilter<TEvent> To(PlayerFilter filter)
    {
        _to = filter;
        return this;
    }

    /// <summary>
    /// Filter to events for specific spell(s).
    /// </summary>
    public EventFilter<TEvent> Spell(params int[] spellIds)
    {
        _spell = new SpellFilter(spellIds);
        return this;
    }

    public bool Matches(TEvent @event, CombatLogParser owner)
    {
        if (_by.HasValue && !CheckBy(@event, owner, _by.Value))
            return false;
        if (_to.HasValue && !CheckTo(@event, owner, _to.Value))
            return false;
        if (_spell.HasValue && !CheckSpell(@event, _spell.Value))
            return false;
        return true;
    }

    private static bool CheckBy(TEvent e, CombatLogParser owner, PlayerFilter filter)
    {
        if (e is not ISourcedEvent sourced) return false;
        
        return filter switch
        {
            PlayerFilter.SelectedPlayer => owner.ByPlayer(sourced),
            PlayerFilter.SelectedPlayerPet => owner.ByPlayerPet(sourced),
            PlayerFilter.SelectedPlayerOrPet => owner.ByPlayer(sourced) || owner.ByPlayerPet(sourced),
            _ => false
        };
    }

    private static bool CheckTo(TEvent e, CombatLogParser owner, PlayerFilter filter)
    {
        if (e is not ITargetedEvent targeted) return false;
        
        return filter switch
        {
            PlayerFilter.SelectedPlayer => owner.ToPlayer(targeted),
            PlayerFilter.SelectedPlayerPet => owner.ToPlayerPet(targeted),
            PlayerFilter.SelectedPlayerOrPet => owner.ToPlayer(targeted) || owner.ToPlayerPet(targeted),
            _ => false
        };
    }

    private static bool CheckSpell(TEvent e, SpellFilter filter)
    {
        if (e is not IAbilityEvent ability) return false;
        return filter.SpellIds.Contains(ability.Ability.Guid);
    }

    private static EventType GetEventTypeForGeneric()
    {
        // Use reflection or a dictionary mapping to get EventType from TEvent
        // This could be optimized with source generation
        return typeof(TEvent).Name switch
        {
            nameof(CastEvent) => EventType.Cast,
            nameof(DamageEvent) => EventType.Damage,
            nameof(HealEvent) => EventType.Heal,
            nameof(ApplyBuffEvent) => EventType.ApplyBuff,
            // ... etc
            _ => throw new NotSupportedException($"Unknown event type: {typeof(TEvent).Name}")
        };
    }
}

[Flags]
public enum PlayerFilter
{
    SelectedPlayer = 1,
    SelectedPlayerPet = 2,
    SelectedPlayerOrPet = SelectedPlayer | SelectedPlayerPet
}

public readonly record struct SpellFilter(ImmutableHashSet<int> SpellIds)
{
    public SpellFilter(params int[] ids) : this(ids.ToImmutableHashSet()) { }
}
```

---

## Spec/Class Configuration

### Config Pattern

**WoWAnalyzer**
```typescript
const Config: Config = {
  contributors: [CONTRIBUTORS.Abelito75],
  patchCompatibility: '11.0.5',
  spec: SPECS.PRESERVATION_EVOKER,
  exampleReport: '/report/...',
  // ...
};
```

**FellowshipAnalyzer**
```csharp
public abstract record SpecConfig
{
    public required GameSpec Spec { get; init; }
    public required IReadOnlyList<Contributor> Contributors { get; init; }
    public required string PatchCompatibility { get; init; }
    public required string ExampleReport { get; init; }
    public SupportLevel SupportLevel { get; init; } = SupportLevel.Foundation;
    
    /// <summary>
    /// Factory method to create the spec-specific CombatLogParser.
    /// </summary>
    public abstract CombatLogParser CreateParser(ParserContext context);
}

public record PreservationHealerConfig : SpecConfig
{
    public override CombatLogParser CreateParser(ParserContext context)
        => new PreservationHealerCombatLogParser(context, this);
}

public enum SupportLevel
{
    Unmaintained,
    Foundation,
    MaintainedPartial,
    MaintainedFull
}
```

### Spec CombatLogParser

```csharp
public class PreservationHealerCombatLogParser : CombatLogParser
{
    protected override IEnumerable<ModuleDescriptor> SpecModules => new[]
    {
        // Normalizers
        ModuleDescriptor.Create<HotApplicationNormalizer>("hotApplicationNormalizer"),
        ModuleDescriptor.Create<CastLinkNormalizer>("castLinkNormalizer"),
        
        // Core
        ModuleDescriptor.Create<HotTracker>("hotTracker"),
        ModuleDescriptor.Create<MasteryEffectiveness>("masteryEffectiveness"),
        
        // Talents
        ModuleDescriptor.Create<DreamBreathAnalyzer>("dreamBreath"),
        ModuleDescriptor.Create<SpiritbloomAnalyzer>("spiritbloom"),
        ModuleDescriptor.Create<EssenceBurstAnalyzer>("essenceBurst"),
        
        // Resources
        ModuleDescriptor.Create<EssenceTracker>("essenceTracker"),
        ModuleDescriptor.Create<ManaTracker>("manaTracker"),
    };

    public PreservationHealerCombatLogParser(ParserContext context, SpecConfig config) 
        : base(context, config)
    {
    }
}
```

---

## C# Performance Optimizations

### 1. Struct Events with `readonly record struct`

Events are immutable value types, avoiding heap allocations during processing:

```csharp
// Each event is a value type - no heap allocation when processing
public readonly record struct CastEvent(...) : ICombatEvent;
```

### 2. `Span<T>` for Event Processing

Process events without allocating new arrays:

```csharp
public abstract class EventsNormalizer : Module
{
    /// <summary>
    /// Normalize events in-place where possible.
    /// </summary>
    public abstract void Normalize(Span<ICombatEvent> events);
    
    /// <summary>
    /// When events need to be added/removed, return a new list.
    /// </summary>
    public virtual List<ICombatEvent>? NormalizeWithMutation(List<ICombatEvent> events) 
        => null;
}
```

### 3. Object Pooling for Temporary Collections

```csharp
public static class ListPool<T>
{
    private static readonly ObjectPool<List<T>> Pool = 
        new DefaultObjectPool<List<T>>(new ListPolicy<T>());

    public static List<T> Rent() => Pool.Get();
    public static void Return(List<T> list) { list.Clear(); Pool.Return(list); }
}

// Usage in analyzers
using var _ = ListPool<CastEvent>.RentDisposable(out var casts);
// ... populate and use casts
// automatically returned when scope ends
```

### 4. `ref struct` for Short-Lived Event Contexts

```csharp
/// <summary>
/// Context passed to event handlers - stack allocated, no GC pressure.
/// </summary>
public ref struct EventContext
{
    public readonly ref readonly ICombatEvent Event;
    public readonly CombatLogParser Parser;
    public readonly long FightDuration;
    
    public EventContext(ref readonly ICombatEvent @event, CombatLogParser parser)
    {
        Event = ref @event;
        Parser = parser;
        FightDuration = parser.FightDuration;
    }
}
```

### 5. Source Generators for Event Registration

Use source generators to eliminate reflection overhead:

```csharp
// This attribute triggers source generation
[GenerateEventSubscriptions]
public partial class MyAnalyzer : Analyzer
{
    [OnEvent(EventType.Cast)]
    [ByPlayer]
    [Spell(SPELLS.FIREBALL, SPELLS.FROSTBOLT)]
    private void OnDamageSpellCast(in CastEvent e)
    {
        // Generated code handles filtering
    }
}

// Generated code:
public partial class MyAnalyzer
{
    protected override void RegisterEventListeners()
    {
        AddEventListener(
            EventFilter<CastEvent>.Create()
                .By(PlayerFilter.SelectedPlayer)
                .Spell(SPELLS.FIREBALL, SPELLS.FROSTBOLT),
            e => OnDamageSpellCast(in e)
        );
    }
}
```

### 6. Frozen Collections for Static Data

```csharp
public static class SPELLS
{
    // Spell definitions as frozen dictionary for O(1) lookup
    public static readonly FrozenDictionary<int, Spell> ById = new Dictionary<int, Spell>
    {
        [12345] = new Spell(12345, "Fireball", "spell_fire_fireball"),
        [12346] = new Spell(12346, "Frostbolt", "spell_frost_frostbolt"),
        // ...
    }.ToFrozenDictionary();
}
```

---

## Blazor Integration

### Component Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      ReportPage.razor                           │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  CascadingValue: CombatLogParser                          │  │
│  │  ┌─────────────────────────────────────────────────────┐  │  │
│  │  │  TabContainer                                       │  │  │
│  │  │  ├── OverviewTab                                    │  │  │
│  │  │  │   ├── StatisticsSection (from Analyzer.Statistic)│  │  │
│  │  │  │   └── SuggestionsSection                         │  │  │
│  │  │  ├── TimelineTab                                    │  │  │
│  │  │  ├── CastEfficiencyTab                              │  │  │
│  │  │  └── [Custom Analyzer Tabs]                         │  │  │
│  │  └─────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### RenderFragment from Analyzers

```csharp
public class DamageDone : Analyzer
{
    private long _totalDamage;
    private readonly Dictionary<int, long> _damageBySpell = new();

    public DamageDone(ModuleOptions options) : base(options)
    {
        AddEventListener(
            EventFilter<DamageEvent>.Create().By(PlayerFilter.SelectedPlayer),
            OnDamage);
    }

    private void OnDamage(DamageEvent e)
    {
        _totalDamage += e.Amount;
        _damageBySpell.TryAdd(e.Ability.Guid, 0);
        _damageBySpell[e.Ability.Guid] += e.Amount;
    }

    public override RenderFragment? Statistic() => builder =>
    {
        builder.OpenComponent<StatisticBox>(0);
        builder.AddAttribute(1, "Title", "Damage Done");
        builder.AddAttribute(2, "Value", FormatNumber(_totalDamage));
        builder.AddAttribute(3, "Icon", "ability_warrior_devastate");
        builder.CloseComponent();
    };
}
```

### State Management

```csharp
public class AnalysisState
{
    public Report? Report { get; private set; }
    public Fight? SelectedFight { get; private set; }
    public PlayerInfo? SelectedPlayer { get; private set; }
    public CombatLogParser? Parser { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }

    public event Action? OnStateChanged;

    public async Task LoadReportAsync(string reportCode)
    {
        IsLoading = true;
        OnStateChanged?.Invoke();
        
        try
        {
            // Fetch from server (which proxies to FellowshipLogs API)
            Report = await _httpClient.GetFromJsonAsync<Report>($"api/report/{reportCode}");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnStateChanged?.Invoke();
        }
    }

    public async Task AnalyzeFightAsync(int fightId, int playerId)
    {
        // ... create parser, run analysis
    }
}
```

---

## Authentication & Authorization

### Overview

FellowshipAnalyzer has two distinct authentication contexts:

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           AUTHENTICATION FLOWS                                   │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                  │
│  1. SERVER → FELLOWSHIPLOGS API (Machine-to-Machine)                            │
│     ┌────────────┐    Client Credentials    ┌──────────────────┐                │
│     │  Blazor    │ ───────────────────────▶ │  FellowshipLogs  │                │
│     │  Server    │ ◀─────────────────────── │  OAuth2 Server   │                │
│     └────────────┘    Access Token          └──────────────────┘                │
│           │                                                                      │
│           │ Fetch public reports, events, static data                           │
│           ▼                                                                      │
│     ┌──────────────────┐                                                        │
│     │ FellowshipLogs   │                                                        │
│     │ GraphQL API      │                                                        │
│     └──────────────────┘                                                        │
│                                                                                  │
│  2. USER → FELLOWSHIPLOGS (Authorization Code + PKCE)                           │
│     ┌────────────┐         ┌────────────┐         ┌──────────────────┐          │
│     │   User     │ ──────▶ │  Blazor    │ ──────▶ │  FellowshipLogs  │          │
│     │  Browser   │         │  Server    │         │  OAuth2 Server   │          │
│     └────────────┘         │   (BFF)    │         └──────────────────┘          │
│           ▲                └────────────┘                │                      │
│           │                      │                       │                      │
│           │    Auth Cookie       │    Tokens stored      │                      │
│           └──────────────────────┴───────────────────────┘                      │
│                                                                                  │
│     User-specific: own reports, characters, guild info, settings                │
│                                                                                  │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Why BFF (Backend-for-Frontend) Pattern?

1. **Security**: Tokens never exposed to browser JavaScript
2. **Token Refresh**: Server handles refresh seamlessly
3. **Simplified WASM**: Client just uses HTTP cookies
4. **CORS Avoidance**: All API calls go through your server

### Server Configuration

```csharp
// Program.cs (Blazor Server)
var builder = WebApplication.CreateBuilder(args);

// 1. Machine-to-Machine auth for FellowshipLogs API
builder.Services.AddFellowshipLogsClient(options =>
{
    options.ClientId = builder.Configuration["FellowshipLogs:ClientId"]!;
    options.ClientSecret = builder.Configuration["FellowshipLogs:ClientSecret"]!;
    options.TokenEndpoint = "https://www.fellowshiplogs.com/oauth/token";
    options.GraphQLEndpoint = "https://www.fellowshiplogs.com/api/v2/client";
});

// 2. User authentication via FellowshipLogs OAuth2
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "FellowshipLogs";
})
.AddCookie(options =>
{
    options.Cookie.Name = "FellowshipAnalyzer.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
})
.AddOAuth("FellowshipLogs", options =>
{
    options.ClientId = builder.Configuration["FellowshipLogs:UserAuth:ClientId"]!;
    options.ClientSecret = builder.Configuration["FellowshipLogs:UserAuth:ClientSecret"]!;
    options.AuthorizationEndpoint = "https://www.fellowshiplogs.com/oauth/authorize";
    options.TokenEndpoint = "https://www.fellowshiplogs.com/oauth/token";
    options.UserInformationEndpoint = "https://www.fellowshiplogs.com/api/v2/user";
    options.CallbackPath = "/signin-fellowshiplogs";
    
    options.SaveTokens = true; // Store tokens in auth cookie
    
    // Same scopes as WarcraftLogs (both managed by Archon.gg)
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("view-user-profile");
    options.Scope.Add("view-private-reports");
    
    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
    options.ClaimActions.MapJsonKey("avatar", "avatar");
    
    options.Events.OnCreatingTicket = async context =>
    {
        // Fetch user info
        var request = new HttpRequestMessage(HttpMethod.Get, options.UserInformationEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        
        var response = await context.Backchannel.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        context.RunClaimActions(user);
    };
});

builder.Services.AddAuthorization();

// Token management service
builder.Services.AddScoped<IUserTokenService, UserTokenService>();
```

### FellowshipLogs API Client

Simple HttpClient-based GraphQL client (same pattern as WarcraftLogs - both Archon.gg APIs):

```csharp
/// <summary>
/// Client for FellowshipLogs GraphQL API.
/// Handles machine-to-machine authentication automatically.
/// </summary>
public interface IFellowshipLogsClient
{
    Task<Report> GetReportAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<ICombatEvent>> GetEventsAsync(string code, int fightId, CancellationToken ct = default);
    Task<CharacterInfo> GetCharacterAsync(string name, string server, string region, CancellationToken ct = default);
}

/// <summary>
/// Client for user-specific FellowshipLogs API calls.
/// Uses the logged-in user's token.
/// </summary>
public interface IFellowshipLogsUserClient
{
    Task<IReadOnlyList<UserReport>> GetMyReportsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserCharacter>> GetMyCharactersAsync(CancellationToken ct = default);
    Task<UserGuild?> GetMyGuildAsync(CancellationToken ct = default);
}

/// <summary>
/// Simple HttpClient-based GraphQL implementation.
/// </summary>
public class FellowshipLogsClient : IFellowshipLogsClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<FellowshipLogsClientOptions> _options;
    private readonly ILogger<FellowshipLogsClient> _logger;

    public FellowshipLogsClient(
        HttpClient httpClient,
        IOptions<FellowshipLogsClientOptions> options,
        ILogger<FellowshipLogsClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(options.Value.GraphQLEndpoint);
    }

    public async Task<Report> GetReportAsync(string code, CancellationToken ct = default)
    {
        var query = """
            query GetReport($code: String!) {
                reportData {
                    report(code: $code) {
                        code
                        title
                        startTime
                        endTime
                        fights {
                            id
                            name
                            startTime
                            endTime
                            kill
                            difficulty
                            bossPercentage
                        }
                        masterData {
                            actors {
                                id
                                name
                                type
                                subType
                                server
                            }
                        }
                    }
                }
            }
            """;

        var result = await ExecuteQueryAsync<ReportResponse>(query, new { code }, ct);
        return result.ReportData.Report;
    }

    public async Task<IReadOnlyList<ICombatEvent>> GetEventsAsync(
        string code, 
        int fightId, 
        CancellationToken ct = default)
    {
        var query = """
            query GetEvents($code: String!, $fightId: Int!, $startTime: Float!, $endTime: Float!) {
                reportData {
                    report(code: $code) {
                        events(fightIDs: [$fightId], startTime: $startTime, endTime: $endTime) {
                            data
                            nextPageTimestamp
                        }
                    }
                }
            }
            """;

        // Paginate through all events
        var allEvents = new List<ICombatEvent>();
        float? nextPage = 0;

        while (nextPage.HasValue)
        {
            var result = await ExecuteQueryAsync<EventsResponse>(query, 
                new { code, fightId, startTime = nextPage.Value, endTime = float.MaxValue }, ct);
            
            var events = ParseEvents(result.ReportData.Report.Events.Data);
            allEvents.AddRange(events);
            
            nextPage = result.ReportData.Report.Events.NextPageTimestamp;
        }

        return allEvents;
    }

    private async Task<T> ExecuteQueryAsync<T>(
        string query, 
        object variables, 
        CancellationToken ct)
    {
        var request = new { query, variables };
        var response = await _httpClient.PostAsJsonAsync("", request, ct);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<T>>(ct);
        
        if (result?.Errors?.Any() == true)
        {
            throw new FellowshipLogsApiException(result.Errors);
        }
        
        return result!.Data;
    }

    private static IEnumerable<ICombatEvent> ParseEvents(JsonElement data)
    {
        // Event parsing logic - deserialize based on "type" field
        foreach (var element in data.EnumerateArray())
        {
            var type = element.GetProperty("type").GetString();
            yield return type switch
            {
                "cast" => element.Deserialize<CastEvent>()!,
                "damage" => element.Deserialize<DamageEvent>()!,
                "heal" => element.Deserialize<HealEvent>()!,
                "applybuff" => element.Deserialize<ApplyBuffEvent>()!,
                "removebuff" => element.Deserialize<RemoveBuffEvent>()!,
                // ... other event types
                _ => element.Deserialize<UnknownEvent>()!
            };
        }
    }
}

public record GraphQLResponse<T>(T Data, IReadOnlyList<GraphQLError>? Errors);
public record GraphQLError(string Message, IReadOnlyList<object>? Path);

public class FellowshipLogsClientOptions
{
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public required string TokenEndpoint { get; set; }
    public required string GraphQLEndpoint { get; set; }
}

public static class FellowshipLogsServiceExtensions
{
    public static IServiceCollection AddFellowshipLogsClient(
        this IServiceCollection services,
        Action<FellowshipLogsClientOptions> configure)
    {
        services.Configure(configure);
        
        // Machine-to-machine client with automatic token management
        services.AddHttpClient<IFellowshipLogsClient, FellowshipLogsClient>()
            .AddHttpMessageHandler<ClientCredentialsTokenHandler>();
        
        services.AddTransient<ClientCredentialsTokenHandler>();
        services.AddSingleton<IClientCredentialsTokenCache, ClientCredentialsTokenCache>();
        
        // User-context client
        services.AddHttpClient<IFellowshipLogsUserClient, FellowshipLogsUserClient>()
            .AddHttpMessageHandler<UserTokenHandler>();
        
        services.AddScoped<UserTokenHandler>();
        
        return services;
    }
}
```

### Client Credentials Token Handler

```csharp
/// <summary>
/// Automatically attaches machine-to-machine access token to requests.
/// </summary>
public class ClientCredentialsTokenHandler : DelegatingHandler
{
    private readonly IClientCredentialsTokenCache _tokenCache;
    private readonly IOptions<FellowshipLogsClientOptions> _options;

    public ClientCredentialsTokenHandler(
        IClientCredentialsTokenCache tokenCache,
        IOptions<FellowshipLogsClientOptions> options)
    {
        _tokenCache = tokenCache;
        _options = options;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        var token = await _tokenCache.GetTokenAsync(_options.Value, ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }
}

public interface IClientCredentialsTokenCache
{
    Task<string> GetTokenAsync(FellowshipLogsClientOptions options, CancellationToken ct = default);
}

public class ClientCredentialsTokenCache : IClientCredentialsTokenCache
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public ClientCredentialsTokenCache(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetTokenAsync(FellowshipLogsClientOptions options, CancellationToken ct)
    {
        // Return cached token if still valid (with 5 min buffer)
        if (_cachedToken != null && DateTimeOffset.UtcNow.AddMinutes(5) < _expiresAt)
        {
            return _cachedToken;
        }

        await _semaphore.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_cachedToken != null && DateTimeOffset.UtcNow.AddMinutes(5) < _expiresAt)
            {
                return _cachedToken;
            }

            // Fetch new token
            using var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync(options.TokenEndpoint, 
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                }), ct);

            response.EnsureSuccessStatusCode();
            
            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            
            _cachedToken = tokenResponse!.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            
            return _cachedToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string TokenType
    );
}
```

### User Token Handler

```csharp
/// <summary>
/// Attaches the logged-in user's access token to requests.
/// </summary>
public class UserTokenHandler : DelegatingHandler
{
    private readonly IUserTokenService _tokenService;

    public UserTokenHandler(IUserTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        var token = await _tokenService.GetAccessTokenAsync(ct);
        
        if (token == null)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }
}

public interface IUserTokenService
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
    Task<bool> RefreshTokenAsync(CancellationToken ct = default);
}

public class UserTokenService : IUserTokenService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptionsMonitor<OAuthOptions> _oauthOptions;

    public UserTokenService(
        IHttpContextAccessor httpContextAccessor,
        IOptionsMonitor<OAuthOptions> oauthOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _oauthOptions = oauthOptions;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var token = await context.GetTokenAsync("access_token");
        var expiresAt = await context.GetTokenAsync("expires_at");

        // Check if token needs refresh
        if (expiresAt != null && DateTimeOffset.Parse(expiresAt) < DateTimeOffset.UtcNow.AddMinutes(5))
        {
            if (await RefreshTokenAsync(ct))
            {
                token = await context.GetTokenAsync("access_token");
            }
        }

        return token;
    }

    public async Task<bool> RefreshTokenAsync(CancellationToken ct = default)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return false;

        var refreshToken = await context.GetTokenAsync("refresh_token");
        if (refreshToken == null) return false;

        var options = _oauthOptions.Get("FellowshipLogs");
        
        using var client = new HttpClient();
        var response = await client.PostAsync(options.TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
            }), ct);

        if (!response.IsSuccessStatusCode) return false;

        var tokenResponse = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        
        // Update the authentication ticket with new tokens
        var authenticateResult = await context.AuthenticateAsync();
        if (authenticateResult.Properties == null) return false;

        authenticateResult.Properties.UpdateTokenValue("access_token", 
            tokenResponse.GetProperty("access_token").GetString()!);
        authenticateResult.Properties.UpdateTokenValue("expires_at", 
            DateTimeOffset.UtcNow.AddSeconds(tokenResponse.GetProperty("expires_in").GetInt32()).ToString("o"));
        
        if (tokenResponse.TryGetProperty("refresh_token", out var newRefresh))
        {
            authenticateResult.Properties.UpdateTokenValue("refresh_token", newRefresh.GetString()!);
        }

        await context.SignInAsync(authenticateResult.Principal!, authenticateResult.Properties);
        return true;
    }
}
```

### Auth State for Blazor Components

```csharp
/// <summary>
/// Provides authentication state to Blazor components.
/// </summary>
public class FellowshipAuthStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FellowshipAuthStateProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        return Task.FromResult(new AuthenticationState(user));
    }
}

/// <summary>
/// User info available to Blazor components.
/// </summary>
public record FellowshipUser(
    string Id,
    string Name,
    string? AvatarUrl,
    bool IsAuthenticated
)
{
    public static FellowshipUser Anonymous => new("", "Anonymous", null, false);
    
    public static FellowshipUser FromClaimsPrincipal(ClaimsPrincipal principal)
    {
        if (!principal.Identity?.IsAuthenticated ?? true)
        {
            return Anonymous;
        }
        
        return new FellowshipUser(
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
            principal.FindFirstValue(ClaimTypes.Name) ?? "Unknown",
            principal.FindFirstValue("avatar"),
            true
        );
    }
}
```

### Blazor Components Usage

```razor
@* LoginButton.razor *@
@inject NavigationManager Navigation

<AuthorizeView>
    <Authorized>
        <div class="user-info">
            <img src="@context.User.FindFirstValue("avatar")" alt="Avatar" />
            <span>@context.User.Identity?.Name</span>
            <a href="/logout">Logout</a>
        </div>
    </Authorized>
    <NotAuthorized>
        <a href="/login" class="btn btn-primary">
            Login with FellowshipLogs
        </a>
    </NotAuthorized>
</AuthorizeView>

@* MyReports.razor - requires authentication *@
@page "/my-reports"
@attribute [Authorize]
@inject IFellowshipLogsUserClient UserClient

<h1>My Reports</h1>

@if (_reports == null)
{
    <Loading />
}
else
{
    <ul>
        @foreach (var report in _reports)
        {
            <li>
                <a href="/report/@report.Code">@report.Title</a>
                <span>@report.StartTime.ToString("g")</span>
            </li>
        }
    </ul>
}

@code {
    private IReadOnlyList<UserReport>? _reports;

    protected override async Task OnInitializedAsync()
    {
        _reports = await UserClient.GetMyReportsAsync();
    }
}
```

### Auth Endpoints

```csharp
// AuthEndpoints.cs
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/login", (string? returnUrl) =>
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = returnUrl ?? "/"
            };
            return Results.Challenge(properties, ["FellowshipLogs"]);
        });

        app.MapGet("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        });

        app.MapGet("/api/me", [Authorize] (ClaimsPrincipal user) =>
        {
            return Results.Ok(FellowshipUser.FromClaimsPrincipal(user));
        });
    }
}
```

### Configuration (appsettings.json)

```json
{
  "FellowshipLogs": {
    "ClientId": "your-app-client-id",
    "ClientSecret": "your-app-client-secret",
    "TokenEndpoint": "https://www.fellowshiplogs.com/oauth/token",
    "GraphQLEndpoint": "https://www.fellowshiplogs.com/api/v2/client",
    "UserAuth": {
      "ClientId": "your-user-auth-client-id",
      "ClientSecret": "your-user-auth-client-secret"
    }
  }
}
```

---

## Implementation Roadmap

### Phase 1: Core Infrastructure
1. [ ] Event type definitions (`ICombatEvent`, concrete events)
2. [ ] `Module` / `EventSubscriber` / `Analyzer` base classes
3. [ ] `AnalysisContext` scoped service
4. [ ] `EventFilter<T>` with fluent API
5. [ ] `EventEmitter` for dispatching
6. [ ] `CombatLogParser` base class
7. [ ] DI registration extension methods

### Phase 2: Server & Authentication
1. [ ] FellowshipLogs GraphQL client
2. [ ] Client credentials token handler
3. [ ] OAuth2 user authentication (BFF pattern)
4. [ ] User token service with refresh
5. [ ] Auth endpoints (`/login`, `/logout`, `/api/me`)
6. [ ] `AuthorizeView` integration

### Phase 3: Essential Modules
1. [ ] `Combatants` - track player info
2. [ ] `Enemies` - track enemy entities
3. [ ] `AbilityTracker` - aggregate ability usage
4. [ ] `DamageDone` / `HealingDone` / `DamageTaken`
5. [ ] `DeathTracker`
6. [ ] `StatTracker` / `Haste`

### Phase 4: Normalizers
1. [ ] `EventsNormalizer` base
2. [ ] `FightEndNormalizer`
3. [ ] `ApplyBuffNormalizer`
4. [ ] `EventLinkNormalizer` pattern

### Phase 5: Spec Framework
1. [ ] `SpecConfig` pattern
2. [ ] Spec-specific `CombatLogParser` inheritance
3. [ ] First spec implementation (pick a simpler DPS spec)

### Phase 6: UI Integration
1. [ ] Blazor component structure
2. [ ] Statistics rendering
3. [ ] Timeline visualization
4. [ ] Suggestions display

### Phase 7: Optimization
1. [ ] Source generators for event subscriptions
2. [ ] Memory pooling
3. [ ] Profiling and benchmarking

---

## Key Differences from WoWAnalyzer

| Aspect | WoWAnalyzer (TS) | FellowshipAnalyzer (C#) |
|--------|------------------|-------------------------|
| Events | Interface + type field | `readonly record struct` implementing interfaces |
| Event Linking | Mutable `_linkedEvents` array | Separate `EventLinks` class with indices |
| Module DI | Custom iterative resolver | Microsoft.Extensions.DependencyInjection |
| Module Dependencies | Static `dependencies` object | Constructor injection |
| Event Filters | Runtime closure compilation | Compile-time source generation option |
| Memory | GC managed, object allocations | Stack-allocated structs, pooling, `Span<T>` |
| UI Integration | React components | Blazor `RenderFragment` |
| Static Data | TypeScript objects | `FrozenDictionary`, `FrozenSet` |
| Authentication | N/A (client-side only) | OAuth2 with BFF pattern |

---

## Questions to Resolve

1. **Spell Database**: How to manage Fellowship's spell/ability database?
2. **Talent System**: Does Fellowship have a similar talent tree system?
3. **Group Composition**: With 4-man groups (Tank/Healer/2 DPS), any special analysis needs?
4. **Dungeon Modifiers**: Does Fellowship have dungeon affixes/modifiers to track?
5. **Rate Limiting**: What are FellowshipLogs API rate limits? (likely same as WarcraftLogs)

---

*This document serves as an architectural blueprint. Implementation details may evolve as development progresses.*
