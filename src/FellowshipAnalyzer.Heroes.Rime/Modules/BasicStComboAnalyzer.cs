using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;

using static FellowshipAnalyzer.Core.Analysis.Events;

namespace FellowshipAnalyzer.Heroes.Rime.Modules;

public sealed class BasicStComboAnalyzer : Analyzer
{
    private const int WintersEmbraceDurationMs = 3000;

    private readonly List<TrackedAbilityCast> _casts = [];

    public AnalyzerScoreCard ScoreCard { get; private set; } = null!;
    public int EvaluatedWindows { get; private set; }
    public int SuccessfulWindows { get; private set; }
    public int PartialWindows { get; private set; }
    public int IgnoredAoeWindows { get; private set; }
    public IReadOnlyList<StComboWindowEvaluation> Windows { get; private set; } = [];
    public IReadOnlyList<RimeAnalyzerFinding> Findings { get; private set; } = [];
    
    public override void Complete()
    {
        var evaluations = new List<StComboWindowEvaluation>();
        var findings = new List<RimeAnalyzerFinding>();
        var ignoredAoeWindows = 0;

        foreach (var cast in _casts)
        {
            if (cast.AbilityId != RimeSpells.BurstingIce.Id)
                continue;

            var startTimestamp = cast.Timestamp;
            var endTimestamp = startTimestamp + WintersEmbraceDurationMs;
            var castsInWindow = _casts
                .Where(c => c.Timestamp > startTimestamp && c.Timestamp <= endTimestamp)
                .ToList();

            var relevant = castsInWindow
                .Where(c =>
                    c.AbilityId == RimeSpells.GlacialBlast.Id ||
                    c.AbilityId == RimeSpells.ColdSnap.Id ||
                    c.AbilityId == RimeSpells.FreezingTorrent.Id ||
                    c.AbilityId == RimeSpells.IceComet.Id)
                .ToList();

            if (relevant.Count > 0 && relevant[0].AbilityId == RimeSpells.IceComet.Id)
            {
                ignoredAoeWindows += 1;
                continue;
            }

            var glacialBlastIndex = relevant.FindIndex(c => c.AbilityId == RimeSpells.GlacialBlast.Id);
            var finisherIndex = relevant.FindIndex(c =>
                c.AbilityId == RimeSpells.ColdSnap.Id || c.AbilityId == RimeSpells.FreezingTorrent.Id);

            var successful = glacialBlastIndex == 0 && finisherIndex == 1;
            var partial = glacialBlastIndex == 0 && !successful;
            var outcome = successful
                ? "Executed Bursting Ice -> Glacial Blast -> finisher inside Winter's Embrace."
                : partial
                    ? finisherIndex > 1
                        ? "Opened with Glacial Blast, but the finisher landed late or after an extra cast."
                        : "Opened with Glacial Blast, but the Winter's Embrace window did not also fit Cold Snap or Freezing Torrent."
                    : glacialBlastIndex > 0
                        ? "Glacial Blast was delayed behind another cast."
                        : "No Glacial Blast was fitted into the Winter's Embrace window.";

            evaluations.Add(new StComboWindowEvaluation(
                startTimestamp,
                endTimestamp,
                cast.TargetId,
                outcome,
                successful,
                partial,
                relevant));
        }

        var evaluatedWindows = evaluations.Count;
        var successfulWindows = evaluations.Count(w => w.Successful);
        var partialWindows = evaluations.Count(w => w.Partial);
        var score = evaluatedWindows == 0
            ? 0
            : (int)Math.Round(((successfulWindows + (partialWindows * 0.5)) / evaluatedWindows) * 100);

        if (evaluatedWindows == 0)
        {
            findings.Add(new RimeAnalyzerFinding("info", "No single-target Bursting Ice windows were detected in the sample."));
        }
        else
        {
            findings.Add(new RimeAnalyzerFinding("info",
                $"{successfulWindows} of {evaluatedWindows} evaluated Bursting Ice windows matched the Basic ST combo."));

            foreach (var failedWindow in evaluations.Where(w => !w.Successful).Take(5))
            {
                findings.Add(new RimeAnalyzerFinding(
                    failedWindow.Partial ? "warning" : "major",
                    failedWindow.Outcome,
                    failedWindow.StartTimestamp));
            }
        }

        var summary = evaluatedWindows == 0
            ? "No ST windows detected in the sample."
            : $"{successfulWindows}/{evaluatedWindows} ST windows cleanly fit Glacial Blast and a finisher inside Winter's Embrace.";

        ScoreCard = new AnalyzerScoreCard("Basic ST Combo", score, summary,
            score >= 75 ? "ice" : score >= 50 ? "amber" : "ember");
        EvaluatedWindows = evaluatedWindows;
        SuccessfulWindows = successfulWindows;
        PartialWindows = partialWindows;
        IgnoredAoeWindows = ignoredAoeWindows;
        Windows = evaluations;
        Findings = findings;
    }

    public sealed record StComboWindowEvaluation(
        int StartTimestamp,
        int EndTimestamp,
        int TargetId,
        string Outcome,
        bool Successful,
        bool Partial,
        IReadOnlyList<TrackedAbilityCast> CastsInWindow);
}
