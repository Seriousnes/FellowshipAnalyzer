using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

public abstract partial class SlaughterUsageAnalyzer : Analyzer
{
    private const int OpenWoundsDurationMs = 18_000;

    public const int RendConsumeGraceMs = 500;

    private readonly Dictionary<TargetKey, OpenWoundsWindow> _openWindows = [];
    private readonly List<OpenWoundsWindow> _windows = [];
    private readonly List<SlaughterCast> _casts = [];

    private Projection Result => field ??= Project();

    public abstract GundePullShape Shape { get; }

    public List<SlaughterEvaluation> Slaughters => Result.Slaughters;

    public int SlaughterCasts => _casts.Count;

    public int OpenWoundsTimed => Result.OpenWoundsTimed;

    public int WellExecuted => Result.WellExecuted;

    public long TotalBleedDamage => Result.TotalBleedDamage;

    public int TotalRendConsumed => Result.TotalRendConsumed;

    public int TotalOpenWoundsWindows => Result.TotalWindows;

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
            _casts[^1].BleedDamage += @event.Amount;
    }

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Slaughter))]
    private void OnSlaughter(CastEvent @event) => _casts.Add(new SlaughterCast(@event.Timestamp));

    protected abstract bool IsWellExecuted(SlaughterEvaluation slaughter);

    private Projection Project()
    {
        var evaluations = new List<SlaughterEvaluation>(_casts.Count);
        foreach (var cast in _casts)
        {
            var evaluation = new SlaughterEvaluation
            {
                Timestamp = cast.Timestamp,
                OpenWoundsActive = IsInsideOpenWounds(cast.Timestamp),
                TargetsHit = cast.Targets.Count,
                RendConsumed = RendConsumedBy(cast.Timestamp),
                BleedDamage = cast.BleedDamage,
            };

            evaluations.Add(evaluation with { WellExecuted = IsWellExecuted(evaluation) });
        }

        var rated = RatedWindows();

        return new Projection(
            evaluations,
            evaluations.Count(slaughter => slaughter.OpenWoundsActive),
            evaluations.Count(slaughter => slaughter.WellExecuted),
            _casts.Sum(cast => cast.BleedDamage),
            evaluations.Sum(slaughter => slaughter.RendConsumed),
            rated.Count,
            rated.Count(window => !SawSlaughter(window)));
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

    private List<OpenWoundsWindow> RatedWindows() =>
        [.. _windows.Where(window => window.RemovedAt is not null || window.End <= Pull.EndTime || SawSlaughter(window))];

    private bool SawSlaughter(OpenWoundsWindow window) =>
        _casts.Any(cast => window.Covers(cast.Timestamp));

    private bool IsInsideOpenWounds(int timestamp) =>
        _windows.Any(window => window.Covers(timestamp));

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

    private sealed class SlaughterCast(int timestamp)
    {
        public int Timestamp { get; } = timestamp;
        public HashSet<TargetKey> Targets { get; } = [];
        public long BleedDamage { get; set; }
    }

    private sealed record Projection(
        List<SlaughterEvaluation> Slaughters,
        int OpenWoundsTimed,
        int WellExecuted,
        long TotalBleedDamage,
        int TotalRendConsumed,
        int TotalWindows,
        int WastedWindows);
}

[ForPull(PullKind.Single)]
public sealed class BossSlaughterUsage : SlaughterUsageAnalyzer
{
    public override GundePullShape Shape => GundePullShape.Boss;

    protected override bool IsWellExecuted(SlaughterEvaluation slaughter) =>
        slaughter.OpenWoundsActive;
}

[ForPull(PullKind.Multi)]
public sealed class TrashSlaughterUsage : SlaughterUsageAnalyzer
{
    public const int PackThreshold = 2;

    public override GundePullShape Shape => GundePullShape.Aoe;

    protected override bool IsWellExecuted(SlaughterEvaluation slaughter) =>
        slaughter.OpenWoundsActive && slaughter.TargetsHit >= PackThreshold;
}
