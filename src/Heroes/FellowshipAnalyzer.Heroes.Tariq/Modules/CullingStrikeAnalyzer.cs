using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Tariq;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>
/// Measures how well Tariq kept Culling Strike rolling through a boss's execute phase. Culling Strike
/// is gated on target health, so it only becomes a rotational button once the boss is inside the last
/// <see cref="ExecuteHealthThreshold"/> of its health bar; from that point it is a cooldown that should
/// be pressed every time it comes back, and every cycle it sits idle is free damage forfeited.
/// </summary>
/// <remarks>
/// The execute phase is reconstructed from target health snapshots, because nothing in the log declares
/// it. Every player <see cref="DamageEvent"/> carrying a target health reading is a sample; samples with
/// no maximum, or with more health than the target can hold, are discarded before anything reads them,
/// since real logs carry occasional garbage of both kinds. The primary target is the unit with the most
/// surviving samples, so an add that the player cleaved through can never displace the boss, and the
/// phase runs from that target's first sample at or below the threshold to the pull's end - a boss dies
/// at the pull boundary, so the phase never closes early.
/// <para>
/// Casts above the threshold are only possible while Executioner's Grin (from the legendary
/// Executioner's Unsanitary Bands) is up. They are counted on their own and deliberately excluded from
/// <see cref="CastsInPhase"/>: an above-threshold cast is item value being extracted, never a discipline
/// failure, and folding it into the phase count would credit or blame the wrong thing.
/// </para>
/// <para>
/// The ability's Fury requirement is not modeled. Culling Strike spends up to 10 Fury for a damage bonus
/// per point, and a rotation holds well above that between spenders, so a cast being unaffordable is not
/// a realistic explanation for a missed cycle.
/// </para>
/// </remarks>
[ForPull(PullKind.Single, Boss = PullBoss.Boss)]
public sealed partial class CullingStrikeAnalyzer : Analyzer
{
    /// <summary>
    /// Target health fraction at or below which Culling Strike is castable unaided. The value is the
    /// ability's own low-health gate (DevName <c>GA_Ink_LowHealthSingleTargetResourceDamage</c>) and is
    /// the same 30 percent <c>ExecutionersGrinTracker</c> reads Culling Strike hits against.
    /// </summary>
    public const double ExecuteHealthThreshold = 0.30;

    /// <summary>
    /// Milliseconds the model budgets per execute-phase Culling Strike: the ability's 6 second cooldown
    /// plus a second of reaction. That cooldown is haste-scaled, so a real rotation fits more casts into
    /// a phase than this interval implies. The undercount is deliberate - the model may only ever ask for
    /// fewer casts than the pull could have produced, never more.
    /// </summary>
    public const int ExpectedCastIntervalMs = 7000;

    private readonly Dictionary<(int TargetId, int TargetInstance), List<HealthSample>> _samplesByInstance = [];
    private readonly Dictionary<int, List<HealthSample>> _samplesByTargetId = [];
    private readonly List<TrackedCast> _casts = [];

    private Computed? _computed;
    private Computed Result => _computed ??= Compute();

    /// <summary>
    /// When the primary target was first observed at or below <see cref="ExecuteHealthThreshold"/>, or
    /// null when it never was and the pull therefore has no execute phase to judge.
    /// </summary>
    public int? ExecutePhaseStartTimestamp => Result.PhaseStart;

    /// <summary>Milliseconds from the execute phase opening to the pull ending; zero without a phase.</summary>
    public int ExecutePhaseDurationMs => Result.PhaseDurationMs;

    /// <summary>Every Culling Strike cast in the pull, in the order it was cast.</summary>
    public IReadOnlyList<CullingStrikeCast> Casts => Result.Casts;

    /// <summary>Culling Strike casts in the pull, above and below the threshold alike.</summary>
    public int TotalCasts => Result.Casts.Count;

    /// <summary>
    /// Casts that landed inside the execute phase on a target at or below the threshold - the casts the
    /// phase's discipline is measured on.
    /// </summary>
    public int CastsInPhase => Result.CastsInPhase;

    /// <summary>
    /// Casts on a target above the threshold, which only Executioner's Grin makes possible. Item value,
    /// counted separately and never held against the phase.
    /// </summary>
    public int CastsAboveThreshold => Result.CastsAboveThreshold;

    /// <summary>
    /// Casts the execute phase had room for: whole <see cref="ExpectedCastIntervalMs"/> cycles in the
    /// phase, or the casts actually made when the player beat that pace.
    /// </summary>
    public int PossibleCasts => Result.PossibleCasts;

    /// <summary>
    /// Share (0-1) of the phase's possible casts that were made. A pull with no execute phase has nothing
    /// to miss and reads as 1.
    /// </summary>
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

    /// <summary>
    /// Reconstructs the phase and classifies every cast against it. Computed once, on first read, so a
    /// guide reading half a dozen properties still walks the samples a single time.
    /// </summary>
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

    /// <summary>
    /// The cast target's health at the cast, read from its most recent surviving sample. Instances fall
    /// back to the unit's samples as a whole, because a cast and the damage it produces do not reliably
    /// agree on whether a target instance is carried at all.
    /// </summary>
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

    private readonly record struct Computed(
        int? PhaseStart,
        int PhaseDurationMs,
        IReadOnlyList<CullingStrikeCast> Casts,
        int CastsInPhase,
        int CastsAboveThreshold,
        int PossibleCasts,
        double Coverage);
}

/// <summary>One Culling Strike cast, classified against the reconstructed execute phase.</summary>
/// <param name="Timestamp">Fight-relative timestamp of the cast.</param>
/// <param name="TargetHealthPercent">
/// The target's health fraction at the cast, from its most recent surviving sample, or null when nothing
/// the player did to that unit carried a usable health reading beforehand.
/// </param>
/// <param name="InExecutePhase">Whether the cast fell inside the reconstructed execute phase.</param>
/// <param name="AboveThreshold">
/// Whether the target was above <see cref="CullingStrikeAnalyzer.ExecuteHealthThreshold"/>, which only
/// Executioner's Grin allows. A cast with no health reading is not treated as one.
/// </param>
public sealed record CullingStrikeCast(
    int Timestamp,
    double? TargetHealthPercent,
    bool InExecutePhase,
    bool AboveThreshold);
