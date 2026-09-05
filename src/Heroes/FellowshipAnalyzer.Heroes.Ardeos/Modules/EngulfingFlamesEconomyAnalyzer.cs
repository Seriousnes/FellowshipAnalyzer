using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<Combatants>]
public sealed partial class EngulfingFlamesEconomyAnalyzer : Analyzer
{
    public const int DoubleApplicationThreshold = 2;

    public const int ReadyLeadMs = 6000;

    public const int DevouringFlameBonusPerInstance = 6;

    private const int FallbackRechargeMs = 20_000;

    private readonly List<int> _wildfireCasts = [];
    private readonly List<int> _wildfireReadyTimes = [];
    private readonly List<int> _engulfingFlamesCasts = [];
    private readonly List<(int Timestamp, int Charges)> _chargeSamples = [];

    private List<ReadinessWindow> WindowList => field ??= BuildWindows();

    public List<ReadinessWindow> Windows => WindowList;

    public int WindowsEvaluated => WindowList.Count;

    public int DoubleAppliedWindows => WindowList.Count(window => window.DoubleApplied);

    public int SingleApplicationWindows => WindowList.Count(window => window.EngulfingFlamesCastCount == 1);

    public int MissedWindows => WindowList.Count(window => window.EngulfingFlamesCastCount == 0);

    public double DoubleApplicationRate => WindowsEvaluated == 0 ? 0 : (double)DoubleAppliedWindows / WindowsEvaluated;

    public int PullDurationMs => Math.Max(0, Pull.EndTime - Pull.StartTime);

    private long? _cappedMs;
    private long CappedMs => _cappedMs ??= ComputeCappedMilliseconds(Spells.EngulfingFlames.Charges);

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

    public double CappedSeconds => Math.Round(CappedMs / 1000d, 1);

    private DevouringFlameMetrics Devouring => field ??= ComputeDevouringFlame();

    public bool DevouringFlameEquipped => Owner.SelectedCombatant.HasItem(Items.DraconicBracersOfTheDevouringFlame.Id);

    public double DevouringFlameAnyUptime => Devouring.AnyUptime;

    public double DevouringFlameDoubleUptime => Devouring.DoubleUptime;

    public double DevouringFlamePrimaryAverageInstances => Devouring.PrimaryAverageInstances;

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
            return new DevouringFlameMetrics(AnyUptime: 0, DoubleUptime: 0, PrimaryAverageInstances: 0);

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

        long activeMs = 0;
        var runStart = 0;
        var runEnd = int.MinValue;
        foreach (var (start, end) in ordered)
        {
            if (start > runEnd)
            {
                if (runEnd > runStart)
                    activeMs += runEnd - runStart;
                runStart = start;
                runEnd = end;
            }
            else if (end > runEnd)
            {
                runEnd = end;
            }
        }

        if (runEnd > runStart)
            activeMs += runEnd - runStart;

        return activeMs;
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

    public sealed record ReadinessWindow
    {
        public int WindowStart { get; init; }
        public int ReadyTimestamp { get; init; }
        public int CastTimestamp { get; init; }
        public int ChargesAtReady { get; init; }
        public int HeldMs { get; init; }
        public List<int> EngulfingFlamesCasts { get; init; } = [];

        public int EngulfingFlamesCastCount => EngulfingFlamesCasts.Count;
        public bool DoubleApplied => EngulfingFlamesCastCount >= DoubleApplicationThreshold;
    }

    private record DevouringFlameMetrics(double AnyUptime, double DoubleUptime, double PrimaryAverageInstances);
}
