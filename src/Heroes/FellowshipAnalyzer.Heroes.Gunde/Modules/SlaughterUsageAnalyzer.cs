using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Evaluates how well each Slaughter is set up. Slaughter consumes all of Gunde's Rend into a short
/// 160% bleed, so its value is maximised when it is cast (a) inside the Open Wounds window that
/// Rupture leaves behind, buffing the next Slaughter for 18s, and (b) after Heart Splitter has
/// been used to build Rend since the previous Slaughter. The shape-specialised leaves add the extra
/// success criterion their rotation calls for.
/// </summary>
/// <remarks>
/// Open Wounds is not a curated symbol in the spell registry, so it is matched by its resolved
/// <c>FSLID</c> (effect range offset + native id 3233). The analyzer keys on the actual debuff rather
/// than inferring the window from Rupture casts, so it holds across builds and Open Wounds sources.
/// </remarks>
public abstract partial class SlaughterUsageAnalyzer : Analyzer
{
    private const int OpenWoundsEffectFslid = 1_000_000 + 3233;
    private const int OpenWoundsDurationMs = 18_000;

    private readonly Dictionary<int, int> _openWoundsApplied = [];
    private readonly List<(int Start, int End)> _openWoundsWindows = [];
    private readonly List<SlaughterEvaluation> _slaughters = [];
    private readonly HashSet<int> _pendingTargets = [];

    private int _lastHeartSplitterTimestamp = int.MinValue;
    private int _previousSlaughterTimestamp = int.MinValue;
    private SlaughterEvaluation? _pendingSlaughter;

    /// <summary>The pull shape this leaf scores against.</summary>
    public abstract GundePullShape Shape { get; }

    /// <summary>Every Slaughter cast on the pull, in cast order, with its per-cast evaluation.</summary>
    public IReadOnlyList<SlaughterEvaluation> Slaughters => _slaughters;

    public int SlaughterCasts => _slaughters.Count;

    /// <summary>Slaughters cast while a Rupture Open Wounds window was active.</summary>
    public int OpenWoundsTimed { get; private set; }

    /// <summary>Slaughters preceded by a Heart Splitter since the previous Slaughter.</summary>
    public int HeartSplitterPrimed { get; private set; }

    /// <summary>Slaughters that met the success bar for this pull shape.</summary>
    public int WellExecuted { get; private set; }

    [On<ApplyDebuffEvent>(By = Actor.Player)]
    private void OnDebuffApplied(ApplyDebuffEvent @event)
    {
        if (@event.Ability.Id == OpenWoundsEffectFslid)
            _openWoundsApplied[@event.TargetId] = @event.Timestamp;
    }

    [On<RefreshDebuffEvent>(By = Actor.Player)]
    private void OnDebuffRefreshed(RefreshDebuffEvent @event)
    {
        if (@event.Ability.Id == OpenWoundsEffectFslid)
            _openWoundsApplied[@event.TargetId] = @event.Timestamp;
    }

    [On<RemoveDebuffEvent>(By = Actor.Player)]
    private void OnDebuffRemoved(RemoveDebuffEvent @event)
    {
        if (@event.Ability.Id == OpenWoundsEffectFslid && _openWoundsApplied.Remove(@event.TargetId, out var start))
            _openWoundsWindows.Add((start, @event.Timestamp));
    }

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SlaughterDot))]
    private void OnSlaughterDotApplied(ApplyDebuffEvent @event) => _pendingTargets.Add(@event.TargetId);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SlaughterDot))]
    private void OnSlaughterDotRefreshed(RefreshDebuffEvent @event) => _pendingTargets.Add(@event.TargetId);

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.HeartSplitter))]
    private void OnHeartSplitter(CastEvent @event) => _lastHeartSplitterTimestamp = @event.Timestamp;

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Slaughter))]
    private void OnSlaughter(CastEvent @event)
    {
        FinalizePendingSlaughter();

        var evaluation = new SlaughterEvaluation
        {
            Timestamp = @event.Timestamp,
            OpenWoundsActive = IsOpenWoundsActive(@event.Timestamp),
            HeartSplitterPrimed = _lastHeartSplitterTimestamp > _previousSlaughterTimestamp,
        };

        _slaughters.Add(evaluation);
        _pendingSlaughter = evaluation;
        _pendingTargets.Clear();
        _previousSlaughterTimestamp = @event.Timestamp;
    }

    public override void OnPullEnd()
    {
        FinalizePendingSlaughter();

        foreach (var slaughter in _slaughters)
            slaughter.WellExecuted = IsWellExecuted(slaughter);

        OpenWoundsTimed = _slaughters.Count(s => s.OpenWoundsActive);
        HeartSplitterPrimed = _slaughters.Count(s => s.HeartSplitterPrimed);
        WellExecuted = _slaughters.Count(s => s.WellExecuted);
    }

    /// <summary>Whether a Slaughter met the success bar for this pull shape.</summary>
    protected abstract bool IsWellExecuted(SlaughterEvaluation slaughter);

    private void FinalizePendingSlaughter()
    {
        if (_pendingSlaughter is not null)
            _pendingSlaughter.TargetsHit = _pendingTargets.Count;
        _pendingSlaughter = null;
    }

    private bool IsOpenWoundsActive(int timestamp)
    {
        foreach (var (start, end) in _openWoundsWindows)
            if (timestamp >= start && timestamp <= end)
                return true;

        foreach (var start in _openWoundsApplied.Values)
            if (timestamp >= start && timestamp <= start + OpenWoundsDurationMs)
                return true;

        return false;
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
