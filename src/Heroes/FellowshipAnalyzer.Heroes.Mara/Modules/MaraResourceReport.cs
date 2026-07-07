using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.UI.Guides;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

/// <summary>
/// The target shape a <see cref="MaraResourceDisciplineAnalyzer"/> leaf scored against, mirroring
/// the pull's <see cref="PullKind"/>. Used by the guide to label single-target vs AoE pulls.
/// </summary>
public enum MaraPullShape
{
    SingleTarget,
    Aoe,
}

/// <summary>
/// One combo-point-spending finisher cast (Queen's Fang or Arachnid Assault) with the combo points
/// available when it was pressed and whether that met the pull shape's spend threshold.
/// </summary>
public sealed record MaraFinisherCast(
    int Timestamp,
    int AbilityId,
    int ComboPoints,
    bool MeetsThreshold);

/// <summary>
/// Immutable per-pull projection of <see cref="MaraResourceDisciplineAnalyzer"/> state, produced by
/// <see cref="MaraResourceDisciplineAnalyzer.OnPullEnd"/> for each pull. Carries finisher combo-point
/// discipline and Energy / combo-point overcap for the pull.
/// </summary>
public sealed record MaraResourceReport(
    AnalyzerScoreCard ScoreCard,
    MaraPullShape Shape,
    int FinisherCpThreshold,
    int FinishersWithData,
    int FinishersAtThreshold,
    int MaintenanceFinisherCasts,
    int GeneratorCasts,
    int GeneratorOvercapCasts,
    int EnergyCastsSampled,
    int EnergyCappedCasts,
    IReadOnlyList<MaraFinisherCast> Finishers,
    IReadOnlyList<Finding> Findings) : IResult
{
    public MaraResourceReport() : this(
        new AnalyzerScoreCard("Resource Discipline", 0, "No finisher casts detected.", "ember"),
        MaraPullShape.SingleTarget, 5, 0, 0, 0, 0, 0, 0, 0, [], [])
    {
    }
}
