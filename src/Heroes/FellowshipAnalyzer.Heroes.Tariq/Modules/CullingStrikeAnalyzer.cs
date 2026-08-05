using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

[ForPull(PullKind.Single, Boss = PullBoss.Boss)]
public sealed partial class CullingStrikeAnalyzer : Analyzer
{
    public const double ExecuteHealthThreshold = 0.30;

    public const int FullStrengthFury = 10;

    public const int PromptCastGraceMs = 1500;

    private readonly Dictionary<(int TargetId, int TargetInstance), List<HealthSample>> _samplesByInstance = [];
    private readonly Dictionary<int, List<HealthSample>> _samplesByTargetId = [];
    private readonly List<TrackedCast> _casts = [];
    private readonly List<(int Start, int? End)> _grinWindows = [];
    private readonly List<(int Timestamp, bool Available)> _availability = [];
    private readonly List<(int Timestamp, int Fury)> _fury = [];

    private Computed Result => field ??= Compute();

    public int? ExecutePhaseStartTimestamp => Result.PhaseStart;

    public int ExecutePhaseDurationMs => Result.PhaseDurationMs;

    public IReadOnlyList<CullingStrikeCast> Casts => Result.Casts;

    public int TotalCasts => Result.Casts.Count;

    public int CastsInPhase => Result.CastsInPhase;

    public int CastsAboveThreshold => Result.CastsAboveThreshold;

    public IReadOnlyList<CullingStrikeOpportunity> Opportunities => Result.Opportunities;

    public int PromptCasts => Result.Opportunities.Count(opportunity => opportunity.Prompt);

    public int HeldWithFury => Result.Opportunities.Count(opportunity => !opportunity.Prompt && opportunity.HadFury);

    public int HeldWithoutFury => Result.Opportunities.Count(opportunity => !opportunity.Prompt && !opportunity.HadFury);

    public int IdleMs => Result.Opportunities.Sum(opportunity => Math.Max(0, opportunity.HeldMs - PromptCastGraceMs));

    public int UnexplainedCastsAboveThreshold => Result.UnexplainedAboveThreshold;

    public int CastsWithoutHealthReading => Result.Casts.Count(cast => cast.TargetHealthPercent is null);

    [On<DamageEvent>(By = Actor.Player)]
    private void OnDamage(DamageEvent @event)
    {
        RecordFury(@event.Timestamp, @event.SourceResources);

        var resources = @event.TargetResources;
        if (resources is null || resources.MaxHitPoints <= 0 || resources.HitPoints > resources.MaxHitPoints)
            return;

        var sample = new HealthSample(@event.Timestamp, (double)resources.HitPoints / resources.MaxHitPoints);
        SeriesFor(_samplesByInstance, (@event.TargetId, @event.TargetInstance ?? 0)).Add(sample);
        SeriesFor(_samplesByTargetId, @event.TargetId).Add(sample);
    }

    [On<CastEvent>(By = Actor.Player)]
    private void OnAnyCast(CastEvent @event) => RecordFury(@event.Timestamp, @event.SourceResources);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.CullingStrike))]
    private void OnCullingStrikeCast(CastEvent @event)
    {
        if (@event.Fake)
            return;

        _casts.Add(new TrackedCast(@event.Timestamp, @event.TargetId, @event.TargetInstance ?? 0));
    }

    [On<UpdateSpellUsableEvent>(By = Actor.Player, Spell = nameof(Spells.CullingStrike))]
    private void OnUsableChanged(UpdateSpellUsableEvent @event) =>
        _availability.Add((@event.Timestamp, @event.IsAvailable));

    [On<ApplyBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ExecutionersGrin))]
    private void OnGrinApplied(ApplyBuffEvent @event) => _grinWindows.Add((@event.Timestamp, null));

    [On<RemoveBuffEvent>(To = Actor.Player, Spell = nameof(Spells.ExecutionersGrin))]
    private void OnGrinRemoved(RemoveBuffEvent @event)
    {
        for (var i = _grinWindows.Count - 1; i >= 0; i--)
        {
            if (_grinWindows[i].End is not null)
                continue;

            _grinWindows[i] = (_grinWindows[i].Start, @event.Timestamp);
            return;
        }
    }

    private void RecordFury(int timestamp, ActorResources? resources)
    {
        if (FuryPercent(resources) is not { } fury)
            return;

        if (_fury.Count > 0 && _fury[^1].Timestamp == timestamp)
            _fury[^1] = (timestamp, fury);
        else
            _fury.Add((timestamp, fury));
    }

    private static int? FuryPercent(ActorResources? resources)
    {
        if (resources?.Resources is not { } pools)
            return null;

        foreach (var pool in pools)
        {
            if (pool.Type != ResourceTypes.Primary)
                continue;

            return pool.Max > 0 ? (int)Math.Clamp(Math.Round(pool.Amount * 100.0 / pool.Max), 0, 100) : null;
        }

        return null;
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
            var aboveThreshold = health is { } percent && percent > ExecuteHealthThreshold;
            casts.Add(new CullingStrikeCast(
                cast.Timestamp,
                health,
                phaseStart is { } opened && cast.Timestamp >= opened,
                aboveThreshold,
                GrinActiveAt(cast.Timestamp)));
        }

        return new Computed(
            phaseStart,
            phaseDuration,
            casts,
            casts.Count(cast => cast.InExecutePhase && !cast.AboveThreshold),
            casts.Count(cast => cast.AboveThreshold),
            casts.Count(cast => cast.AboveThreshold && !cast.GrinActive),
            BuildOpportunities(phaseStart));
    }

    private List<CullingStrikeOpportunity> BuildOpportunities(int? phaseStart)
    {
        if (phaseStart is not { } start)
            return [];

        var opportunities = new List<CullingStrikeOpportunity>();
        var available = true;
        var readyAt = Pull.StartTime;

        foreach (var (timestamp, nowAvailable) in _availability)
        {
            if (available && !nowAvailable)
                Add(readyAt, timestamp);

            if (!available && nowAvailable)
                readyAt = timestamp;

            available = nowAvailable;
        }

        if (available)
            Add(readyAt, Pull.EndTime);

        return opportunities;

        void Add(int windowStart, int windowEnd)
        {
            var clipped = Math.Max(windowStart, start);
            if (windowEnd < clipped)
                return;

            var castAt = FirstCastIn(clipped, windowEnd);

            if (castAt is null && windowEnd <= clipped)
                return;

            opportunities.Add(new CullingStrikeOpportunity
            {
                ReadyAt = clipped,
                HeldMs = (castAt ?? windowEnd) - clipped,
                FuryAtReady = FuryAt(clipped),
                CastAt = castAt,
            });
        }
    }

    private int? FirstCastIn(int from, int to)
    {
        foreach (var cast in _casts)
        {
            if (cast.Timestamp < from)
                continue;
            if (cast.Timestamp > to)
                break;

            return cast.Timestamp;
        }

        return null;
    }

    private int? FuryAt(int timestamp)
    {
        int? found = null;
        foreach (var (sampleTimestamp, fury) in _fury)
        {
            if (sampleTimestamp > timestamp)
                break;

            found = fury;
        }

        return found;
    }

    private bool GrinActiveAt(int timestamp)
    {
        foreach (var (start, end) in _grinWindows)
        {
            if (timestamp >= start && timestamp <= (end ?? Pull.EndTime))
                return true;
        }

        return false;
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
        int UnexplainedAboveThreshold,
        IReadOnlyList<CullingStrikeOpportunity> Opportunities);
}

public sealed record CullingStrikeCast(
    int Timestamp,
    double? TargetHealthPercent,
    bool InExecutePhase,
    bool AboveThreshold,
    bool GrinActive);

public sealed record CullingStrikeOpportunity
{
    public required int ReadyAt { get; init; }

    public required int HeldMs { get; init; }

    public required int? FuryAtReady { get; init; }

    public required int? CastAt { get; init; }

    public bool Prompt => CastAt is not null && HeldMs <= CullingStrikeAnalyzer.PromptCastGraceMs;

    public bool HadFury => FuryAtReady is null or >= CullingStrikeAnalyzer.FullStrengthFury;
}
