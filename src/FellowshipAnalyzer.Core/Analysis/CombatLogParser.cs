using System.Globalization;

using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Orchestrates event processing through a set of modules.
/// Owns runtime analysis state (events, player, definition) and delegates
/// event dispatching to <see cref="EventEmitter"/>.
/// Registered as a scoped DI service — one instance per analysis run.
/// </summary>
[AddModule<Combatants>]
[AddModule<TrackedStateModule>]
public abstract partial class CombatLogParser(EventEmitter eventEmitter, IServiceProvider provider) : IHeroAnalyzer
{
    public EventEmitter EventEmitter { get; } = eventEmitter;

    public List<Event> Events { get; set; } = [];
    public int PlayerId { get; set; }
    public int FightStartTime { get; set; }
    public abstract string HeroId { get; }

    public Combatants? Combatants => GetModule<Combatants>();
    public TrackedStateModule? TrackedStateModule => GetModule<TrackedStateModule>();

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
    /// Looks up an active module by type. Returns null if the module is
    /// inactive or has not been resolved yet.
    /// </summary>
    public T? GetModule<T>() where T : Module => _activeModules.TryGetValue(typeof(T), out var module) ? (T)module : null;

    public async Task<HeroAnalysisResult> Analyze(IReadOnlyList<Event> events, int playerId, int fightStartTime)
    {
        Events = [.. events];
        PlayerId = playerId;
        FightStartTime = fightStartTime;
        SelectedCombatant = null;
        _activeModules = GetModuleTypes()
            .Select((t, i) =>
            {
                var m = (Module)(provider.GetService(t) ?? throw new InvalidOperationException($"Module {t.Name} not registered."));
                m.Priority = i;
                m.Owner = this;
                return m;
            })
            .Where(m => m.Active)
            .ToDictionary(m => m.GetType(), m => m);

        await Task.Run(async () =>
        {
            // Run normalizers
            foreach (var normalizerType in GetNormalizerTypes())
            {
                var normalizer = (IEventNormalizer)(provider.GetService(normalizerType)
                    ?? throw new InvalidOperationException($"Normalizer {normalizerType.Name} not registered."));
                Events = normalizer.Normalize(Events, playerId);
            }

            foreach (var m in _activeModules.Values)
            {
                m.Initialize();
            }

            EventEmitter.SortListeners();

            await EventEmitter.DispatchEventsAsync(Events);

            foreach (var m in _activeModules.Values)
            {
                m.Complete();
            }
        });

        return new HeroAnalysisResult
        {
            GuideComponentType = GuideComponent!,
            Statistics = _activeModules.Values
                    .Where(m => m.StatisticsComponentType != null)
                    .Select(m => (m, m.StatisticsComponentType!))
                    .ToList(),
            Modules = [.. _activeModules.Values],
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

