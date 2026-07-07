using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// A single analyzer finding for an Ardeos report. Kept independent of the UI layer so the report
/// stays a pure analysis projection.
/// </summary>
public sealed record ArdeosFinding(
    string Severity,
    string Message,
    int? Timestamp = null);

/// <summary>
/// Immutable per-pull projection of <see cref="WildfireComboAnalyzer"/> state. Scores each burn
/// window anchored on a Wildfire cast against the setup rotation that should precede it and the
/// Detonate spam that should follow. Serializable via the source-generated
/// <c>JsonSerializerContext</c>, so it can be cached or sent across worker boundaries.
/// </summary>
public sealed record WildfireComboReport(
    AnalyzerScoreCard ScoreCard,
    int EvaluatedWindows,
    int SuccessfulWindows,
    int PartialWindows,
    int WindowsWithPyromania,
    int WindowsWithIncinerate,
    double AverageDetonateCasts,
    IReadOnlyList<WildfireComboAnalyzer.WildfireWindowEvaluation> Windows,
    IReadOnlyList<ArdeosFinding> Findings) : IResult
{
    public WildfireComboReport() : this(
        new AnalyzerScoreCard("Wildfire Burn Windows", 0, "No Wildfire windows detected.", "ember"),
        0, 0, 0, 0, 0, 0d, [], [])
    {
    }
}
