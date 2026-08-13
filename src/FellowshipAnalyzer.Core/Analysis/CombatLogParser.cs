using System.Globalization;

using FellowshipAnalyzer.Core.Analysis.Deaths;
using FellowshipAnalyzer.Core.Analysis.Gems;
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
/// <para>
/// One instance serves exactly one analysis. Registered as a transient DI service, so the host
/// resolves a fresh parser for every report it analyzes and the read surfaces a guide renders can
/// only ever hold that analysis. Reusing an instance across reports is not supported: the retained
/// pull analyzers, modules, and event list all belong to the analysis that produced them.
/// </para>
/// </summary>
[AddNormalizer<PullBookendNormalizer>]
[AddNormalizer<DungeonBookendNormalizer>]
[AddNormalizer<AbilityMasterDataNormalizer>]
[AddNormalizer<ResourceNormalizer>]
[AddNormalizer<CastLinkNormalizer>]
[AddModule<DebugAnnotations>]
[AddAnalyzer<Combatants>]
[AddModule<Enemies>]
[AddAnalyzer<StatTracker>]
[AddAnalyzer<Haste>]
[AddAnalyzer<GlobalCooldown>]
[AddAnalyzer<SpellUsable>]
[AddAnalyzer<DeathTracker>]
[AddAnalyzer<ChronoshiftAnalyzer>]
[AddAnalyzer<ThroughputTracker>]
[AddAnalyzer<AbilityTracker>]
[AddAnalyzer<GloriousPurposeAnalyzer>]
[AddAnalyzer<RubyGemAnalyzer>]
[AddAnalyzer<AmethystGemAnalyzer>]
[AddAnalyzer<TopazGemAnalyzer>]
[AddAnalyzer<EmeraldGemAnalyzer>]
[AddAnalyzer<SapphireGemAnalyzer>]
[AddAnalyzer<DiamondGemAnalyzer>]
[AddAnalyzer<SpiritTracker>]
public abstract partial class CombatLogParser(EventEmitter eventEmitter, IServiceProvider provider) : IHeroAnalyzer
{
    /// <summary>The outer DI container, passed through from the parser's primary constructor.
    /// Generated <c>CreateInstance</c> emits read from this to obtain framework-supplied
    /// dependencies such as <c>ILogger&lt;T&gt;</c> and <see cref="ReportMasterDataService"/>.</summary>
    protected IServiceProvider Provider { get; } = provider;

    /// <summary>The event dispatcher for the analysis in progress. Replaced with a fresh instance at the start of every <see cref="Analyze"/> call.</summary>
    public EventEmitter EventEmitter { get; private set; } = eventEmitter;

    /// <summary>The normalized event stream for the current analysis, after every <see cref="IEventNormalizer"/> pass has run.</summary>
    public List<Event> Events { get; set; } = [];

    /// <summary>The actor id of the player this analysis is scoped to.</summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// The dungeon currently being analyzed. Set at the start of every <see cref="Analyze"/> call.
    /// </summary>
    public ReportDungeon Dungeon { get; private set; } = null!;

    /// <summary>
    /// The timestamp of the event currently being dispatched. Updated by <see cref="EventEmitter"/>
    /// before each listener invocation. Initialized to <see cref="Dungeon"/>.StartTime when <see cref="Analyze"/> begins.
    /// </summary>
    public int CurrentTimestamp { get; internal set; }

    /// <summary>The analyzed dungeon's start time, from <see cref="Dungeon"/>.</summary>
    public int DungeonStartTime => (int)Dungeon.StartTime;

    /// <summary>The analyzed dungeon's end time, from <see cref="Dungeon"/>.</summary>
    public int DungeonEndTime => (int)Dungeon.EndTime;

    /// <summary>The analyzed dungeon's duration in milliseconds.</summary>
    public int DungeonDurationMs => DungeonEndTime - DungeonStartTime;

    /// <summary>
    /// Report-level actor master data: every player and NPC the report names.
    /// Set by the host (e.g. Report.razor) before <see cref="Analyze"/> is called, and the source
    /// <see cref="Enemies"/> reads hostility from.
    /// </summary>
    public IReadOnlyList<ReportActor> Actors { get; set; } = [];

    /// <summary>Actor display names keyed by actor id, derived from <see cref="Actors"/>.</summary>
    public IReadOnlyDictionary<int, string> ActorNames =>
        field ??= Actors.ToDictionary(actor => actor.Id, actor => actor.Name);

    /// <summary>
    /// The combatant representing the selected (analyzed) player. Populated by
    /// <see cref="Analyze"/> before any module is constructed.
    /// </summary>
    public FullCombatant SelectedCombatant => CurrentParseContext.SelectedCombatant;

    /// <summary>
    /// Builds the selected player's <see cref="FullCombatant"/> from the resolved combatantinfo. Overridable so
    /// tests can supply a combatant with hand-built <see cref="CombatantStats"/> (e.g. scoped cooldown
    /// modifiers that no combatantinfo field yet produces).
    /// </summary>
    protected virtual FullCombatant CreateSelectedCombatant(CombatantInfoEvent info) => new(info);

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
    private readonly HashSet<Type> _runModuleTypeSet = [];
    private Type[] _runModuleTypes = [];
    private readonly Dictionary<Type, object> _pullInstances = [];
    private readonly List<(PullStartEvent Pull, Analyzer Analyzer)> _pullAnalyzers = [];
    private readonly List<PullStartEvent> _pulls = [];

    /// <summary>
    /// The active parser for the analysis currently in progress, exposed to pipeline-internal
    /// dispatch wiring. Set in a <c>try/finally</c> at the top of <see cref="Analyze"/>. Event
    /// processing is sequential and synchronous, so a plain static suffices.
    /// </summary>
    public static CombatLogParser? Current { get; private set; }

    /// <summary>
    /// The pull currently being dispatched, or <c>null</c> outside any pull window (dungeon setup,
    /// teardown, and gaps between pulls). Set by <see cref="BeginPull"/> / <see cref="EndPull"/>.
    /// </summary>
    public PullStartEvent? CurrentPull { get; private set; }

    /// <summary>
    /// When set, generated analyzer surface streams (<c>{Surface}s</c>) and <see cref="PullAnalyzers"/>
    /// are clamped to this pull, so a report view can crop to a single encounter. Reset to <c>null</c>
    /// at the start of every <see cref="Analyze"/> run, and only ever assigned afterwards by the host.
    /// <see cref="Pulls"/> stays unclamped.
    /// </summary>
    public PullStartEvent? SelectedPull { get; set; }

    /// <summary>
    /// Analyzer instances retained at each <see cref="PullEndEvent"/>, in pull order.
    /// Clamped to <see cref="SelectedPull"/> when one is set.
    /// </summary>
    public IReadOnlyList<(PullStartEvent Pull, Analyzer Analyzer)> PullAnalyzers =>
        SelectedPull is null
            ? _pullAnalyzers
            : [.. _pullAnalyzers.Where(entry => ReferenceEquals(entry.Pull, SelectedPull))];

    /// <summary>
    /// Every ended pull in encounter order, whether or not any analyzer matched it. Guides walk
    /// this to compose sections from more than one analyzer surface, probing each pull's
    /// generated nullable accessors for the shape that ran.
    /// </summary>
    public IReadOnlyList<PullStartEvent> Pulls => _pulls;

    /// <summary>
    /// Filters a generated per-surface analyzer stream to <see cref="SelectedPull"/>. Returns the
    /// backing list unchanged when no pull is selected; otherwise the single entry whose
    /// <see cref="PullAnalyzer{T}.Pull"/> is reference-equal to the selection (diagnostic FA0016
    /// guarantees at most one analyzer per surface per pull). Called by generated surface getters.
    /// </summary>
    protected IReadOnlyList<PullAnalyzer<T>> ClampToSelectedPull<T>(PullAnalyzerList<T> analyzers)
        where T : IAnalyzerSurface
    {
        if (SelectedPull is null) return analyzers;
        return [.. analyzers.Where(entry => ReferenceEquals(entry.Pull, SelectedPull))];
    }

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
        if (!_runModuleTypeSet.Contains(type))
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
        }
        return instance;
    }

    /// <summary>
    /// Returns the types of all modules to resolve from DI for this parser. Source-generated, covering
    /// base and own modules in the order <c>[Before&lt;T&gt;]</c> and <c>[After&lt;T&gt;]</c>
    /// topologically sort them, which is the order they are constructed and subscribed in.
    /// </summary>
    protected abstract Type[] GetModuleTypes();

    /// <summary>
    /// Returns the types of all normalizers to resolve from DI.
    /// Source-generated when the parser has <see cref="AddNormalizerAttribute{T}"/> attributes.
    /// </summary>
    protected virtual Type[] GetNormalizerTypes() => [];

    /// <summary>
    /// Returns the <c>[ForPull]</c> <see cref="Analyzer"/> types. The default is empty; concrete
    /// parsers override it.
    /// </summary>
    protected virtual Type[] GetAnalyzerTypes() => [];

    /// <summary>
    /// Returns the <see cref="Analyzer"/> types whose <c>[ForPull]</c> filter matches
    /// <paramref name="pull"/>. A fresh instance of each is constructed in <see cref="BeginPull"/>
    /// and discarded in <see cref="EndPull"/>. The source generator overrides this with a static
    /// bitmask gate; the default runs every analyzer on every pull.
    /// </summary>
    protected virtual Type[] GetAnalyzerTypes(PullStartEvent pull) => GetAnalyzerTypes();

    /// <summary>
    /// Routes a retained analyzer into the parser's denormalized, surface-typed cross-pull
    /// index. The default is a no-op; the source generator overrides it for parsers that register a
    /// <c>[ForPull]</c> analyzer, appending to the matching <see cref="PullAnalyzerList{T}"/>.
    /// </summary>
    protected virtual void IndexPullAnalyzer(PullStartEvent pull, Analyzer analyzer) { }

    /// <summary>
    /// Opens a pull: constructs a fresh instance of every <see cref="GetAnalyzerTypes(PullStartEvent)"/>
    /// analyzer into the per-pull cache and routes their subscriptions into the pull listener tier.
    /// Enforces the single-open-pull invariant by closing any already-open pull first (close-before-open).
    /// </summary>
    public void BeginPull(PullStartEvent pull)
    {
        if (CurrentPull is not null) EndPull(CurrentPull);

        CurrentPull = pull;
        _pullInstances.Clear();

        EventEmitter.BeginPullSubscriptions();
        foreach (var analyzerType in GetAnalyzerTypes(pull))
        {
            var instance = CreateInstance(analyzerType)
                ?? throw new InvalidOperationException($"No generated factory for analyzer {analyzerType.Name}.");
            _pullInstances[analyzerType] = instance;
            if (instance is Module module) module.Owner = this;
            if (instance is Analyzer analyzer)
            {
                analyzer.Pull = pull;
                analyzer.RegisterSubscriptions();
            }
        }
        EventEmitter.EndPullSubscriptions();
    }

    /// <summary>
    /// Closes a pull, in order: emits the pull's own <see cref="PullStartEvent.End"/> to the pull's
    /// listeners while they are still live (so a subscriber can snapshot dungeon-lifetime state at the
    /// instant the pull ends), then retains each analyzer on the pull read surfaces, then retires the
    /// pull listener tier and discards the per-pull instance cache. Emitting here (rather than relying on
    /// the fabricated event's dispatch) is what makes the event fire exactly once for every pull,
    /// including a force-close triggered by <see cref="BeginPull"/>. Analyzers derive their own per-pull
    /// metrics from retained state via get-style accessors, so this does not run a finalization pass.
    /// Ignores a <paramref name="pull"/> that is not the currently-open one (a stale end left over from
    /// close-before-open).
    /// </summary>
    public void EndPull(PullStartEvent pull)
    {
        if (!ReferenceEquals(CurrentPull, pull)) return;

        _pulls.Add(pull);

        EventEmitter.Emit(pull.End);

        foreach (var instance in _pullInstances.Values)
        {
            if (instance is not Analyzer analyzer) continue;

            _pullAnalyzers.Add((pull, analyzer));
            pull.Metadata.SetAnalyzer(Analyzer.GetSurfaceType(analyzer.GetType()), analyzer);
            IndexPullAnalyzer(pull, analyzer);
        }

        EventEmitter.UnsubscribePullAnalyzers();
        _pullInstances.Clear();
        CurrentPull = null;
    }

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

        foreach (var m in _activeModules.Values)
        {
            if (m is T match)
                return match;
        }

        return null;
    }

    /// <summary>Runs the full analysis pipeline over <paramref name="events"/> for <paramref name="playerId"/> during <paramref name="dungeon"/>: normalizes the stream, dispatches it through every module and analyzer, and returns the resulting <see cref="HeroAnalysisResult"/>.</summary>
    public Task<HeroAnalysisResult> Analyze(IReadOnlyList<Event> events, int playerId, ReportDungeon dungeon)
        => RunAnalysisAsync(events, playerId, dungeon);

    private async Task<HeroAnalysisResult> RunAnalysisAsync(IReadOnlyList<Event> events, int playerId, ReportDungeon dungeon)
    {
        PlayerId = playerId;
        Dungeon = dungeon;
        CurrentTimestamp = (int)dungeon.StartTime;

        var allModuleTypes = GetModuleTypes();
        var normalizerTypes = GetNormalizerTypes();

        _pullInstances.Clear();
        _pullAnalyzers.Clear();
        _pulls.Clear();
        CurrentPull = null;
        SelectedPull = null;

        _runInstances.Clear();
        _runModuleTypeSet.Clear();
        _runModuleTypes = allModuleTypes;
        _runModuleTypeSet.UnionWith(allModuleTypes);

        Events = [.. events.Where(e => e is not CastEvent { Fake: true })];

        var playerInfo = Events.OfType<CombatantInfoEvent>().FirstOrDefault(e => e.SourceId == playerId)
            ?? new CombatantInfoEvent { SourceId = playerId };
        CurrentParseContext = new ParseContext(playerId, dungeon, ActorNames, CreateSelectedCombatant(playerInfo), Actors);

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
            if (m is Analyzer analyzer) analyzer.RegisterSubscriptions();
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

        tracker?.AnalyzeState = ReportLoadingTracker.StepState.Ok;
        await Task.Yield();

        return new HeroAnalysisResult
        {
            GuideComponentType = GuideComponent,
            Statistics = [.. _activeModules.Values
                    .Select(m => (Module: m, Statistic: m.Statistic))
                    .Where(collected => collected.Statistic is not null)
                    .Select(collected => new StatisticEntry(
                        collected.Module,
                        collected.Statistic!,
                        collected.Module.StatisticCategory,
                        collected.Module.StatisticOrder))],
            Modules = [.. _activeModules.Values],
            Events = Events,
            DebugAnnotations = GetModule<DebugAnnotations>(),
        };
    }

    /// <summary>
    /// Formats an absolute event timestamp as time into the analyzed dungeon (mm:ss).
    /// Uses <see cref="DungeonStartTime"/> obtained from the Fellowship Logs API.
    /// </summary>
    public string FormatTimestamp(int timestamp, int precision = 0)
    {
        var totalSeconds = (timestamp - DungeonStartTime) / 1000d;
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

    /// <summary>Whether <paramref name="e"/> was sourced by <paramref name="playerId"/>, defaulting to <see cref="PlayerId"/>.</summary>
    public bool ByPlayer(IHasSourceEvent e, int? playerId = null) => e.SourceId == (playerId ?? PlayerId);

    /// <summary>Whether <paramref name="e"/> targeted <paramref name="playerId"/>, defaulting to <see cref="PlayerId"/>.</summary>
    public bool ToPlayer(IHasTargetEvent e, int? playerId = null) => e.TargetId == (playerId ?? PlayerId);

}

