using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Ardeos.Core;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

[ForPull(PullKind.Single | PullKind.Multi)]
[Dependency<Combatants>]
[Dependency<ArdeosDotTracker>]
public sealed partial class WildfireComboAnalyzer : Analyzer
{
    public static int TotalDots => ArdeosDots.Count;

    public const int DistinctDotSuccessThreshold = 4;

    public const int PartialSetupFloor = 2;

    public const int EngulfingInstanceThreshold = 2;

    public const int DetonateSpamThreshold = 3;

    private const int WildfireBuffDurationMs = 9000;
    private const int SequenceWindowMs = 6000;

    private readonly List<CastEvent> _casts = [];
    private readonly List<WildfireAnchor> _anchors = [];

    private List<WildfireWindowEvaluation> Evaluated => field ??= BuildWindows();

    public IReadOnlyList<WildfireWindowEvaluation> Windows => Evaluated;

    public int EvaluatedWindows => Evaluated.Count;
    public int SuccessfulWindows => Evaluated.Count(w => w.Successful);
    public int PartialWindows => Evaluated.Count(w => w.Partial);
    public int WindowsWithPyromania => Evaluated.Count(w => w.HasPyromania);
    public int WindowsWithIncinerate => Evaluated.Count(w => w.HasIncinerate);
    public double AverageDetonateCasts => Evaluated.Count == 0 ? 0d : Evaluated.Average(w => w.DetonateCasts);
    public double AverageDistinctDots => Evaluated.Count == 0 ? 0d : Evaluated.Average(w => w.DistinctDots);

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        if (castEvent.Fake)
            return;

        if (!IsRelevant(castEvent.Ability.Id))
            return;

        _casts.Add(castEvent);

        if (castEvent.Ability.Id != Spells.Wildfire.FSLID)
            return;

        var target = ResolveTarget(castEvent);
        _anchors.Add(new WildfireAnchor(castEvent, target, ArdeosDotTracker.CoverageOn(target, castEvent.Timestamp)));
    }

    private List<WildfireWindowEvaluation> BuildWindows()
    {
        var windows = new List<WildfireWindowEvaluation>();
        foreach (var anchor in _anchors)
            windows.Add(EvaluateWindow(anchor));
        return windows;
    }

    private WildfireWindowEvaluation EvaluateWindow(WildfireAnchor anchor)
    {
        var timestamp = anchor.Cast.Timestamp;
        var target = anchor.Target;
        var coverage = anchor.Coverage;
        var activeDots = coverage.Where(entry => entry.Active).Select(entry => entry.Dot).ToList();
        var engulfingInstances = coverage.Single(entry => entry.Dot == ArdeosDots.EngulfingFlames).Instances;

        var setupSuccessful =
            activeDots.Count >= DistinctDotSuccessThreshold &&
            engulfingInstances >= EngulfingInstanceThreshold;

        var (buffStart, buffEnd) = ResolveBuffWindow(timestamp);
        var detonateCasts = _casts.Count(c =>
            c.Ability.Id == Spells.Detonate.FSLID && c.Timestamp >= buffStart && c.Timestamp <= buffEnd);
        var detonateSpamFollowed = detonateCasts >= DetonateSpamThreshold;

        var successful = setupSuccessful && detonateSpamFollowed;
        var partial = !successful && detonateSpamFollowed && activeDots.Count >= PartialSetupFloor;

        var (hasPyromania, hasIncinerate, castsInWindow) = CollectSequence(timestamp);

        return new WildfireWindowEvaluation
        {
            StartTimestamp = timestamp,
            TargetId = target.ActorId,
            TargetInstance = target.Instance,
            Coverage = coverage,
            ActiveDots = activeDots,
            EngulfingInstances = engulfingInstances,
            SetupSuccessful = setupSuccessful,
            DetonateCasts = detonateCasts,
            DetonateSpamFollowed = detonateSpamFollowed,
            BuffWindowStart = buffStart,
            BuffWindowEnd = buffEnd,
            Successful = successful,
            Partial = partial,
            HasPyromania = hasPyromania,
            HasIncinerate = hasIncinerate,
            CastsInWindow = castsInWindow,
        };
    }

    private UnitKey ResolveTarget(CastEvent anchor)
    {
        if (anchor.TargetId > 0)
            return new UnitKey(anchor.TargetId, anchor.TargetInstance);

        return ArdeosDotTracker.MostDottedEnemy(anchor.Timestamp) ?? new UnitKey(0, null);
    }

    private (int Start, int End) ResolveBuffWindow(int anchor)
    {
        var buff = Combatants.Selected.GetBuff(Spells.WildfireDotBonusBuff.FSLID, forTimestamp: anchor);
        if (buff is null)
            return (anchor, anchor + WildfireBuffDurationMs);

        return (buff.Start, buff.End ?? anchor + WildfireBuffDurationMs);
    }

    private (bool HasPyromania, bool HasIncinerate, IReadOnlyList<CastEvent> Casts) CollectSequence(int anchor)
    {
        var start = anchor - SequenceWindowMs;
        var end = anchor + SequenceWindowMs;

        var hasPyromania = false;
        var hasIncinerate = false;
        var casts = new List<CastEvent>();
        foreach (var cast in _casts)
        {
            var t = cast.Timestamp;
            if (t < start || t > end)
                continue;

            casts.Add(cast);

            if (t >= anchor)
                continue;

            if (cast.Ability.Id == Spells.Pyromania.FSLID)
                hasPyromania = true;
            else if (cast.Ability.Id == Spells.Incinerate.FSLID)
                hasIncinerate = true;
        }

        return (hasPyromania, hasIncinerate, casts);
    }

    private static bool IsRelevant(int id) =>
        id == Spells.Wildfire.FSLID ||
        id == Spells.Detonate.FSLID ||
        id == Spells.FireFrogs.FSLID ||
        id == Spells.Apocalypse.FSLID ||
        id == Spells.FireBall.FSLID ||
        id == Spells.EngulfingFlames.FSLID ||
        id == Spells.Pyromania.FSLID ||
        id == Spells.Incinerate.FSLID;

    private sealed record WildfireAnchor(CastEvent Cast, UnitKey Target, IReadOnlyList<DotCoverage> Coverage);

    public sealed record WildfireWindowEvaluation
    {
        public int StartTimestamp { get; init; }
        public int TargetId { get; init; }
        public int? TargetInstance { get; init; }

        public IReadOnlyList<DotCoverage> Coverage { get; init; } = [];

        public IReadOnlyList<Dot> ActiveDots { get; init; } = [];
        public int DistinctDots => ActiveDots.Count;
        public int EngulfingInstances { get; init; }
        public bool SetupSuccessful { get; init; }
        public int DetonateCasts { get; init; }
        public bool DetonateSpamFollowed { get; init; }
        public int BuffWindowStart { get; init; }
        public int BuffWindowEnd { get; init; }
        public bool Successful { get; init; }
        public bool Partial { get; init; }
        public bool HasPyromania { get; init; }
        public bool HasIncinerate { get; init; }
        public IReadOnlyList<CastEvent> CastsInWindow { get; init; } = [];
    }
}
