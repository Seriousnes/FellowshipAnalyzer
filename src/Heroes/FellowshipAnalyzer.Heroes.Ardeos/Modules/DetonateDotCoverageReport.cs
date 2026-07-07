using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

/// <summary>
/// A single analyzer finding for the Detonate DoT-coverage report. Kept independent of the UI
/// layer so the report stays a pure analysis projection.
/// </summary>
public sealed record ArdeosFinding(
    string Severity,
    string Message,
    int? Timestamp = null);

/// <summary>
/// Immutable per-pull projection of <see cref="DetonateDotCoverageAnalyzer"/> state. Measures how
/// many of Ardeos's damage-over-time effects were active on each target at the moment Detonate hit
/// it — the quantity Detonate's damage scales with. <see cref="DistributionByDotCount"/> is indexed
/// by the number of active DoTs (index 0 = detonated a target with no DoTs).
/// </summary>
public sealed record DetonateDotCoverageReport(
    AnalyzerScoreCard ScoreCard,
    int DetonateCasts,
    int DetonateHits,
    double AverageDotsPerHit,
    int MaxDotsObserved,
    int WellLayeredHits,
    int UnderLayeredHits,
    IReadOnlyList<int> DistributionByDotCount,
    IReadOnlyList<ArdeosFinding> Findings) : IResult
{
    public DetonateDotCoverageReport() : this(
        new AnalyzerScoreCard("Detonate DoT Coverage", 0, "No Detonate casts detected.", "ember"),
        0, 0, 0d, 0, 0, 0, [], [])
    {
    }
}
