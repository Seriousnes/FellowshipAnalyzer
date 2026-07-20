using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Measures how well Ardeos sets Engulfing Flames up around Wildfire. Correct play is not about holding
/// both charges at the instant Wildfire is cast; it is about pooling the charges so that both Engulfing
/// Flames applications land in the setup as Wildfire becomes available, seeding two damage-over-time
/// windows before the burn. Once Wildfire is cast its nine seconds of twenty-percent-faster ticks reward
/// Detonate spam rather than more setup. Each Wildfire cast is scored on the Engulfing Flames casts inside
/// its readiness window; the classic overcap signal (Engulfing Flames sitting at maximum charges long
/// enough to waste full recharge periods) is surfaced alongside.
/// </summary>
/// <remarks>
/// A readiness window is anchored on Wildfire availability, one per Wildfire cast. The window spans
/// <c>[max(previous Wildfire cast, readyTime - <see cref="ReadyLeadMs"/>), wildfireCast]</c>, where
/// readyTime is the moment Wildfire came off cooldown - the pull start for the first window, since Wildfire
/// opens the pull available, and thereafter the <see cref="UpdateSpellUsableType.EndCooldown"/> that
/// <see cref="SpellUsable"/> fabricates for it. The window reaches <see cref="ReadyLeadMs"/> ahead of that
/// instant because a skilled player starts the two Engulfing Flames casts (1.5s each) slightly before
/// Wildfire is actually ready so both applications land as it becomes available. Charges at the ready
/// instant and the time Wildfire was held past ready are recorded as context only, never as gates: a
/// player who pre-cast Engulfing Flames legitimately has fewer charges at that instant, and holding
/// Wildfire for mechanics is legitimate. A window succeeds when it contains at least
/// <see cref="DoubleApplicationThreshold"/> Engulfing Flames casts; the qualitative tiers are decided in the
/// guide.
/// <para>
/// Overcap is reconstructed from the charge count over time: <see cref="SpellUsable"/> fabricates an
/// <see cref="UpdateSpellUsableEvent"/> whenever Engulfing Flames spends or restores a charge, and the
/// intervals it spends at max charges, assuming it starts the pull at max, are summed and divided by the
/// effective recharge period, so brief holds before an imminent window cost nothing while sitting capped
/// across recharge cycles is charged as waste.
/// </para>
/// <para>
/// When the player wears the legendary Draconic Bracers of the Devouring Flame (item
/// <see cref="DevouringFlameBracersItemId"/>) a sub-metric models that item's Devouring Flame effect, which
/// makes a target take <see cref="DevouringFlameBonusPerInstance"/> percent more damage for each Engulfing
/// Flames instance active on it. The effect itself is not yet observable in logs, so its value is modeled
/// from the Engulfing Flames damage-over-time windows the <see cref="Combatants"/> registry tracks on
/// enemies rather than from a logged effect id. One analyzer serves single-target and AoE pulls, since the
/// burn window is identical for both.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[Uses<Combatants>]
public sealed partial class EngulfingFlamesEconomyAnalyzer : Analyzer
{
    /// <summary>Engulfing Flames casts a window needs to seed both setup applications and count as successful.</summary>
    public const int DoubleApplicationThreshold = 2;

    /// <summary>
    /// How far ahead of Wildfire coming off cooldown the readiness window opens, in milliseconds. The two
    /// Engulfing Flames casts (1.5s each) can legitimately begin slightly before Wildfire is ready so both
    /// applications land as it becomes available, so the window reaches back this far ahead of the ready
    /// instant.
    /// </summary>
    public const int ReadyLeadMs = 6000;

    /// <summary>Modeled Devouring Flame incoming-damage bonus per active Engulfing Flames instance, as a percentage.</summary>
    public const int DevouringFlameBonusPerInstance = 6;

    /// <summary>The legendary Draconic Bracers of the Devouring Flame item id.</summary>
    public const int DevouringFlameBracersItemId = 5225;

    private const int FallbackRechargeMs = 20_000;

    private readonly List<int> _wildfireCasts = [];
    private readonly List<int> _wildfireReadyTimes = [];
    private readonly List<int> _engulfingFlamesCasts = [];
    private readonly List<(int Timestamp, int Charges)> _chargeSamples = [];

    private List<ReadinessWindow>? _windows;
    private List<ReadinessWindow> WindowList => _windows ??= BuildWindows();

    /// <summary>Per-Wildfire-cast readiness windows, one per Wildfire cast in the pull.</summary>
    public IReadOnlyList<ReadinessWindow> Windows => WindowList;

    public int WindowsEvaluated => WindowList.Count;

    /// <summary>Windows that seeded both setup applications (at least <see cref="DoubleApplicationThreshold"/> Engulfing Flames casts).</summary>
    public int DoubleAppliedWindows => WindowList.Count(window => window.DoubleApplied);

    /// <summary>Windows that fit only a single Engulfing Flames cast in the setup.</summary>
    public int SingleApplicationWindows => WindowList.Count(window => window.EngulfingFlamesCastCount == 1);

    /// <summary>Windows that entered Wildfire with no Engulfing Flames applied in the setup.</summary>
    public int MissedWindows => WindowList.Count(window => window.EngulfingFlamesCastCount == 0);

    /// <summary>Share of windows (0-1) that seeded both setup applications.</summary>
    public double DoubleApplicationRate => WindowsEvaluated == 0 ? 0 : (double)DoubleAppliedWindows / WindowsEvaluated;

    /// <summary>Pull length in milliseconds.</summary>
    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    private long? _cappedMs;
    private long CappedMs => _cappedMs ??= ComputeCappedMilliseconds(Spells.EngulfingFlames.Charges);

    /// <summary>
    /// Full Engulfing Flames recharge periods wasted sitting at maximum charges. Measured against the
    /// spell's <i>effective</i> recharge period (base cooldown accelerated by haste, gear, and cooldown
    /// recovery) rather than the raw curated 20 seconds, so a legendary's cooldown acceleration counts a
    /// capped window as more wasted charges, matching how fast the game would actually have refilled them.
    /// </summary>
    public int WastedCharges
    {
        get
        {
            var rechargeMs = Owner.SpellUsable!.RechargeDuration(Spells.EngulfingFlames.Id);
            if (rechargeMs <= 0)
                rechargeMs = Spells.EngulfingFlames.Cooldown is { } cooldown and > 0
                    ? (int)(cooldown * 1000)
                    : FallbackRechargeMs;
            return rechargeMs > 0 ? (int)(CappedMs / rechargeMs) : 0;
        }
    }

    /// <summary>Total seconds Engulfing Flames spent at maximum charges during the pull.</summary>
    public double CappedSeconds => Math.Round(CappedMs / 1000d, 1);

    private DevouringFlameMetrics? _devouring;
    private DevouringFlameMetrics Devouring => _devouring ??= ComputeDevouringFlame();

    /// <summary>True when the player wears the legendary Draconic Bracers of the Devouring Flame.</summary>
    public bool DevouringFlameEquipped => Combatants.Selected.HasGear(DevouringFlameBracersItemId);

    /// <summary>Share of the pull (0-1) at least one enemy carried an Engulfing Flames instance.</summary>
    public double DevouringFlameAnyUptime => Devouring.AnyUptime;

    /// <summary>Share of the pull (0-1) at least one enemy carried two or more concurrent Engulfing Flames instances.</summary>
    public double DevouringFlameDoubleUptime => Devouring.DoubleUptime;

    /// <summary>Time-weighted average Engulfing Flames instance count on the primary target (the enemy with the most instance-seconds).</summary>
    public double DevouringFlamePrimaryAverageInstances => Devouring.PrimaryAverageInstances;

    /// <summary>Modeled average Devouring Flame incoming-damage bonus on the primary target, as a percentage.</summary>
    public double DevouringFlameModeledBonusPercent => Devouring.PrimaryAverageInstances * DevouringFlameBonusPerInstance;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Wildfire))]
    private void OnWildfireCast(CastEvent e) => _wildfireCasts.Add(e.Timestamp);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.EngulfingFlames))]
    private void OnEngulfingFlamesCast(CastEvent e) => _engulfingFlamesCasts.Add(e.Timestamp);

    [On<UpdateSpellUsableEvent>(Spell = nameof(Spells.Wildfire))]
    private void OnWildfireUsable(UpdateSpellUsableEvent e)
    {
        if (e.UpdateType == UpdateSpellUsableType.EndCooldown)
            _wildfireReadyTimes.Add(e.Timestamp);
    }

    [On<UpdateSpellUsableEvent>(Spell = nameof(Spells.EngulfingFlames))]
    private void OnEngulfingFlamesUsable(UpdateSpellUsableEvent e) => _chargeSamples.Add((e.Timestamp, e.ChargesAvailable));

    private List<ReadinessWindow> BuildWindows()
    {
        var windows = new List<ReadinessWindow>(_wildfireCasts.Count);
        var previousCast = Pull.StartTime;

        foreach (var castTimestamp in _wildfireCasts)
        {
            var readyTime = ResolveReadyTime(castTimestamp, previousCast);
            var windowStart = Math.Max(previousCast, readyTime - ReadyLeadMs);

            var castsInSpan = new List<int>();
            foreach (var efCast in _engulfingFlamesCasts)
            {
                if (efCast >= windowStart && efCast <= castTimestamp)
                    castsInSpan.Add(efCast);
            }

            windows.Add(new ReadinessWindow
            {
                WindowStart = windowStart,
                ReadyTimestamp = readyTime,
                CastTimestamp = castTimestamp,
                ChargesAtReady = ChargesAt(readyTime),
                HeldMs = Math.Max(0, castTimestamp - readyTime),
                EngulfingFlamesCasts = castsInSpan,
            });

            previousCast = castTimestamp;
        }

        return windows;
    }

    /// <summary>
    /// The moment Wildfire became available for a window: the latest ready-time at or before the cast (pull
    /// start counts, since Wildfire opens the pull available), floored at the previous Wildfire cast so a
    /// cast the simulation still shows on cooldown cannot borrow a prior cycle's ready-time.
    /// </summary>
    private int ResolveReadyTime(int castTimestamp, int previousCast)
    {
        var readyTime = Pull.StartTime;
        foreach (var candidate in _wildfireReadyTimes)
        {
            if (candidate <= castTimestamp && candidate > readyTime)
                readyTime = candidate;
        }
        return Math.Max(readyTime, previousCast);
    }

    private int ChargesAt(int timestamp)
    {
        var charges = Spells.EngulfingFlames.Charges;
        foreach (var (sampleTime, sampleCharges) in _chargeSamples)
        {
            if (sampleTime > timestamp)
                break;
            charges = sampleCharges;
        }
        return charges;
    }

    private long ComputeCappedMilliseconds(int maxCharges)
    {
        var startTime = Pull.StartTime;
        var endTime = Pull.EndTime;
        if (endTime <= startTime)
            return 0;

        var transitions = new List<(int Time, int Charges)>(_chargeSamples.Count + 1)
        {
            (startTime, maxCharges),
        };
        foreach (var (timestamp, charges) in _chargeSamples)
            transitions.Add((Math.Clamp(timestamp, startTime, endTime), charges));

        long cappedMs = 0;
        for (var i = 0; i < transitions.Count; i++)
        {
            var segmentStart = transitions[i].Time;
            var segmentEnd = i + 1 < transitions.Count ? transitions[i + 1].Time : endTime;
            if (segmentEnd > segmentStart && transitions[i].Charges >= maxCharges)
                cappedMs += segmentEnd - segmentStart;
        }

        return cappedMs;
    }

    private DevouringFlameMetrics ComputeDevouringFlame()
    {
        var duration = Pull.EndTime - Pull.StartTime;
        if (!DevouringFlameEquipped || duration <= 0)
            return default;

        var perEnemyIntervals = new List<List<(int Start, int End)>>();
        foreach (var enemy in Combatants.Units.Values.OfType<Enemy>())
        {
            List<(int Start, int End)>? intervals = null;
            foreach (var buff in enemy.Buffs)
            {
                if (buff.Ability.Id != Spells.EngulfingFlamesDot.FSLID) continue;
                if (buff.SourceId != Owner.PlayerId) continue;

                var start = Math.Max(buff.Start, Pull.StartTime);
                var end = Math.Min(buff.End ?? Pull.EndTime, Pull.EndTime);
                if (end <= start) continue;

                (intervals ??= []).Add((start, end));
            }

            if (intervals is not null)
                perEnemyIntervals.Add(intervals);
        }

        var anyMs = UnionMilliseconds(perEnemyIntervals.SelectMany(intervals => intervals));

        var doubleRegions = new List<(int Start, int End)>();
        foreach (var intervals in perEnemyIntervals)
            doubleRegions.AddRange(RegionsWithOverlapAtLeast(intervals, DoubleApplicationThreshold));
        var doubleMs = UnionMilliseconds(doubleRegions);

        var primaryInstanceMs = 0L;
        foreach (var intervals in perEnemyIntervals)
        {
            var instanceMs = 0L;
            foreach (var (start, end) in intervals)
                instanceMs += end - start;
            if (instanceMs > primaryInstanceMs)
                primaryInstanceMs = instanceMs;
        }

        return new DevouringFlameMetrics(
            AnyUptime: (double)anyMs / duration,
            DoubleUptime: (double)doubleMs / duration,
            PrimaryAverageInstances: (double)primaryInstanceMs / duration);
    }

    private static long UnionMilliseconds(IEnumerable<(int Start, int End)> intervals)
    {
        var ordered = intervals.OrderBy(interval => interval.Start).ToList();

        long covered = 0;
        var coverStart = 0;
        var coverEnd = int.MinValue;
        foreach (var (start, end) in ordered)
        {
            if (start > coverEnd)
            {
                if (coverEnd > coverStart)
                    covered += coverEnd - coverStart;
                coverStart = start;
                coverEnd = end;
            }
            else if (end > coverEnd)
            {
                coverEnd = end;
            }
        }

        if (coverEnd > coverStart)
            covered += coverEnd - coverStart;

        return covered;
    }

    private static List<(int Start, int End)> RegionsWithOverlapAtLeast(List<(int Start, int End)> intervals, int minOverlap)
    {
        var regions = new List<(int Start, int End)>();
        if (intervals.Count < minOverlap)
            return regions;

        var deltas = new List<(int Time, int Delta)>(intervals.Count * 2);
        foreach (var (start, end) in intervals)
        {
            deltas.Add((start, 1));
            deltas.Add((end, -1));
        }
        deltas.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Delta.CompareTo(b.Delta));

        var count = 0;
        var previousTime = deltas[0].Time;
        foreach (var (time, delta) in deltas)
        {
            if (time > previousTime && count >= minOverlap)
                regions.Add((previousTime, time));
            count += delta;
            previousTime = time;
        }

        return regions;
    }

    /// <summary>
    /// A single readiness window: the setup span before one Wildfire cast, the Engulfing Flames casts it
    /// contained, and the context recorded at Wildfire's ready instant.
    /// </summary>
    public sealed record ReadinessWindow
    {
        public int WindowStart { get; init; }
        public int ReadyTimestamp { get; init; }
        public int CastTimestamp { get; init; }
        public int ChargesAtReady { get; init; }
        public int HeldMs { get; init; }
        public IReadOnlyList<int> EngulfingFlamesCasts { get; init; } = [];

        public int EngulfingFlamesCastCount => EngulfingFlamesCasts.Count;
        public bool DoubleApplied => EngulfingFlamesCastCount >= DoubleApplicationThreshold;
    }

    private readonly record struct DevouringFlameMetrics(double AnyUptime, double DoubleUptime, double PrimaryAverageInstances);
}
