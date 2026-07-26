using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Evaluates how well each Slaughter is set up and what it paid out. Slaughter consumes all of the
/// Rend standing on every enemy it hits and reapplies it as a short 160% bleed, so its value is
/// maximised when it is cast (a) inside the Open Wounds window that Rupture leaves behind, buffing
/// the next Slaughter for 18s, and (b) after Heart Splitter has been used to build Rend since the
/// previous Slaughter. The shape-specialised leaves add the extra success criterion their rotation
/// calls for. The bleed damage that follows each cast is attributed back to it, which measures the
/// payoff directly rather than inferring it.
/// </summary>
/// <remarks>
/// The analyzer keys on the actual Open Wounds debuff rather than inferring the window from Rupture
/// casts, so it holds across builds and Open Wounds sources. Windows and per-cast verdicts are
/// projected when read, after the pull has closed, so a single containment test drives both the
/// per-cast Open Wounds flag and the wasted-window count.
/// </remarks>
public abstract partial class SlaughterUsageAnalyzer : Analyzer
{
    private const int OpenWoundsDurationMs = 18_000;

    private readonly Dictionary<int, OpenWoundsWindow> _openWindows = [];
    private readonly List<OpenWoundsWindow> _windows = [];
    private readonly List<SlaughterCast> _casts = [];

    private int _lastHeartSplitterTimestamp = int.MinValue;
    private int _previousSlaughterTimestamp = int.MinValue;

    /// <summary>The pull shape this leaf scores against.</summary>
    public abstract GundePullShape Shape { get; }

    /// <summary>Every Slaughter cast on the pull, in cast order, with its per-cast evaluation.</summary>
    public IReadOnlyList<SlaughterEvaluation> Slaughters => BuildEvaluations();

    public int SlaughterCasts => _casts.Count;

    /// <summary>Slaughters cast while a Rupture Open Wounds window was active.</summary>
    public int OpenWoundsTimed => _casts.Count(cast => IsInsideOpenWounds(cast.Timestamp));

    /// <summary>Slaughters preceded by a Heart Splitter since the previous Slaughter.</summary>
    public int HeartSplitterPrimed => _casts.Count(cast => cast.HeartSplitterPrimed);

    /// <summary>Slaughters that met the success bar for this pull shape.</summary>
    public int WellExecuted => Slaughters.Count(slaughter => slaughter.WellExecuted);

    /// <summary>Slaughter bleed damage across every cast on the pull.</summary>
    public long TotalPayoffDamage => _casts.Sum(cast => cast.PayoffDamage);

    /// <summary>
    /// The largest payoff any single Slaughter on this pull produced, the yardstick the guide scores
    /// the other casts against. Zero when no Slaughter bleed damage was logged.
    /// </summary>
    public long BestPayoff => _casts.Count == 0 ? 0 : _casts.Max(cast => cast.PayoffDamage);

    /// <summary>
    /// Open Wounds windows the pull can be judged on: every window that closed, plus every window
    /// still open at the pull's end that either saw a Slaughter or ran its full 18s inside the pull.
    /// A Rupture landed just before the kill leaves a window that never had time to be used, so it
    /// is excluded from both this count and <see cref="WastedOpenWoundsWindows"/>.
    /// </summary>
    public int TotalOpenWoundsWindows => JudgedWindows().Count();

    /// <summary>Judged Open Wounds windows that closed without a Slaughter cashing the bonus in.</summary>
    public int WastedOpenWoundsWindows => JudgedWindows().Count(window => !SawSlaughter(window));

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.OpenWounds))]
    private void OnOpenWoundsApplied(ApplyDebuffEvent @event) => OpenWindow(@event.TargetId, @event.Timestamp);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.OpenWounds))]
    private void OnOpenWoundsRefreshed(RefreshDebuffEvent @event) => RefreshWindow(@event.TargetId, @event.Timestamp);

    [On<RemoveDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.OpenWounds))]
    private void OnOpenWoundsRemoved(RemoveDebuffEvent @event)
    {
        if (_openWindows.Remove(@event.TargetId, out var window))
            window.RemovedAt = @event.Timestamp;
    }

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SlaughterDot))]
    private void OnSlaughterDotApplied(ApplyDebuffEvent @event) => TrackBleedTarget(@event.TargetId);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SlaughterDot))]
    private void OnSlaughterDotRefreshed(RefreshDebuffEvent @event) => TrackBleedTarget(@event.TargetId);

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

    private List<SlaughterEvaluation> BuildEvaluations()
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
                PayoffDamage = cast.PayoffDamage,
            };

            evaluations.Add(evaluation with { WellExecuted = IsWellExecuted(evaluation) });
        }

        return evaluations;
    }

    private void TrackBleedTarget(int targetId)
    {
        if (_casts.Count > 0)
            _casts[^1].Targets.Add(targetId);
    }

    private void OpenWindow(int targetId, int timestamp)
    {
        if (_openWindows.TryGetValue(targetId, out var stale))
            stale.RemovedAt = Math.Min(stale.End, timestamp);

        var window = new OpenWoundsWindow(timestamp);
        _windows.Add(window);
        _openWindows[targetId] = window;
    }

    private void RefreshWindow(int targetId, int timestamp)
    {
        if (_openWindows.TryGetValue(targetId, out var window) && timestamp <= window.End)
            window.LastRefresh = timestamp;
        else
            OpenWindow(targetId, timestamp);
    }

    private IEnumerable<OpenWoundsWindow> JudgedWindows() =>
        _windows.Where(window => window.RemovedAt is not null || window.End <= Pull.EndTime || SawSlaughter(window));

    private bool SawSlaughter(OpenWoundsWindow window) =>
        _casts.Any(cast => window.Covers(cast.Timestamp));

    private bool IsInsideOpenWounds(int timestamp) =>
        _windows.Any(window => window.Covers(timestamp));

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
        public HashSet<int> Targets { get; } = [];
        public long PayoffDamage { get; set; }
    }
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
