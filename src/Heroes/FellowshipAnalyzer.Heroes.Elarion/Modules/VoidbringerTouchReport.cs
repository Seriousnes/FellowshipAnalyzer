using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Immutable per-pull projection of <see cref="VoidbringerTouchAnalyzer"/> state, produced by
/// <see cref="VoidbringerTouchAnalyzer.OnPullEnd"/> for each pull.
/// </summary>
public sealed record VoidbringerTouchReport(
    IReadOnlyList<VoidbringerTouchAnalyzer.VoidbringerCast> Casts,
    int TotalCasts,
    int DoubleMarks) : IResult
{
    public VoidbringerTouchReport() : this([], 0, 0) { }
}
