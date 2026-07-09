using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

using System.Text;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Scores Ardeos's Wildfire burn windows. Each window is anchored on a Wildfire cast: the setup
/// rotation should land in the seconds before it (Fire Frogs, Apocalypse, Fire Ball and Engulfing
/// Flames lay the DoTs Detonate scales on) and Detonate spam should follow immediately after.
/// </summary>
/// <remarks>
/// Detection is cast-based, never DoT-state based. A flat list of the relevant player casts is
/// accumulated during dispatch and sliced around each Wildfire anchor in <see cref="OnPullEnd"/>;
/// no per-target debuff tracking is reconstructed, because targets that die carrying a DoT never
/// log a remove and drift that state. Pyromania and Incinerate are cooldown-limited relative to
/// Wildfire and cannot always be aligned, so they are reported as bonus signals and never gate a
/// window's classification. One analyzer serves single-target and AoE pulls, since the standard
/// burn window is identical for both.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class WildfireComboAnalyzer : Analyzer<WildfireComboReport>
{
    private const int PreWindowMs = 6000;
    private const int PostWindowMs = 6000;
    private const int CoreSetupSuccessThreshold = 3;
    private const int DetonateSpamThreshold = 3;
    private const int StandardEngulfingFlamesCasts = 2;

    private readonly List<CastEvent> _casts = [];

    [On<CastEvent>(By = Actor.Player)]
    private void OnCast(CastEvent castEvent)
    {
        if (castEvent.Fake)
            return;

        if (IsRelevant(castEvent.Ability.Id))
            _casts.Add(castEvent);
    }

    /// <summary>Slices the accumulated casts around each Wildfire anchor and projects the pull.</summary>
    public override WildfireComboReport OnPullEnd()
    {
        var anchors = _casts.Where(c => c.Ability.Id == Spells.Wildfire.FSLID).ToList();
        var windows = new List<WildfireWindowEvaluation>(anchors.Count);
        foreach (var anchor in anchors)
            windows.Add(EvaluateWindow(anchor.Timestamp));

        var evaluatedWindows = windows.Count;
        var successfulWindows = windows.Count(w => w.Successful);
        var partialWindows = windows.Count(w => w.Partial);
        var windowsWithPyromania = windows.Count(w => w.HasPyromania);
        var windowsWithIncinerate = windows.Count(w => w.HasIncinerate);
        var averageDetonateCasts = evaluatedWindows == 0
            ? 0d
            : windows.Average(w => w.DetonateCasts);

        var score = evaluatedWindows == 0
            ? 0
            : (int)Math.Round(((successfulWindows + (partialWindows * 0.5)) / evaluatedWindows) * 100);

        var summary = evaluatedWindows == 0
            ? "No Wildfire windows detected in this pull."
            : $"{successfulWindows}/{evaluatedWindows} Wildfire windows opened with the setup rotation and were followed by Detonate spam.";

        var scoreCard = new AnalyzerScoreCard(
            "Wildfire Burn Windows",
            score,
            summary,
            score >= 75 ? "ice" : score >= 50 ? "amber" : "ember");

        var findings = BuildFindings(windows, successfulWindows, windowsWithPyromania, windowsWithIncinerate);

        return new WildfireComboReport(
            scoreCard,
            evaluatedWindows,
            successfulWindows,
            partialWindows,
            windowsWithPyromania,
            windowsWithIncinerate,
            averageDetonateCasts,
            windows,
            findings);
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

        var window = new WildfireWindowEvaluation
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
        window.Outcome = BuildOutcome(window);
        return window;
    }

    private static string BuildOutcome(WildfireWindowEvaluation window)
    {
        if (window.Successful)
        {
            var note = window is { HasPyromania: true, HasIncinerate: true }
                ? " Pyromania and Incinerate both aligned to extend the burn."
                : window.HasPyromania
                    ? " Pyromania aligned with this window."
                    : window.HasIncinerate
                        ? " Incinerate extended the DoTs into this window."
                        : string.Empty;
            return "Opened Wildfire with the setup rotation in place and followed with Detonate spam." + note;
        }

        var reasons = new List<string>();

        var missing = new List<string>();
        if (!window.HasFireFrogs)
            missing.Add("Fire Frogs");
        if (!window.HasApocalypse)
            missing.Add("Apocalypse");
        if (!window.HasFireBall)
            missing.Add("Fire Ball");
        if (!window.HasEngulfingFlames)
            missing.Add("Engulfing Flames");

        if (window.CoreSetupCount == 0)
            reasons.Add("no setup abilities were cast before Wildfire");
        else if (window.CoreSetupCount < CoreSetupSuccessThreshold && missing.Count > 0)
            reasons.Add($"the setup was incomplete before Wildfire (missing {JoinNames(missing)})");

        if (window.DetonateCasts == 0)
            reasons.Add("Detonate was not spammed after Wildfire");
        else if (window.DetonateCasts < DetonateSpamThreshold)
            reasons.Add($"only {window.DetonateCasts} Detonate cast{(window.DetonateCasts == 1 ? " " : "s ")}followed Wildfire");

        if (window is { HasEngulfingFlames: true, EngulfingFlamesCount: < StandardEngulfingFlamesCasts })
            reasons.Add("Engulfing Flames was cast only once (the standard window uses it twice)");

        if (reasons.Count == 0)
            reasons.Add("the window only partially matched the expected burn setup");

        return JoinReasons(reasons);
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

    private List<ArdeosFinding> BuildFindings(
        IReadOnlyList<WildfireWindowEvaluation> windows,
        int successfulWindows,
        int windowsWithPyromania,
        int windowsWithIncinerate)
    {
        var findings = new List<ArdeosFinding>();
        if (windows.Count == 0)
        {
            findings.Add(new ArdeosFinding("info", "No Wildfire windows were detected in this pull."));
            return findings;
        }

        findings.Add(new ArdeosFinding("info",
            $"{successfulWindows} of {windows.Count} Wildfire windows were clean burns (setup complete and Detonate spam followed)."));

        foreach (var window in windows.Where(w => !w.Successful).Take(5))
        {
            findings.Add(new ArdeosFinding(
                window.Partial ? "warning" : "major",
                window.Outcome,
                window.StartTimestamp));
        }

        findings.Add(new ArdeosFinding("info",
            $"Pyromania aligned with {windowsWithPyromania} of {windows.Count} windows; Incinerate extended DoTs into {windowsWithIncinerate}."));

        return findings;
    }

    private static string JoinNames(List<string> names)
    {
        if (names.Count == 1)
            return names[0];

        var builder = new StringBuilder();
        for (var i = 0; i < names.Count; i++)
        {
            if (i > 0)
                builder.Append(i == names.Count - 1 ? " and " : ", ");
            builder.Append(names[i]);
        }

        return builder.ToString();
    }

    private static string JoinReasons(List<string> reasons)
    {
        if (reasons.Count == 1)
            return Capitalise(reasons[0]) + ".";

        var builder = new StringBuilder();
        for (var i = 0; i < reasons.Count; i++)
        {
            if (i > 0)
                builder.Append(i == reasons.Count - 1 ? ", and " : ", ");
            builder.Append(i == 0 ? Capitalise(reasons[i]) : reasons[i]);
        }

        builder.Append('.');
        return builder.ToString();
    }

    private static string Capitalise(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    /// <summary>
    /// Per-window evaluation of a single Wildfire burn. Exposed as settable properties so the
    /// source-generated <c>JsonSerializerContext</c> can round-trip it across worker boundaries.
    /// </summary>
    public sealed class WildfireWindowEvaluation
    {
        public int StartTimestamp { get; set; }
        public int PreWindowStart { get; set; }
        public int PostWindowEnd { get; set; }
        public bool Successful { get; set; }
        public bool Partial { get; set; }
        public string Outcome { get; set; } = string.Empty;
        public int CoreSetupCount { get; set; }
        public bool HasFireFrogs { get; set; }
        public bool HasApocalypse { get; set; }
        public bool HasFireBall { get; set; }
        public bool HasEngulfingFlames { get; set; }
        public int EngulfingFlamesCount { get; set; }
        public bool HasPyromania { get; set; }
        public bool HasIncinerate { get; set; }
        public int DetonateCasts { get; set; }
        public List<CastEvent> CastsInWindow { get; set; } = [];
    }
}
