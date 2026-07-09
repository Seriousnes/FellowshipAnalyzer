using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Immutable per-pull projection of <see cref="EngulfingFlamesEconomyAnalyzer"/> state. Captures how
/// many Wildfire windows opened with both Engulfing Flames charges ready and how much recharge was
/// wasted by Engulfing Flames sitting at maximum charges. Serializable via the source-generated
/// <c>JsonSerializerContext</c>, so it can be cached or sent across worker boundaries.
/// </summary>
public sealed record EngulfingFlamesEconomyReport(
    AnalyzerScoreCard ScoreCard,
    int WindowsEvaluated,
    int WindowsReady,
    int WindowsShort,
    int WastedCharges,
    double CappedSeconds,
    IReadOnlyList<EngulfingFlamesEconomyAnalyzer.WindowReadiness> Windows,
    IReadOnlyList<ArdeosFinding> Findings) : IResult
{
    public EngulfingFlamesEconomyReport() : this(
        new AnalyzerScoreCard("Engulfing Flames Economy", 0, "No Engulfing Flames economy detected.", "ember"),
        0, 0, 0, 0, 0d, [], [])
    {
    }
}
