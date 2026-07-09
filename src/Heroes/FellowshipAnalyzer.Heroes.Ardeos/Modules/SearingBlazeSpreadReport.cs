using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// Immutable per-pull projection of <see cref="SearingBlazeSpreadAnalyzer"/> state. Captures how many
/// distinct enemies took the Searing Blaze DoT from the player and coverage against the pull's enemy
/// roster. Serializable via the source-generated <c>JsonSerializerContext</c>, so it can be cached or
/// sent across worker boundaries.
/// </summary>
public sealed record SearingBlazeSpreadReport(
    AnalyzerScoreCard ScoreCard,
    int DistinctTargets,
    int TargetCount,
    double Coverage,
    int TotalApplications,
    IReadOnlyList<ArdeosFinding> Findings) : IResult
{
    public SearingBlazeSpreadReport() : this(
        new AnalyzerScoreCard("Searing Blaze Spread", 0, "No Searing Blaze spread detected.", "ember"),
        0, 0, 0d, 0, [])
    {
    }
}
