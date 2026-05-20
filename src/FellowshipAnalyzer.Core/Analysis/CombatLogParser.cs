using System.Globalization;

using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI;

using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Orchestrates event processing through a set of modules.
/// Owns runtime analysis state (events, player, definition) and delegates
/// event dispatching to <see cref="EventEmitter"/>.
/// Registered as a scoped DI service; each <see cref="Analyze"/> call creates an
/// internal analysis-run service cache so repeated analyses do not share module state.
/// </summary>
[AddNormalizer<FightBookendNormalizer>]
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
[AddModule<SpiritTracker>]
public abstract partial class CombatLogParser(EventEmitter eventEmitter, IServiceProvider provider) : IHeroAnalyzer
{
    /// <summary>The outer DI container, passed through from the parser's primary constructor.
    /// Generated <c>CreateInstance</c> emits read from this to obtain framework-supplied
    /// dependencies such as <c>ILogger&lt;T&gt;</c> and <see cref="ReportMasterDataService"/>.</summary>
    protected IServiceProvider Provider { get; } = provider;

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
    /// Computed from the <see cref="Combatants"/> module — null until that module has populated
    /// its own <c>Selected</c>.
    /// </summary>
    public Combatant? SelectedCombatant => GetModule<Combatants>()?.Selected;

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
    private readonly Dictionary<Type, object> _runInstances = [];
    private readonly Dictionary<Type, int> _moduleTypeIndex = [];
    private Type[] _runModuleTypes = [];

    /// <summary>
    /// The <see cref="ParseContext"/> for the analysis currently in progress. Populated at the
    /// start of <see cref="Analyze"/> and read by generator-emitted <see cref="CreateInstance"/>
    /// to inject context into modules and normalizers that need it.
    /// </summary>
    protected ParseContext CurrentParseContext { get; private set; } = null!;

    /// <summary>
    /// Resolves a module by type for the current analysis run. Supports polymorphic resolution
    /// (e.g. <c>typeof(Abilities)</c> resolves to a hero's <c>Abilities</c> subclass when one is
    /// registered). Constructs via <see cref="CreateInstance"/> on first request, caches the
    /// result for the rest of the run, and assigns <see cref="Module.Owner"/> on construction.
    /// </summary>
    protected object ResolveAnalysisModule(Type type)
    {
        if (_runInstances.TryGetValue(type, out var existing)) return existing;

        var concrete = type;
        if (!_moduleTypeIndex.ContainsKey(type))
        {
            Type? match = null;
            foreach (var mt in _runModuleTypes)
            {
                if (!type.IsAssignableFrom(mt)) continue;
                if (match != null)
                    throw new InvalidOperationException($"Ambiguous module resolution: multiple registered modules are assignable to {type.Name}.");
                match = mt;
            }
            if (match is null)
                throw new InvalidOperationException($"No registered module is assignable to {type.Name}.");
            concrete = match;
            if (_runInstances.TryGetValue(concrete, out var existingConcrete))
            {
                _runInstances[type] = existingConcrete;
                return existingConcrete;
            }
        }

        var instance = CreateInstance(concrete)
            ?? throw new InvalidOperationException($"No generated factory for {concrete.Name}. Override CreateInstance on the parser to construct it.");

        _runInstances[concrete] = instance;
        if (type != concrete) _runInstances[type] = instance;

        if (instance is Module module)
        {
            module.Owner = this;
            if (_moduleTypeIndex.TryGetValue(concrete, out var priority))
                module.Priority = priority;
        }
        return instance;
    }

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
        PlayerId = playerId;
        Fight = fight;
        CurrentTimestamp = (int)fight.StartTime;

        var allModuleTypes = GetModuleTypes();
        var normalizerTypes = GetNormalizerTypes();

        _runInstances.Clear();
        _moduleTypeIndex.Clear();
        _runModuleTypes = allModuleTypes;
        for (var i = 0; i < allModuleTypes.Length; i++)
            _moduleTypeIndex[allModuleTypes[i]] = i;

        Events = [.. events];

        var playerInfo = Events.OfType<CombatantInfoEvent>().FirstOrDefault(e => e.SourceId == playerId)
            ?? new CombatantInfoEvent { SourceId = playerId };
        CurrentParseContext = new ParseContext(playerId, fight, ActorNames, new Combatant(playerInfo));

        EventEmitter = new EventEmitter((ILogger<EventEmitter>)Provider.GetService(typeof(ILogger<EventEmitter>))!)
        {
            Owner = this,
        };
        _runInstances[typeof(EventEmitter)] = EventEmitter;

        _activeModules = [];
        foreach (var t in allModuleTypes)
        {
            if (!IsModuleActive(t, CurrentParseContext)) continue;
            var m = (Module)ResolveAnalysisModule(t);
            m.Priority = _activeModules.Count;
            if (m.Active)
                _activeModules[m.GetType()] = m;
        }

        var tracker = Provider.GetService(typeof(ReportLoadingTracker)) as ReportLoadingTracker;

        if (tracker is not null)
        {
            tracker.NormalizeState = ReportLoadingTracker.StepState.Loading;
            tracker.TotalNormalizerCount = normalizerTypes.Length;
            tracker.NormalizedCount = 0;
        }
        await Task.Yield();

        foreach (var normalizerType in normalizerTypes)
        {
            var normalizer = (IEventNormalizer)(CreateInstance(normalizerType)
                ?? throw new InvalidOperationException($"No generated factory for normalizer {normalizerType.Name}."));
            Events = normalizer.Normalize(Events, playerId);
            if (tracker is not null) tracker.NormalizedCount++;
            await Task.Yield();
        }

        foreach (var m in _activeModules.Values)
        {
            if (m is EventSubscriber es) es.RegisterSubscriptions();
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

        return new HeroAnalysisResult
        {
            GuideComponentType = GuideComponent,
            Statistics = [.. _activeModules.Values
                    .Where(m => m.StatisticsComponentType != null)
                    .Select(m => new StatisticEntry(m, m.StatisticsComponentType!, m.StatisticCategory, m.StatisticOrder))],
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
}

