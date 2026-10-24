using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<BlueyTracker>]
[Dependency<Haste>]
public sealed partial class HeartBloomRampAnalyzer : Analyzer
{
    private readonly List<HeartBloomCapture> _casts = [];

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

        var pulsePeriodMs = PulsePeriodAt(Haste.Current);

        _casts.Add(new HeartBloomCapture
        {
            Timestamp = castEvent.Timestamp,
            AccumulationMs = Math.Max(0, castEvent.Timestamp - accumulatedFrom),
            FedEffective = _pendingEffective,
            FedOverheal = _pendingOverheal,
            BlueyAssigned = posting is not null,
            BlueyOnAlly = posting is { OnSylvie: false },
            PulsePeriodMs = pulsePeriodMs,
            LastPulseAt = castEvent.Timestamp,
            NextPulseAt = castEvent.Timestamp + pulsePeriodMs,
        });

        _pendingEffective = 0;
        _pendingOverheal = 0;
        _pendingFrom = castEvent.Timestamp;
    }

    [On<HealEvent>(By = Actor.Player, Spell = nameof(Spells.HeartBloom))]
    private void OnHeartBloomHeal(HealEvent healEvent)
    {
        var bloom = ReleasingBloom(healEvent.Timestamp);
        if (bloom is null) return;

        bloom.Record(healEvent.Timestamp, healEvent.Amount, healEvent.Overheal ?? 0);
        bloom.Pulsed(healEvent.Timestamp, PulsePeriodAt(Haste.Current));
    }

    [On<ChangeHasteEvent>]
    private void OnChangeHaste(ChangeHasteEvent changeHasteEvent)
    {
        var pulsePeriodMs = PulsePeriodAt(changeHasteEvent.NewHaste ?? Haste.Current);

        foreach (var capture in Alive(changeHasteEvent.Timestamp))
        {
            capture.Rescale(changeHasteEvent.Timestamp, pulsePeriodMs);
        }
    }

    private static double PulsePeriodAt(double haste) => SylvieKit.HeartBloomTickIntervalMs / (1.0 + haste);

    /// <summary>
    /// The cast whose Heart Bloom released the pulse at <paramref name="timestamp"/>. With two charges two
    /// Heart Blooms are active at once. The releasing bloom is the active bloom with the least time between
    /// <paramref name="timestamp"/> and its last or next expected pulse. On a tie it is the bloom with no release
    /// recorded for that pulse, then the older bloom. A Haste change re-times each active bloom's next expected pulse
    /// from its remaining pulse progress.
    /// After every bloom has withered, the releasing bloom is the latest cast's.
    /// </summary>
    private HeartBloomCapture? ReleasingBloom(int timestamp)
    {
        var alive = Alive(timestamp).ToList();

        foreach (var capture in alive)
        {
            capture.AdvanceTo(timestamp);
        }

        return alive.Count > 0
            ? alive
                .OrderBy(capture => capture.PulseOffset(timestamp))
                .ThenBy(capture => capture.HasReleasedAt(timestamp))
                .First()
            : _casts.LastOrDefault();
    }

    private IEnumerable<HeartBloomCapture> Alive(int timestamp) => _casts
        .Where(capture => capture.Timestamp <= timestamp
            && timestamp - capture.Timestamp <= SylvieKit.HeartBloomDurationMs + (capture.PulsePeriodMs / 2));

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
        public double PulsePeriodMs { get; set; }
        public double LastPulseAt { get; set; }
        public double NextPulseAt { get; set; }
        public long Effective { get; private set; }
        public long Overheal { get; private set; }
        public HashSet<int> ReleaseInstants { get; } = [];

        public void Record(int timestamp, long effective, long overheal)
        {
            Effective += effective;
            Overheal += overheal;
            ReleaseInstants.Add((timestamp - Timestamp) / SylvieKit.HeartBloomTickIntervalMs);
        }

        public bool HasReleasedAt(int timestamp) =>
            ReleaseInstants.Contains((timestamp - Timestamp) / SylvieKit.HeartBloomTickIntervalMs);

        public void AdvanceTo(int timestamp)
        {
            while (NextPulseAt + (PulsePeriodMs / 2) < timestamp)
            {
                LastPulseAt = NextPulseAt;
                NextPulseAt += PulsePeriodMs;
            }
        }

        public double PulseOffset(int timestamp) =>
            Math.Min(Math.Abs(timestamp - LastPulseAt), Math.Abs(timestamp - NextPulseAt));

        public void Pulsed(int timestamp, double pulsePeriodMs)
        {
            if (Math.Abs(timestamp - NextPulseAt) > Math.Abs(timestamp - LastPulseAt)) return;

            LastPulseAt = timestamp;
            NextPulseAt = timestamp + pulsePeriodMs;
            PulsePeriodMs = pulsePeriodMs;
        }

        public void Rescale(int timestamp, double pulsePeriodMs)
        {
            while (NextPulseAt < timestamp)
            {
                LastPulseAt = NextPulseAt;
                NextPulseAt += PulsePeriodMs;
            }

            var remainingProgress = (NextPulseAt - timestamp) / PulsePeriodMs;
            NextPulseAt = timestamp + (remainingProgress * pulsePeriodMs);
            PulsePeriodMs = pulsePeriodMs;
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
