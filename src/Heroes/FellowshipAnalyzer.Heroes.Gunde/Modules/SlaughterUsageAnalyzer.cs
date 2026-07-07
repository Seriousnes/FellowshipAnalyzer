using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

/// <summary>
/// Scores how well each Slaughter is set up. Slaughter consumes all of Gunde's Rend into a short
/// 160% bleed, so its value is maximised when it is cast (a) inside the Open Wounds window that
/// Rupture leaves behind — the next Slaughter deals more for 18s — and (b) after Heart Splitter has
/// been used to build Rend since the previous Slaughter. The shape-specialised leaves add the extra
/// success criterion their rotation calls for.
/// </summary>
/// <remarks>
/// Open Wounds is not a curated symbol in the spell registry, so it is matched by its resolved
/// <c>FSLID</c> (effect range offset + native id 3233). The analyzer keys on the actual debuff rather
/// than inferring the window from Rupture casts, so it holds across builds and Open Wounds sources.
/// </remarks>
public abstract partial class SlaughterUsageAnalyzer : Analyzer<SlaughterUsageReport>
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

    /// <summary>Per-pull projection of this analyzer's accumulated state for the closing pull.</summary>
    public override SlaughterUsageReport OnPullEnd()
    {
        FinalizePendingSlaughter();

        var casts = _slaughters.Count;
        if (casts == 0)
        {
            var idleCard = new AnalyzerScoreCard(ScoreTitle, 0,
                "No Slaughter casts detected — consistent with a Heart Splitter build that keeps Rend on the target.",
                "amber");
            return new SlaughterUsageReport(idleCard, Shape, 0, 0, 0, 0, [],
                [new GundeFinding("info", "No Slaughter was cast on this pull; nothing to score for a build that avoids it.")]);
        }

        foreach (var slaughter in _slaughters)
        {
            slaughter.WellExecuted = IsWellExecuted(slaughter);
            slaughter.Outcome = DescribeOutcome(slaughter);
        }

        var openWoundsTimed = _slaughters.Count(s => s.OpenWoundsActive);
        var heartSplitterPrimed = _slaughters.Count(s => s.HeartSplitterPrimed);
        var wellExecuted = _slaughters.Count(s => s.WellExecuted);
        var score = (int)Math.Round((double)wellExecuted / casts * 100);

        var findings = new List<GundeFinding>
        {
            new("info", $"{wellExecuted} of {casts} Slaughters were set up well; {openWoundsTimed} landed inside an Open Wounds window."),
        };
        foreach (var missed in _slaughters.Where(s => !s.WellExecuted).Take(5))
            findings.Add(new GundeFinding("warning", missed.Outcome, missed.Timestamp));

        var accent = score >= 75 ? "ice" : score >= 50 ? "amber" : "ember";
        var card = new AnalyzerScoreCard(ScoreTitle, score,
            $"{wellExecuted}/{casts} Slaughters cast on cue ({openWoundsTimed} inside Open Wounds).", accent);

        return new SlaughterUsageReport(card, Shape, casts, openWoundsTimed, heartSplitterPrimed, wellExecuted,
            [.. _slaughters], findings);
    }

    /// <summary>The pull shape this leaf scores, stamped onto the shared report.</summary>
    protected abstract GundePullShape Shape { get; }

    /// <summary>Title for this leaf's score card.</summary>
    protected abstract string ScoreTitle { get; }

    /// <summary>Whether a Slaughter met the success bar for this pull shape.</summary>
    protected abstract bool IsWellExecuted(SlaughterEvaluation slaughter);

    /// <summary>Human-readable outcome for a single Slaughter under this pull shape.</summary>
    protected abstract string DescribeOutcome(SlaughterEvaluation slaughter);

    /// <summary>Joins one or two failure reasons into a sentence, capitalising the first.</summary>
    protected static string JoinReasons(List<string> reasons) =>
        reasons.Count == 0
            ? "Only partially set up."
            : char.ToUpperInvariant(reasons[0][0]) + reasons[0][1..] +
                (reasons.Count > 1 ? ", and " + reasons[1] : string.Empty) + ".";

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
    protected override GundePullShape Shape => GundePullShape.Boss;

    protected override string ScoreTitle => "Slaughter Setup (Boss)";

    protected override bool IsWellExecuted(SlaughterEvaluation slaughter) =>
        slaughter.OpenWoundsActive && slaughter.HeartSplitterPrimed;

    protected override string DescribeOutcome(SlaughterEvaluation slaughter)
    {
        if (slaughter.WellExecuted)
            return "Cast inside Open Wounds with Rend rebuilt by Heart Splitter.";

        var reasons = new List<string>();
        if (!slaughter.OpenWoundsActive)
            reasons.Add("no Open Wounds window was active, so it missed Rupture's bonus");
        if (!slaughter.HeartSplitterPrimed)
            reasons.Add("Heart Splitter was not used since the previous Slaughter, so Rend was not rebuilt");
        return JoinReasons(reasons);
    }
}

/// <summary>
/// Trash (AoE) Slaughter scoring: a Slaughter counts when it lands inside an Open Wounds window and
/// its bleed spreads across the pack (two or more enemies).
/// </summary>
[ForPull(PullKind.Multi)]
public sealed class TrashSlaughterUsage : SlaughterUsageAnalyzer
{
    private const int PackThreshold = 2;

    protected override GundePullShape Shape => GundePullShape.Aoe;

    protected override string ScoreTitle => "Slaughter Setup (AoE)";

    protected override bool IsWellExecuted(SlaughterEvaluation slaughter) =>
        slaughter.OpenWoundsActive && slaughter.TargetsHit >= PackThreshold;

    protected override string DescribeOutcome(SlaughterEvaluation slaughter)
    {
        if (slaughter.WellExecuted)
            return $"Cast inside Open Wounds, spreading the bleed to {slaughter.TargetsHit} enemies.";

        var reasons = new List<string>();
        if (!slaughter.OpenWoundsActive)
            reasons.Add("no Open Wounds window was active, so it missed Rupture's bonus");
        if (slaughter.TargetsHit < PackThreshold)
            reasons.Add(slaughter.TargetsHit <= 1
                ? "it hit a single target, wasting the AoE bleed"
                : $"it only reached {slaughter.TargetsHit} enemies");
        return JoinReasons(reasons);
    }
}
