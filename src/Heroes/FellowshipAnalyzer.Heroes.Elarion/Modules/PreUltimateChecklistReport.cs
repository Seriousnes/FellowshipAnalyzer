using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Immutable per-pull projection of <see cref="PreUltimateChecklistAnalyzer"/> state, produced by
/// <see cref="PreUltimateChecklistAnalyzer.OnPullEnd"/> for each pull.
/// </summary>
public sealed record PreUltimateChecklistReport(
    IReadOnlyList<PreUltimateChecklistAnalyzer.UltWindow> Windows,
    int Score) : IResult
{
    public PreUltimateChecklistReport() : this([], 0) { }
}
