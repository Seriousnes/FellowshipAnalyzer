using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Sylvie;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// What each Heart Bloom was ramped with. Heart Bloom banks a share of the healing that came before it
/// and pays the bank back out over sixteen seconds, so the cast is worth whatever the flutterflies fed
/// it while it was on cooldown, and the state Bluey was in decides how much that was.
/// <para>
/// The healing fed and the healing paid out are both measured; the ratio between them is reported as
/// observed rather than asserted, because the log carries no bank reading and the game data's
/// accumulation scalar does not on its own account for what the payout is worth.
/// </para>
/// </summary>
[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<BlueyTracker>]
public sealed partial class HeartBloomRampAnalyzer : Analyzer
{
    private readonly List<HeartBloomCapture> _casts = [];

    private HeartBloomCapture? _open;
    private long _pendingEffective;
    private long _pendingOverheal;
    private int _pendingFrom;

    private IReadOnlyList<HeartBloomCast> Result => field ??= Compute();

    /// <summary>Every Heart Bloom cast this pull, in order.</summary>
    public IReadOnlyList<HeartBloomCast> Casts => Result;

    /// <summary>Heart Blooms cast this pull.</summary>
    public int CastCount => Result.Count;

    /// <summary>Heart Blooms cast with Bluey parked on an ally, where the flutterflies feed it hardest.</summary>
    public int CastsWithBlueyOnAlly => Result.Count(cast => cast.BlueyOnAlly);

    /// <summary>Heart Blooms cast with Bluey parked nowhere the log could see.</summary>
    public int CastsWithoutBluey => Result.Count(cast => !cast.BlueyAssigned);

    /// <summary>Flutterfly healing, overheal included, that fed the bank across this pull's casts.</summary>
    public long TotalFed => Result.Sum(cast => cast.FlutterflyFed);

    /// <summary>Healing, overheal included, that this pull's Heart Blooms paid out.</summary>
    public long TotalPaidOut => Result.Sum(cast => cast.TotalHealing);

    /// <summary>Payouts observed across this pull's casts, against <see cref="SylvieKit.HeartBloomPayouts"/> each.</summary>
    public int TotalPayouts => Result.Sum(cast => cast.Payouts);

    /// <summary>Casts that ran their full complement of payouts before the pull or the duration ended.</summary>
    public int FullyPaidOutCasts => Result.Count(cast => cast.Payouts >= SylvieKit.HeartBloomPayouts);

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

    private IReadOnlyList<HeartBloomCast> Compute() =>
    [
        .. _casts.Select(capture => new HeartBloomCast(
            capture.Timestamp,
            capture.AccumulationMs,
            capture.FedEffective,
            capture.FedOverheal,
            capture.BlueyAssigned,
            capture.BlueyOnAlly,
            capture.PayoutInstants.Count,
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
        public HashSet<int> PayoutInstants { get; } = [];

        public void Record(int timestamp, long effective, long overheal)
        {
            Effective += effective;
            Overheal += overheal;
            PayoutInstants.Add((timestamp - Timestamp) / SylvieKit.HeartBloomTickIntervalMs);
        }
    }
}

/// <summary>One Heart Bloom, what fed it, and what it paid back.</summary>
/// <param name="Timestamp">When it went out.</param>
/// <param name="AccumulationMs">How long it had been banking since the previous cast or the pull's start.</param>
/// <param name="FlutterflyEffective">Flutterfly healing that landed during the accumulation window.</param>
/// <param name="FlutterflyOverheal">Flutterfly healing lost to full bars during the accumulation window, which the bank still counts.</param>
/// <param name="BlueyAssigned">Whether Bluey was placed anywhere the log could see when the cast went out.</param>
/// <param name="BlueyOnAlly">Whether Bluey was on an ally rather than on Sylvie, the placement that feeds the flutterflies hardest.</param>
/// <param name="Payouts">Distinct payout intervals observed, against the eight the duration allows.</param>
/// <param name="Effective">Healing this cast landed.</param>
/// <param name="Overheal">Healing this cast lost to full bars.</param>
public sealed record HeartBloomCast(
    int Timestamp,
    int AccumulationMs,
    long FlutterflyEffective,
    long FlutterflyOverheal,
    bool BlueyAssigned,
    bool BlueyOnAlly,
    int Payouts,
    long Effective,
    long Overheal)
{
    /// <summary>Flutterfly healing, overheal included, that fed the bank before this cast.</summary>
    public long FlutterflyFed => FlutterflyEffective + FlutterflyOverheal;

    /// <summary>Healing this cast delivered, overheal included.</summary>
    public long TotalHealing => Effective + Overheal;

    /// <summary>Share (0-1) of this cast's own healing that was overheal.</summary>
    public double OverhealShare => TotalHealing > 0 ? Overheal / (double)TotalHealing : 0;

    /// <summary>Whether every payout the duration allows was observed.</summary>
    public bool PaidOutInFull => Payouts >= SylvieKit.HeartBloomPayouts;
}
