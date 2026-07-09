using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Immutable per-pull projection of <see cref="LunarlightMarkEruptionAnalyzer"/> state, produced by
/// <see cref="LunarlightMarkEruptionAnalyzer.OnPullEnd"/> for each pull.
/// </summary>
public sealed record LunarlightMarkEruptionReport(
    IReadOnlyList<LunarlightMarkEruptionAnalyzer.BarrageEvent> Barrages,
    int TotalMarksApplied,
    int MarksExpired,
    int BarrageWithEruption,
    int BarrageWithoutEruption) : IResult
{
    public LunarlightMarkEruptionReport() : this([], 0, 0, 0, 0) { }
}
