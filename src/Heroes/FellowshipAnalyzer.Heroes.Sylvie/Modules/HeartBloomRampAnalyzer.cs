using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<BlueyTracker>]
public sealed partial class HeartBloomRampAnalyzer : Analyzer
{
    private readonly List<HeartBloomCapture> _casts = [];

    private HeartBloomCapture? _open;
    private long _pendingEffective;
    private long _pendingOverheal;
    private int _pendingFrom;

    private List<HeartBloomCast> Result => field ??= Compute();

    public List<HeartBloomCast> Casts => Result;

    public int CastCount => Result.Count;

    public int CastsWithBlueyOnAlly => Result.Count(cast => cast.BlueyOnAlly);

    public int CastsWithoutBluey => Result.Count(cast => !cast.BlueyAssigned);

    public long TotalFed => Result.Sum(cast => cast.FlutterflyFed);

    public long TotalReleased => Result.Sum(cast => cast.TotalHealing);

    public int TotalReleases => Result.Sum(cast => cast.Releases);

    public int FullyReleasedCasts => Result.Count(cast => cast.Releases >= SylvieKit.HeartBloomReleases);

    [On<HealEvent>(By = Actor.Player, Spell = nameof(Spells.FluttercallHealHot))]
    private void OnFlutterflyHeal(HealEvent healEvent)
    {
        _pendingEffective += healEvent.Amount;
        _pendingOverheal += healEvent.Overheal ?? 0;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HeartBloom))]
    private void OnHeartBloomCast(CastEvent castEvent)
    {
        var posting = BlueyTracker.PostingAt(castEvent.Timestamp);
        var accumulatedFrom = _pendingFrom == 0 ? Pull.StartTime : _pendingFrom;

        _open = new HeartBloomCapture
        {
            Timestamp = castEvent.Timestamp,
            AccumulationMs = Math.Max(0, castEvent.Timestamp - accumulatedFrom),
            FedEffective = _pendingEffective,
            FedOverheal = _pendingOverheal,
            BlueyAssigned = posting is not null,
            BlueyOnAlly = posting is { OnSylvie: false },
        };
        _casts.Add(_open);

        _pendingEffective = 0;
        _pendingOverheal = 0;
        _pendingFrom = castEvent.Timestamp;
    }

    [On<HealEvent>(By = Actor.Player, Spell = nameof(Spells.HeartBloom))]
    private void OnHeartBloomHeal(HealEvent healEvent)
    {
        if (_open is null) return;

        _open.Record(healEvent.Timestamp, healEvent.Amount, healEvent.Overheal ?? 0);
    }

    private List<HeartBloomCast> Compute() =>
    [
        .. _casts.Select(capture => new HeartBloomCast(
            capture.Timestamp,
            capture.AccumulationMs,
            capture.FedEffective,
            capture.FedOverheal,
            capture.BlueyAssigned,
            capture.BlueyOnAlly,
            capture.ReleaseInstants.Count,
            capture.Effective,
            capture.Overheal))
    ];

    private sealed class HeartBloomCapture
    {
        public int Timestamp { get; init; }
        public int AccumulationMs { get; init; }
        public long FedEffective { get; init; }
        public long FedOverheal { get; init; }
        public bool BlueyAssigned { get; init; }
        public bool BlueyOnAlly { get; init; }
        public long Effective { get; private set; }
        public long Overheal { get; private set; }
        public HashSet<int> ReleaseInstants { get; } = [];

        public void Record(int timestamp, long effective, long overheal)
        {
            Effective += effective;
            Overheal += overheal;
            ReleaseInstants.Add((timestamp - Timestamp) / SylvieKit.HeartBloomTickIntervalMs);
        }
    }
}

public sealed record HeartBloomCast(
    int Timestamp,
    int AccumulationMs,
    long FlutterflyEffective,
    long FlutterflyOverheal,
    bool BlueyAssigned,
    bool BlueyOnAlly,
    int Releases,
    long Effective,
    long Overheal)
{
    public long FlutterflyFed => FlutterflyEffective + FlutterflyOverheal;

    public long TotalHealing => Effective + Overheal;

    public double OverhealShare => TotalHealing > 0 ? Overheal / (double)TotalHealing : 0;

    public bool ReleasedInFull => Releases >= SylvieKit.HeartBloomReleases;
}
