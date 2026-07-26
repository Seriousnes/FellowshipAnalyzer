using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Evaluates Ardeos's Wildfire burn windows. Each window is anchored on a Wildfire cast: the setup
/// rotation should leave the six fire damage-over-time effects ticking on the enemy Wildfire is aimed at
/// (Engulfing Flames twice), and Detonate spam should follow while the Wildfire self-buff is up.
/// </summary>
/// <remarks>
/// Setup is scored on live per-target damage-over-time state read from the fight-wide
/// <see cref="Combatants"/> aura registry at the exact Wildfire cast timestamp, not on a cast-window
/// heuristic. A window is evaluated on the unit the cast targets (<see cref="CastEvent.TargetId"/> /
/// <see cref="CastEvent.TargetInstance"/>); a cast with no usable enemy target falls back to the enemy
/// carrying the most damage-over-time instances at that instant, and to no coverage when no enemy carries
/// any. Detonate follow-up is counted inside the Wildfire self-buff window (native effect 1825), read from
/// the player's tracked buff and falling back to a nine-second window from the cast when no buff window is
/// logged. Wildfire's forty-five-second cooldown far exceeds the nine-second buff, so the window active at
/// the cast is unambiguously this cast's.
///
/// Setup counts as complete when at least <see cref="DistinctDotSuccessThreshold"/> of the six damage
/// over-time effects are ticking on the target and Engulfing Flames holds at least
/// <see cref="EngulfingInstanceThreshold"/> concurrent instances. A window is successful when setup is
/// complete and Detonate was spammed at least <see cref="DetonateSpamThreshold"/> times inside the buff;
/// partial when the spam followed but setup only reached the <see cref="PartialSetupFloor"/> distinct
/// damage-over-time floor; failing otherwise. Pyromania and Incinerate are cooldown-limited relative to
/// Wildfire and cannot always be aligned, so they are surfaced as bonus alignment signals and never gate a
/// window's classification. A flat cast list is kept only to slice a sequence around each anchor for
/// display and to derive those bonus alignment flags; it no longer drives scoring. One analyzer serves
/// single-target and AoE pulls, since the standard burn window is identical for both.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
[Uses<Combatants>]
public sealed partial class WildfireComboAnalyzer : Analyzer
{
    /// <summary>The count of fire damage-over-time effects the setup rotation lays before Wildfire.</summary>
    public static int TotalDots => ArdeosDots.Count;

    /// <summary>Distinct damage-over-time effects on the target that make a window's setup complete.</summary>
    public const int DistinctDotSuccessThreshold = 4;

    /// <summary>Distinct damage-over-time effects on the target below which a spammed window is not even partial.</summary>
    public const int PartialSetupFloor = 2;

    /// <summary>Concurrent Engulfing Flames instances the standard setup lands on the target.</summary>
    public const int EngulfingInstanceThreshold = 2;

    /// <summary>Detonate casts inside the Wildfire buff that count as a clean follow-up.</summary>
    public const int DetonateSpamThreshold = 3;

    private const int WildfireBuffDurationMs = 9000;
    private const int SequenceWindowMs = 6000;

    private readonly List<CastEvent> _casts = [];

    private List<WildfireWindowEvaluation>? _evaluated;
    private List<WildfireWindowEvaluation> Evaluated => _evaluated ??= BuildWindows();

    /// <summary>Per-window evaluations, one per Wildfire cast in the pull.</summary>
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

        if (IsRelevant(castEvent.Ability.Id))
            _casts.Add(castEvent);
    }

    private List<WildfireWindowEvaluation> BuildWindows()
    {
        var windows = new List<WildfireWindowEvaluation>();
        foreach (var anchor in _casts.Where(c => c.Ability.Id == Spells.Wildfire.FSLID))
            windows.Add(EvaluateWindow(anchor));
        return windows;
    }

    private WildfireWindowEvaluation EvaluateWindow(CastEvent anchor)
    {
        var timestamp = anchor.Timestamp;
        var target = ResolveTarget(anchor);
        var activeDots = ActiveDotsOn(target, timestamp);
        var engulfingInstances = Combatants.AuraInstanceCount(target.ActorId, target.Instance, ArdeosDots.EngulfingFlames.EffectId, timestamp);

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

        return MostDottedEnemy(anchor.Timestamp) ?? new UnitKey(0, null);
    }

    private UnitKey? MostDottedEnemy(int timestamp)
    {
        var candidates = new HashSet<UnitKey>();
        foreach (var dot in ArdeosDots.All)
            foreach (var key in Combatants.EnemiesWithAura(dot.EffectId, timestamp))
                candidates.Add(key);

        UnitKey? best = null;
        var bestInstances = 0;
        foreach (var key in candidates)
        {
            var instances = TotalDotInstances(key, timestamp);
            if (best is null || instances > bestInstances)
                (best, bestInstances) = (key, instances);
        }

        return best;
    }

    private int TotalDotInstances(UnitKey key, int timestamp) =>
        ArdeosDots.All.Sum(dot => Combatants.AuraInstanceCount(key.ActorId, key.Instance, dot.EffectId, timestamp));

    private IReadOnlyList<ArdeosDot> ActiveDotsOn(UnitKey target, int timestamp)
    {
        var dots = new List<ArdeosDot>();
        foreach (var dot in ArdeosDots.All)
        {
            if (Combatants.AuraInstanceCount(target.ActorId, target.Instance, dot.EffectId, timestamp) > 0)
                dots.Add(dot);
        }
        return dots;
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

    /// <summary>
    /// Typed evaluation of a single Wildfire burn window: the target it was scored on, which damage
    /// over-time effects were ticking there at the cast, the Engulfing Flames instances present, the
    /// Detonate spam that followed inside the buff, and the classification derived from both.
    /// </summary>
    public sealed record WildfireWindowEvaluation
    {
        public int StartTimestamp { get; init; }
        public int TargetId { get; init; }
        public int? TargetInstance { get; init; }
        public IReadOnlyList<ArdeosDot> ActiveDots { get; init; } = [];
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
