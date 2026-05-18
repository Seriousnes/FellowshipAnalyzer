using System.Globalization;

using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.DependencyInjection;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Orchestrates event processing through a set of modules.
/// Owns runtime analysis state (events, player, definition) and delegates
/// event dispatching to <see cref="EventEmitter"/>.
/// Registered as a scoped DI service; each <see cref="Analyze"/> call creates an
/// internal analysis-run service cache so repeated analyses do not share module state.
/// </summary>
[AddNormalizer<AbilityMasterDataNormalizer>]
[AddNormalizer<ResourceNormalizer>]
[AddNormalizer<CastLinkNormalizer>]
[AddNormalizer<ResourceFabricationNormalizer>]
[AddModule<DebugAnnotations>]
[AddModule<Combatants>]
[AddModule<StatTracker>]
[AddModule<Haste>]
[AddModule<GlobalCooldown>]
[AddModule<SpellUsable>]
[AddModule<ChronoshiftAnalyzer>]
public abstract partial class CombatLogParser(EventEmitter eventEmitter, IServiceProvider provider) : IHeroAnalyzer
{
    public EventEmitter EventEmitter { get; private set; } = eventEmitter;

    public List<Event> Events { get; set; } = [];
    public int PlayerId { get; set; }

    /// <summary>
    /// The fight currently being analyzed. Set at the start of every <see cref="Analyze"/> call.
    /// </summary>
    public ReportFight Fight { get; private set; } = null!;

    /// <summary>
    /// The timestamp of the event currently being dispatched. Updated by <see cref="EventEmitter"/>
    /// before each listener invocation. Initialized to <see cref="Fight"/>.StartTime when <see cref="Analyze"/> begins.
    /// </summary>
    public int CurrentTimestamp { get; internal set; }

    public int FightStartTime => (int)Fight.StartTime;
    public int FightEndTime => (int)Fight.EndTime;

    /// <summary>
    /// Report-level actor name lookup, keyed by actor ID.
    /// Set by the host (e.g. Report.razor) before <see cref="Analyze"/> is called.
    /// </summary>
    public Dictionary<int, string> ActorNames { get; set; } = [];

    /// <summary>
    /// The combatant representing the selected (analyzed) player.
    /// Set by the <see cref="Combatants"/> module before event dispatch.
    /// </summary>
    public Combatant? SelectedCombatant { get; set; }

    /// <summary>
    /// The Razor component type to render for the Guide tab.
    /// Source-generated parsers override this to return their hero's Guide.razor type.
    /// </summary>
    public virtual Type? GuideComponent => null;

    /// <summary>
    /// The hero this parser is for. Source-generated from <see cref="HeroAnalyzerAttribute"/>.
    /// Returns <c>null</c> for parsers without a <see cref="HeroAnalyzerAttribute"/> (e.g. test parsers).
    /// </summary>
    public virtual Hero? Hero => null;

    private Dictionary<Type, Module> _activeModules = [];

    /// <summary>
    /// Returns the types of all modules to resolve from DI for this parser.
    /// Source-generated — includes base + own modules in priority order.
    /// </summary>
    protected abstract Type[] GetModuleTypes();

    /// <summary>
    /// Returns the types of all normalizers to resolve from DI.
    /// Source-generated when the parser has <see cref="AddNormalizerAttribute{T}"/> attributes.
    /// </summary>
    protected virtual Type[] GetNormalizerTypes() => [];

    /// <summary>
    /// Builds the source-generated typed projection of this analysis run. The default returns
    /// <c>null</c>. Source-generated concrete parsers override this when at least one of their
    /// modules declares a <c>ToReport()</c> method, returning a hero-specific result record
    /// (e.g. <c>RimeAnalysisResult</c>).
    /// </summary>
    protected virtual object? BuildTypedReport() => null;

    /// <summary>
    /// Consulted before constructing each module declared in <see cref="GetModuleTypes"/>.
    /// The default returns <c>true</c> for every module. The source generator overrides this
    /// on concrete parsers that have at least one module decorated with
    /// <see cref="ActiveWhenAttribute{TPredicate}"/>, switching on the module type and
    /// invoking the predicate's static <c>IsActive</c> method.
    /// </summary>
    protected virtual bool IsModuleActive(Type moduleType, ParseContext context) => true;

    /// <summary>
    /// Looks up an active module by type. Returns null if the module is
    /// inactive or has not been resolved yet.
    /// </summary>
    public T? GetModule<T>() where T : Module
    {
        if (_activeModules.TryGetValue(typeof(T), out var exact))
            return (T)exact;

        // Support polymorphic lookup: GetModule<BaseType>() finds a registered subclass.
        foreach (var m in _activeModules.Values)
        {
            if (m is T match)
                return match;
        }

        return null;
    }

    public async Task<HeroAnalysisResult> Analyze(IReadOnlyList<Event> events, int playerId, ReportFight fight)
    {
        // Assign parse-time state up-front so ParseContext and IsModuleActive can read it.
        PlayerId = playerId;
        Fight = fight;
        CurrentTimestamp = (int)fight.StartTime;

        // Filter modules through the source-generated activation predicate before resolution
        // so [ActiveWhen<>]-disabled modules never instantiate, subscribe, or accumulate state.
        var parseContext = new ParseContext(playerId, fight, ActorNames);
        var allModuleTypes = GetModuleTypes();
        var moduleTypes = Array.FindAll(allModuleTypes, t => IsModuleActive(t, parseContext));
        var normalizerTypes = GetNormalizerTypes();
        var analysisServices = new AnalysisRunServiceProvider(provider, this, moduleTypes, normalizerTypes);

        EventEmitter = analysisServices.GetRequiredService<EventEmitter>();
        Events = [.. events];
        SelectedCombatant = null;
        _activeModules = moduleTypes
            .Select((t, i) =>
            {
                var m = (Module)(analysisServices.GetService(t) ?? throw new InvalidOperationException($"Module {t.Name} not registered."));
                m.Priority = i;
                m.Owner = this;
                return m;
            })
            .Where(m => m.Active)
            .ToDictionary(m => m.GetType(), m => m);

        var tracker = provider.GetService(typeof(ReportLoadingTracker)) as ReportLoadingTracker;

        // Run normalizers
        if (tracker is not null)
        {
            tracker.NormalizeState = ReportLoadingTracker.StepState.Loading;
            tracker.TotalNormalizerCount = normalizerTypes.Length;
            tracker.NormalizedCount = 0;
        }
        await Task.Yield();

        foreach (var normalizerType in normalizerTypes)
        {
            var normalizer = (IEventNormalizer)(analysisServices.GetService(normalizerType)
                ?? throw new InvalidOperationException($"Normalizer {normalizerType.Name} not registered."));
            Events = normalizer.Normalize(Events, playerId);
            if (tracker is not null) tracker.NormalizedCount++;
            await Task.Yield();
        }

        foreach (var m in _activeModules.Values)
        {
            m.Initialize();
        }

        EventEmitter.SortListeners();

        if (tracker is not null)
        {
            tracker.NormalizeState = ReportLoadingTracker.StepState.Ok;
            tracker.AnalyzeState = ReportLoadingTracker.StepState.Loading;
            tracker.TotalEventCount = Events.Count;
        }
        await Task.Yield();

        await EventEmitter.DispatchEventsAsync(Events, tracker);

        if (tracker is not null)
        {
            tracker.AnalyzeState = ReportLoadingTracker.StepState.Ok;
        }
        await Task.Yield();

        foreach (var m in _activeModules.Values)
        {
            m.Complete();
        }

        return new HeroAnalysisResult
        {
            GuideComponentType = GuideComponent,
            Statistics = _activeModules.Values
                    .Where(m => m.StatisticsComponentType != null)
                    .Select(m => (m, m.StatisticsComponentType!))
                    .ToList(),
            Modules = [.. _activeModules.Values],
            Events = Events,
            DebugAnnotations = GetModule<DebugAnnotations>(),
            TypedReport = BuildTypedReport(),
        };
    }

    /// <summary>
    /// Formats an absolute event timestamp as time into the analyzed fight (mm:ss).
    /// Uses <see cref="FightStartTime"/> obtained from the Fellowship Logs API.
    /// </summary>
    public string FormatTimestamp(int timestamp, int precision = 0)
    {
        var totalSeconds = (timestamp - FightStartTime) / 1000d;
        var negative = totalSeconds < 0 ? "-" : string.Empty;
        var positiveSeconds = Math.Abs(totalSeconds);
        var minutes = (int)Math.Floor(positiveSeconds / 60);
        var multiplier = Math.Pow(10, precision);
        var remainder = (Math.Floor((positiveSeconds % 60) * multiplier) / multiplier)
            .ToString($"F{precision}", CultureInfo.InvariantCulture);
        var seconds = double.Parse(remainder, CultureInfo.InvariantCulture) < 10
            ? $"0{remainder}"
            : remainder;

        return $"{negative}{minutes}:{seconds}";
    }

    public bool ByPlayer(IHasSourceEvent e, int? playerId = null) => e.SourceId == (playerId ?? PlayerId);

    public bool ToPlayer(IHasTargetEvent e, int? playerId = null) => e.TargetId == (playerId ?? PlayerId);

    public bool ByPlayerPet(IHasSourceEvent e) => false; // TODO: implement when pet tracking is added

    public bool ToPlayerPet(IHasTargetEvent e) => false; // TODO: implement when pet tracking is added

    private sealed class AnalysisRunServiceProvider(
        IServiceProvider fallback,
        CombatLogParser owner,
        IReadOnlyList<Type> moduleTypes,
        IReadOnlyList<Type> normalizerTypes) : IServiceProvider
    {
        private static readonly System.Reflection.MethodInfo CreateLazyMethod =
            typeof(AnalysisRunServiceProvider).GetMethod(
                nameof(CreateLazy),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        private readonly Dictionary<Type, object> _instances = [];
        private readonly Dictionary<Type, int> _modulePriorities = moduleTypes
            .Select((type, index) => (type, index))
            .ToDictionary(static x => x.type, static x => x.index);
        private readonly HashSet<Type> _normalizerTypes = [.. normalizerTypes];

        private Lazy<T> CreateLazy<T>() where T : class =>
            new(() => (T)GetService(typeof(T))!);

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceProvider))
            {
                return this;
            }

            if (serviceType == typeof(EventEmitter))
            {
                return GetOrCreate(typeof(EventEmitter));
            }

            if (serviceType == typeof(ParseContext))
            {
                // Materialized lazily — the parser fills PlayerId/Fight before any handler fires,
                // but ctors run before that. The closure resolves through Owner at call time.
                return new ParseContext(owner.PlayerId, owner.Fight, owner.ActorNames);
            }

            // Lazy<TModule> defers resolution to break ctor cycles without giving up DI.
            // Cycle analyzer FA0013 ignores Lazy<>-shaped edges.
            if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(Lazy<>))
            {
                var inner = serviceType.GetGenericArguments()[0];
                return CreateLazyMethod
                    .MakeGenericMethod(inner)
                    .Invoke(this, null);
            }

            if (TryGetModuleType(serviceType, out var moduleType))
            {
                return GetOrCreate(moduleType);
            }

            if (_normalizerTypes.Contains(serviceType))
            {
                return GetOrCreate(serviceType);
            }

            return fallback.GetService(serviceType);
        }

        private object GetOrCreate(Type implementationType)
        {
            if (_instances.TryGetValue(implementationType, out var existing))
            {
                return existing;
            }

            var instance = ActivatorUtilities.CreateInstance(this, implementationType);
            _instances[implementationType] = instance;

            if (instance is Module module)
            {
                module.Owner = owner;
                if (_modulePriorities.TryGetValue(implementationType, out var priority))
                {
                    module.Priority = priority;
                }
            }

            return instance;
        }

        private bool TryGetModuleType(Type serviceType, out Type moduleType)
        {
            if (_modulePriorities.ContainsKey(serviceType))
            {
                moduleType = serviceType;
                return true;
            }

            var matches = moduleTypes.Where(serviceType.IsAssignableFrom).ToList();
            if (matches.Count == 1)
            {
                moduleType = matches[0];
                return true;
            }

            moduleType = null!;
            return false;
        }
    }
}

