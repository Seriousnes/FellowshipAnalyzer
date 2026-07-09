using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Scores Ardeos's Engulfing Flames charge economy. Engulfing Flames is a flat two-charge,
/// twenty-second-recharge spell; the goal is to enter each Wildfire burn window holding both charges
/// so it can be double-cast inside the burst. Two waste signals are surfaced: window readiness (how
/// many Engulfing Flames charges were available at each Wildfire cast, two being ready) and overcap
/// (Engulfing Flames sitting at max charges long enough that full recharge periods were wasted).
/// </summary>
/// <remarks>
/// Readiness is sampled live from <see cref="SpellUsable"/> at each Wildfire cast. Overcap is
/// reconstructed from the charge count over time: <see cref="SpellUsable"/> fabricates an
/// <see cref="UpdateSpellUsableEvent"/> whenever Engulfing Flames spends or restores a charge, and the
/// intervals it spends at max charges — assuming it starts the pull at max — are summed and divided by
/// the recharge period, so brief holds before an imminent window cost nothing while sitting capped
/// across recharge cycles is charged as waste. One analyzer serves single-target and AoE pulls, since
/// the burn window is identical for both.
/// </remarks>
[ForPull(PullKind.Single | PullKind.Multi)]
public sealed partial class EngulfingFlamesEconomyAnalyzer : Analyzer<EngulfingFlamesEconomyReport>
{
    private const int WastePenaltyPerCharge = 10;
    private const int FallbackRechargeMs = 20_000;

    private readonly List<WindowReadiness> _windows = [];
    private readonly List<(int Timestamp, int Charges)> _chargeSamples = [];

    [On<CastEvent>(By = Actor.Player, Spell = nameof(Spells.Wildfire))]
    private void OnWildfireCast(CastEvent e)
    {
        var charges = Owner.SpellUsable!.ChargesAvailable(Spells.EngulfingFlames.Id);
        _windows.Add(new WindowReadiness
        {
            Timestamp = e.Timestamp,
            ChargesAvailable = charges,
            Ready = charges >= Spells.EngulfingFlames.Charges,
        });
    }

    [On<UpdateSpellUsableEvent>]
    private void OnUpdateSpellUsable(UpdateSpellUsableEvent e)
    {
        if (e.Ability.FSLID != Spells.EngulfingFlames.FSLID)
            return;

        _chargeSamples.Add((e.Timestamp, e.ChargesAvailable));
    }

    /// <summary>Projects the pull's window readiness and Engulfing Flames overcap.</summary>
    public override EngulfingFlamesEconomyReport OnPullEnd()
    {
        var maxCharges = Spells.EngulfingFlames.Charges;
        var rechargeMs = Spells.EngulfingFlames.Cooldown is { } cooldown and > 0
            ? (int)(cooldown * 1000)
            : FallbackRechargeMs;

        var cappedMs = ComputeCappedMilliseconds(maxCharges);
        var wastedCharges = rechargeMs > 0 ? (int)(cappedMs / rechargeMs) : 0;
        var cappedSeconds = Math.Round(cappedMs / 1000d, 1);

        var windowsEvaluated = _windows.Count;
        var windowsReady = _windows.Count(w => w.Ready);
        var windowsShort = windowsEvaluated - windowsReady;

        var score = windowsEvaluated == 0
            ? 0
            : Math.Clamp(
                (int)Math.Round((double)windowsReady / windowsEvaluated * 100) - (wastedCharges * WastePenaltyPerCharge),
                0,
                100);

        var summary = windowsEvaluated == 0
            ? "No Wildfire windows detected in this pull."
            : $"{windowsReady}/{windowsEvaluated} Wildfire windows opened with both Engulfing Flames charges ready.";

        var scoreCard = new AnalyzerScoreCard(
            "Engulfing Flames Economy",
            score,
            summary,
            score >= 75 ? "ice" : score >= 50 ? "amber" : "ember");

        var findings = BuildFindings(windowsEvaluated, windowsReady, windowsShort, wastedCharges, cappedSeconds);

        return new EngulfingFlamesEconomyReport(
            scoreCard,
            windowsEvaluated,
            windowsReady,
            windowsShort,
            wastedCharges,
            cappedSeconds,
            _windows,
            findings);
    }

    private long ComputeCappedMilliseconds(int maxCharges)
    {
        if (Owner.CurrentPull is not { } pull)
            return 0;

        var startTime = pull.StartTime;
        var endTime = pull.EndTime;
        if (endTime <= startTime)
            return 0;

        var transitions = new List<(int Time, int Charges)>(_chargeSamples.Count + 1)
        {
            (startTime, maxCharges),
        };
        foreach (var (timestamp, charges) in _chargeSamples)
            transitions.Add((Math.Clamp(timestamp, startTime, endTime), charges));

        long cappedMs = 0;
        for (var i = 0; i < transitions.Count; i++)
        {
            var segmentStart = transitions[i].Time;
            var segmentEnd = i + 1 < transitions.Count ? transitions[i + 1].Time : endTime;
            if (segmentEnd > segmentStart && transitions[i].Charges >= maxCharges)
                cappedMs += segmentEnd - segmentStart;
        }

        return cappedMs;
    }

    private static List<ArdeosFinding> BuildFindings(
        int windowsEvaluated,
        int windowsReady,
        int windowsShort,
        int wastedCharges,
        double cappedSeconds)
    {
        var findings = new List<ArdeosFinding>();

        if (windowsEvaluated == 0)
        {
            findings.Add(new ArdeosFinding("info", "No Wildfire windows were detected in this pull."));
        }
        else if (windowsShort == 0)
        {
            findings.Add(new ArdeosFinding("info",
                $"Both Engulfing Flames charges were ready for all {windowsEvaluated} Wildfire windows."));
        }
        else
        {
            findings.Add(new ArdeosFinding(
                windowsReady > 0 ? "warning" : "major",
                $"{windowsShort} of {windowsEvaluated} Wildfire windows opened without both Engulfing Flames charges ready — hold both charges for the burst so it can be double-cast."));
        }

        if (wastedCharges > 0)
        {
            findings.Add(new ArdeosFinding("warning",
                $"Engulfing Flames sat at maximum charges for {cappedSeconds:0.#}s, wasting about {wastedCharges} charge{(wastedCharges == 1 ? string.Empty : "s")} of recharge."));
        }

        return findings;
    }

    /// <summary>
    /// Per-window snapshot of Engulfing Flames charge availability at a Wildfire cast. Exposed as
    /// settable properties so the source-generated <c>JsonSerializerContext</c> can round-trip it
    /// across worker boundaries.
    /// </summary>
    public sealed class WindowReadiness
    {
        public int Timestamp { get; set; }
        public int ChargesAvailable { get; set; }
        public bool Ready { get; set; }
    }
}
