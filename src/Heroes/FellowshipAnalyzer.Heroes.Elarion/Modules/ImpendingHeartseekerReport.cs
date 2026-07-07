using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Immutable per-pull projection of <see cref="ImpendingHeartseekerAnalyzer"/> state, produced by
/// <see cref="ImpendingHeartseekerAnalyzer.OnPullEnd"/> for each pull.
/// </summary>
public sealed record ImpendingHeartseekerReport(
    IReadOnlyList<ImpendingHeartseekerAnalyzer.ProcEvent> Procs,
    int Gains,
    int Refreshes,
    int Consumed,
    int Expired) : IResult
{
    public ImpendingHeartseekerReport() : this([], 0, 0, 0, 0) { }
}
