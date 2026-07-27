using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Evaluates how well each Slaughter is set up and how much bleed it had to cash. Slaughter consumes
/// all of the Rend standing on every enemy it hits and reapplies it as a short bleed, so its value is
/// maximised when it is cast (a) inside the Open Wounds window that Rupture leaves behind, buffing
/// the next Slaughter for 18s, and (b) after Heart Splitter has been used to build Rend since the
/// previous Slaughter. The shape-specialised leaves add the extra success criterion their rotation
/// calls for. Each cast is judged on those conditions alone, all of which read the same on any gear.
/// </summary>
/// <remarks>
/// <para>
/// Rupture applies Open Wounds to the enemies it hits, and the next Slaughter on such an enemy
/// consumes the debuff, which the log shows as the debuff being removed within a millisecond or two
/// of that cast; a window nobody cashes in expires 18s after it went up. That is the reading live
/// Season 3 log data bears out. The analyzer keys on the Open Wounds debuff itself rather than on
/// Rupture casts, so it holds across builds and Open Wounds sources, and it tracks a window per
/// (TargetId, TargetInstance), the unit the game applies a debuff to. Windows and per-cast verdicts
/// are projected once on first read, after the pull has closed, so a single containment test drives
/// both the per-cast Open Wounds flag and the wasted-window count.
/// </para>
/// <para>
/// Two sizes are recorded alongside the verdict. The Rend consumed comes from
/// <see cref="RendStackTracker"/>: Slaughter's consumption reaches the log as a burst of Rend
/// removals sharing the cast's millisecond, so the removals inside <see cref="RendConsumeGraceMs"/>
/// of a cast are the pile it took. That is a stack count and reads the same on any gear. The bleed
/// damage that follows is attributed back to the cast as well, but it scales with gear and is
/// therefore evidence rather than a verdict.
/// </para>
/// </remarks>
public abstract partial class SlaughterUsageAnalyzer : Analyzer
{
    private const int OpenWoundsDurationMs = 18_000;

    /// <summary>
    /// Window after a Slaughter cast in which a Rend removal is that cast consuming the bleed. Live
    /// Season 3 data puts the whole burst of removals inside a few hundred milliseconds of the cast.
    /// </summary>
    public const int RendConsumeGraceMs = 500;

    private readonly Dictionary<TargetKey, OpenWoundsWindow> _openWindows = [];
    private readonly List<OpenWoundsWindow> _windows = [];
    private readonly List<SlaughterCast> _casts = [];

    private int _lastHeartSplitterTimestamp = int.MinValue;
    private int _previousSlaughterTimestamp = int.MinValue;

    private Projection? _projection;
    private Projection Result => _projection ??= Project();

    /// <summary>The pull shape this leaf scores against.</summary>
    public abstract GundePullShape Shape { get; }

    /// <summary>Every Slaughter cast on the pull, in cast order, with its per-cast evaluation.</summary>
    public IReadOnlyList<SlaughterEvaluation> Slaughters => Result.Slaughters;

    public int SlaughterCasts => _casts.Count;

    /// <summary>Slaughters cast while a Rupture Open Wounds window was active.</summary>
    public int OpenWoundsTimed => Result.OpenWoundsTimed;

    /// <summary>Slaughters preceded by a Heart Splitter since the previous Slaughter.</summary>
    public int HeartSplitterPrimed => Result.HeartSplitterPrimed;

    /// <summary>Slaughters that met the success bar for this pull shape.</summary>
    public int WellExecuted => Result.WellExecuted;

    /// <summary>Slaughter bleed damage across every cast on the pull.</summary>
    public long TotalPayoffDamage => Result.TotalPayoffDamage;

    /// <summary>Rend stacks every Slaughter on the pull consumed between them.</summary>
    public int TotalRendConsumed => Result.TotalRendConsumed;

    /// <summary>
    /// Open Wounds windows the pull can be judged on: every window that closed, plus every window
    /// still open at the pull's end that either saw a Slaughter or ran its full 18s inside the pull.
    /// A Rupture landed just before the kill leaves a window that never had time to be used, so it
    /// is excluded from both this count and <see cref="WastedOpenWoundsWindows"/>.
    /// </summary>
    public int TotalOpenWoundsWindows => Result.TotalWindows;

    /// <summary>Judged Open Wounds windows that closed without a Slaughter cashing the bonus in.</summary>
    public int WastedOpenWoundsWindows => Result.WastedWindows;

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.OpenWounds))]
    private void OnOpenWoundsApplied(ApplyDebuffEvent @event) => OpenWindow(Key(@event), @event.Timestamp);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.OpenWounds))]
    private void OnOpenWoundsRefreshed(RefreshDebuffEvent @event) => RefreshWindow(Key(@event), @event.Timestamp);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.OpenWounds))]
    private void OnOpenWoundsRemoved(RemoveDebuffEvent @event)
    {
        if (_openWindows.Remove(Key(@event), out var window))
            window.RemovedAt = @event.Timestamp;
    }

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SlaughterDot))]
    private void OnSlaughterDotApplied(ApplyDebuffEvent @event) => TrackBleedTarget(@event);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SlaughterDot))]
    private void OnSlaughterDotRefreshed(RefreshDebuffEvent @event) => TrackBleedTarget(@event);

    [On<DamageEvent>(By = Actor.Player, Spell = nameof(Spells.SlaughterDot))]
    private void OnSlaughterDotDamage(DamageEvent @event)
    {
        if (_casts.Count > 0)
            _casts[^1].PayoffDamage += @event.Amount;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HeartSplitter))]
    private void OnHeartSplitter(CastEvent @event) => _lastHeartSplitterTimestamp = @event.Timestamp;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Slaughter))]
    private void OnSlaughter(CastEvent @event)
    {
        _casts.Add(new SlaughterCast(@event.Timestamp, _lastHeartSplitterTimestamp > _previousSlaughterTimestamp));
        _previousSlaughterTimestamp = @event.Timestamp;
    }

    /// <summary>Whether a Slaughter met the success bar for this pull shape.</summary>
    protected abstract bool IsWellExecuted(SlaughterEvaluation slaughter);

    /// <summary>
    /// Turns the retained casts and windows into every figure this analyzer reports. Computed once,
    /// on first read, by which point the pull has closed and no window can move again.
    /// </summary>
    private Projection Project()
    {
        var evaluations = new List<SlaughterEvaluation>(_casts.Count);
        foreach (var cast in _casts)
        {
            var evaluation = new SlaughterEvaluation
            {
                Timestamp = cast.Timestamp,
                OpenWoundsActive = IsInsideOpenWounds(cast.Timestamp),
                HeartSplitterPrimed = cast.HeartSplitterPrimed,
                TargetsHit = cast.Targets.Count,
                RendConsumed = RendConsumedBy(cast.Timestamp),
                PayoffDamage = cast.PayoffDamage,
            };

            evaluations.Add(evaluation with { WellExecuted = IsWellExecuted(evaluation) });
        }

        var judged = JudgedWindows();

        return new Projection(
            evaluations,
            evaluations.Count(slaughter => slaughter.OpenWoundsActive),
            evaluations.Count(slaughter => slaughter.HeartSplitterPrimed),
            evaluations.Count(slaughter => slaughter.WellExecuted),
            _casts.Sum(cast => cast.PayoffDamage),
            evaluations.Sum(slaughter => slaughter.RendConsumed),
            judged.Count,
            judged.Count(window => !SawSlaughter(window)));
    }

    private void TrackBleedTarget(IHasTargetWithInstanceEvent target)
    {
        if (_casts.Count > 0)
            _casts[^1].Targets.Add(Key(target));
    }

    private void OpenWindow(TargetKey target, int timestamp)
    {
        if (_openWindows.TryGetValue(target, out var stale))
            stale.RemovedAt = Math.Min(stale.End, timestamp);

        var window = new OpenWoundsWindow(timestamp);
        _windows.Add(window);
        _openWindows[target] = window;
    }

    private void RefreshWindow(TargetKey target, int timestamp)
    {
        if (_openWindows.TryGetValue(target, out var window) && timestamp <= window.End)
            window.LastRefresh = timestamp;
        else
            OpenWindow(target, timestamp);
    }

    private List<OpenWoundsWindow> JudgedWindows() =>
        [.. _windows.Where(window => window.RemovedAt is not null || window.End <= Pull.EndTime || SawSlaughter(window))];

    private bool SawSlaughter(OpenWoundsWindow window) =>
        _casts.Any(cast => window.Covers(cast.Timestamp));

    private bool IsInsideOpenWounds(int timestamp) =>
        _windows.Any(window => window.Covers(timestamp));

    /// <summary>
    /// Rend the cast at <paramref name="timestamp"/> consumed, taken from the removals the tracker
    /// recorded in the moments after it. The tracker runs for the whole fight, so the window also
    /// keeps a neighbouring pull's removals out of this pull's reading. The lookup is ad-hoc rather
    /// than a declared dependency because this is an abstract base whose sealed leaves would each
    /// have to forward a generated constructor.
    /// </summary>
    private int RendConsumedBy(int timestamp) =>
        Owner.GetModule<RendStackTracker>() is not { } tracker
            ? 0
            : tracker.Removals
                .Where(removal => removal.Timestamp >= timestamp && removal.Timestamp - timestamp <= RendConsumeGraceMs)
                .Sum(removal => removal.Stacks);

    private static TargetKey Key(IHasTargetWithInstanceEvent target) =>
        new(target.TargetId, target.TargetInstance ?? 0);

    private readonly record struct TargetKey(int TargetId, int TargetInstance);

    private sealed class OpenWoundsWindow(int start)
    {
        public int Start { get; } = start;
        public int LastRefresh { get; set; } = start;
        public int? RemovedAt { get; set; }
        public int End => RemovedAt ?? LastRefresh + OpenWoundsDurationMs;

        public bool Covers(int timestamp) => timestamp >= Start && timestamp <= End;
    }

    private sealed class SlaughterCast(int timestamp, bool heartSplitterPrimed)
    {
        public int Timestamp { get; } = timestamp;
        public bool HeartSplitterPrimed { get; } = heartSplitterPrimed;
        public HashSet<TargetKey> Targets { get; } = [];
        public long PayoffDamage { get; set; }
    }

    private sealed record Projection(
        IReadOnlyList<SlaughterEvaluation> Slaughters,
        int OpenWoundsTimed,
        int HeartSplitterPrimed,
        int WellExecuted,
        long TotalPayoffDamage,
        int TotalRendConsumed,
        int TotalWindows,
        int WastedWindows);
}

/// <summary>
/// Boss (single-target) Slaughter scoring: a Slaughter counts when it lands inside an Open Wounds
/// window and Heart Splitter has been used to rebuild Rend since the previous Slaughter.
/// </summary>
[ForPull(PullKind.Single)]
public sealed class BossSlaughterUsage : SlaughterUsageAnalyzer
{
    public override GundePullShape Shape => GundePullShape.Boss;

    protected override bool IsWellExecuted(SlaughterEvaluation slaughter) =>
        slaughter.OpenWoundsActive && slaughter.HeartSplitterPrimed;
}

/// <summary>
/// Trash (AoE) Slaughter scoring: a Slaughter counts when it lands inside an Open Wounds window and
/// its bleed spreads across the pack (two or more enemies).
/// </summary>
[ForPull(PullKind.Multi)]
public sealed class TrashSlaughterUsage : SlaughterUsageAnalyzer
{
    /// <summary>Minimum enemies a Slaughter bleed must reach to count as spread across the pack.</summary>
    public const int PackThreshold = 2;

    public override GundePullShape Shape => GundePullShape.Aoe;

    protected override bool IsWellExecuted(SlaughterEvaluation slaughter) =>
        slaughter.OpenWoundsActive && slaughter.TargetsHit >= PackThreshold;
}
