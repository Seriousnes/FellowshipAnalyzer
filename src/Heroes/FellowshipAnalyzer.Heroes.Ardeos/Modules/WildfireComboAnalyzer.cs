using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Evaluates Ardeos's Wildfire burn windows. Each window is anchored on a Wildfire cast: the setup
/// rotation should land in the seconds before it (Fire Frogs, Apocalypse, Fire Ball and Engulfing
/// Flames lay the DoTs Detonate scales on) and Detonate spam should follow immediately after.
/// </summary>
/// <remarks>
/// Detection is cast-based, never DoT-state based. A flat list of the relevant player casts is
/// accumulated during dispatch and sliced around each Wildfire anchor in <see cref="OnPullEnd"/>;
/// no per-target debuff tracking is reconstructed, because targets that die carrying a DoT never
/// log a remove and drift that state. Pyromania and Incinerate are cooldown-limited relative to
/// Wildfire and cannot always be aligned, so they are surfaced as bonus signals and never gate a
/// window's classification. One analyzer serves single-target and AoE pulls, since the standard
/// burn window is identical for both.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class WildfireComboAnalyzer : Analyzer
{
    /// <summary>How many of the four core setup abilities make a window's setup complete.</summary>
    public const int CoreSetupSuccessThreshold = 3;

    /// <summary>Detonate casts after Wildfire that count as a clean follow-up.</summary>
    public const int DetonateSpamThreshold = 3;

    /// <summary>Engulfing Flames casts the standard window fits before Wildfire.</summary>
    public const int StandardEngulfingFlamesCasts = 2;

    private const int PreWindowMs = 6000;
    private const int PostWindowMs = 6000;

    private readonly List<CastEvent> _casts = [];
    private readonly List<WildfireWindowEvaluation> _windows = [];

    /// <summary>Per-window evaluations, one per Wildfire cast in the pull.</summary>
    public IReadOnlyList<WildfireWindowEvaluation> Windows => _windows;

    public int EvaluatedWindows => _windows.Count;
    public int SuccessfulWindows { get; private set; }
    public int PartialWindows { get; private set; }
    public int WindowsWithPyromania { get; private set; }
    public int WindowsWithIncinerate { get; private set; }
    public double AverageDetonateCasts { get; private set; }

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        if (castEvent.Fake)
            return;

        if (IsRelevant(castEvent.Ability.Id))
            _casts.Add(castEvent);
    }

    /// <summary>Slices the accumulated casts around each Wildfire anchor and finalizes the aggregates.</summary>
    public override void OnPullEnd()
    {
        foreach (var anchor in _casts.Where(c => c.Ability.Id == Spells.Wildfire.FSLID).ToList())
            _windows.Add(EvaluateWindow(anchor.Timestamp));

        SuccessfulWindows = _windows.Count(w => w.Successful);
        PartialWindows = _windows.Count(w => w.Partial);
        WindowsWithPyromania = _windows.Count(w => w.HasPyromania);
        WindowsWithIncinerate = _windows.Count(w => w.HasIncinerate);
        AverageDetonateCasts = _windows.Count == 0 ? 0d : _windows.Average(w => w.DetonateCasts);
    }

    private WildfireWindowEvaluation EvaluateWindow(int anchor)
    {
        var preStart = anchor - PreWindowMs;
        var postEnd = anchor + PostWindowMs;

        var hasFireFrogs = false;
        var hasApocalypse = false;
        var hasFireBall = false;
        var hasEngulfingFlames = false;
        var hasPyromania = false;
        var hasIncinerate = false;
        var engulfingFlamesCount = 0;
        var detonateCasts = 0;
        var castsInWindow = new List<CastEvent>();

        foreach (var cast in _casts)
        {
            var timestamp = cast.Timestamp;
            if (timestamp < preStart || timestamp > postEnd)
                continue;

            castsInWindow.Add(cast);

            var id = cast.Ability.Id;
            if (timestamp >= preStart && timestamp < anchor)
            {
                if (id == Spells.FireFrogs.FSLID)
                    hasFireFrogs = true;
                else if (id == Spells.Apocalypse.FSLID)
                    hasApocalypse = true;
                else if (id == Spells.FireBall.FSLID)
                    hasFireBall = true;
                else if (id == Spells.EngulfingFlames.FSLID)
                {
                    hasEngulfingFlames = true;
                    engulfingFlamesCount++;
                }
                else if (id == Spells.Pyromania.FSLID)
                    hasPyromania = true;
                else if (id == Spells.Incinerate.FSLID)
                    hasIncinerate = true;
            }
            else if (timestamp > anchor && id == Spells.Detonate.FSLID)
            {
                detonateCasts++;
            }
        }

        var coreSetupCount =
            (hasFireFrogs ? 1 : 0) +
            (hasApocalypse ? 1 : 0) +
            (hasFireBall ? 1 : 0) +
            (hasEngulfingFlames ? 1 : 0);

        var detonateSpamFollowed = detonateCasts >= DetonateSpamThreshold;
        var successful = coreSetupCount >= CoreSetupSuccessThreshold && detonateSpamFollowed;
        var partial = !successful && detonateSpamFollowed && coreSetupCount >= 1;

        return new WildfireWindowEvaluation
        {
            StartTimestamp = anchor,
            PreWindowStart = preStart,
            PostWindowEnd = postEnd,
            Successful = successful,
            Partial = partial,
            CoreSetupCount = coreSetupCount,
            HasFireFrogs = hasFireFrogs,
            HasApocalypse = hasApocalypse,
            HasFireBall = hasFireBall,
            HasEngulfingFlames = hasEngulfingFlames,
            EngulfingFlamesCount = engulfingFlamesCount,
            HasPyromania = hasPyromania,
            HasIncinerate = hasIncinerate,
            DetonateCasts = detonateCasts,
            CastsInWindow = castsInWindow,
        };
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
    /// Typed evaluation of a single Wildfire burn window: which setup abilities landed before the
    /// anchor, the Detonate spam that followed, and the classification derived from both.
    /// </summary>
    public sealed record WildfireWindowEvaluation
    {
        public int StartTimestamp { get; init; }
        public int PreWindowStart { get; init; }
        public int PostWindowEnd { get; init; }
        public bool Successful { get; init; }
        public bool Partial { get; init; }
        public int CoreSetupCount { get; init; }
        public bool HasFireFrogs { get; init; }
        public bool HasApocalypse { get; init; }
        public bool HasFireBall { get; init; }
        public bool HasEngulfingFlames { get; init; }
        public int EngulfingFlamesCount { get; init; }
        public bool HasPyromania { get; init; }
        public bool HasIncinerate { get; init; }
        public int DetonateCasts { get; init; }
        public IReadOnlyList<CastEvent> CastsInWindow { get; init; } = [];
    }
}
