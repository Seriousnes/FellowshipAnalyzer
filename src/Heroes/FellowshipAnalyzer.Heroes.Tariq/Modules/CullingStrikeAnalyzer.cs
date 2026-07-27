using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

[ForPull(PullKind.Single, Boss = PullBoss.Boss)]
public sealed partial class CullingStrikeAnalyzer : Analyzer
{
    public const double ExecuteHealthThreshold = 0.30;

    public const int ExpectedCastIntervalMs = 7000;

    private readonly Dictionary<(int TargetId, int TargetInstance), List<HealthSample>> _samplesByInstance = [];
    private readonly Dictionary<int, List<HealthSample>> _samplesByTargetId = [];
    private readonly List<TrackedCast> _casts = [];

    private Computed Result => field ??= Compute();

    public int? ExecutePhaseStartTimestamp => Result.PhaseStart;

    public int ExecutePhaseDurationMs => Result.PhaseDurationMs;

    public IReadOnlyList<CullingStrikeCast> Casts => Result.Casts;

    public int TotalCasts => Result.Casts.Count;

    public int CastsInPhase => Result.CastsInPhase;

    public int CastsAboveThreshold => Result.CastsAboveThreshold;

    public int PossibleCasts => Result.PossibleCasts;

    public double Coverage => Result.Coverage;

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamage(DamageEvent @event)
    {
        var resources = @event.TargetResources;
        if (resources is null || resources.MaxHitPoints <= 0 || resources.HitPoints > resources.MaxHitPoints)
            return;

        var sample = new HealthSample(@event.Timestamp, (double)resources.HitPoints / resources.MaxHitPoints);
        SeriesFor(_samplesByInstance, (@event.TargetId, @event.TargetInstance ?? 0)).Add(sample);
        SeriesFor(_samplesByTargetId, @event.TargetId).Add(sample);
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.CullingStrike))]
    private void OnCullingStrikeCast(CastEvent @event)
    {
        if (@event.Fake)
            return;

        _casts.Add(new TrackedCast(@event.Timestamp, @event.TargetId, @event.TargetInstance ?? 0));
    }

    private static List<HealthSample> SeriesFor<TKey>(Dictionary<TKey, List<HealthSample>> series, TKey key)
        where TKey : notnull
    {
        if (series.TryGetValue(key, out var existing))
            return existing;

        existing = [];
        series[key] = existing;
        return existing;
    }

    private Computed Compute()
    {
        var phaseStart = FindPhaseStart();
        var phaseDuration = phaseStart is { } start ? Math.Max(0, Pull.EndTime - start) : 0;

        var casts = new List<CullingStrikeCast>(_casts.Count);
        foreach (var cast in _casts)
        {
            var health = HealthAt(cast);
            casts.Add(new CullingStrikeCast(
                cast.Timestamp,
                health,
                phaseStart is { } opened && cast.Timestamp >= opened,
                health is { } percent && percent > ExecuteHealthThreshold));
        }

        var castsInPhase = casts.Count(cast => cast.InExecutePhase && !cast.AboveThreshold);
        var possible = Math.Max(castsInPhase, phaseDuration / ExpectedCastIntervalMs);
        var coverage = possible == 0 ? 1d : Math.Min(1d, (double)castsInPhase / possible);

        return new Computed(
            phaseStart,
            phaseDuration,
            casts,
            castsInPhase,
            casts.Count(cast => cast.AboveThreshold),
            possible,
            coverage);
    }

    private int? FindPhaseStart()
    {
        List<HealthSample>? primary = null;
        foreach (var series in _samplesByInstance.Values)
        {
            if (primary is null || series.Count > primary.Count)
                primary = series;
        }

        if (primary is null)
            return null;

        foreach (var sample in primary)
        {
            if (sample.HealthPercent <= ExecuteHealthThreshold)
                return sample.Timestamp;
        }

        return null;
    }

    private double? HealthAt(TrackedCast cast)
    {
        if (_samplesByInstance.TryGetValue((cast.TargetId, cast.TargetInstance), out var instance)
            && LastAtOrBefore(instance, cast.Timestamp) is { } onInstance)
            return onInstance;

        return _samplesByTargetId.TryGetValue(cast.TargetId, out var unit)
            ? LastAtOrBefore(unit, cast.Timestamp)
            : null;
    }

    private static double? LastAtOrBefore(List<HealthSample> series, int timestamp)
    {
        double? found = null;
        foreach (var sample in series)
        {
            if (sample.Timestamp > timestamp)
                break;

            found = sample.HealthPercent;
        }

        return found;
    }

    private readonly record struct HealthSample(int Timestamp, double HealthPercent);

    private readonly record struct TrackedCast(int Timestamp, int TargetId, int TargetInstance);

    private record Computed(
        int? PhaseStart,
        int PhaseDurationMs,
        IReadOnlyList<CullingStrikeCast> Casts,
        int CastsInPhase,
        int CastsAboveThreshold,
        int PossibleCasts,
        double Coverage);
}

public sealed record CullingStrikeCast(
    int Timestamp,
    double? TargetHealthPercent,
    bool InExecutePhase,
    bool AboveThreshold);
