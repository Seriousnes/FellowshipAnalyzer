using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Immutable per-pull projection of <see cref="HighwindArrowCapAnalyzer"/> state, produced by
/// <see cref="HighwindArrowCapAnalyzer.OnPullEnd"/> for each single-target pull.
/// </summary>
public sealed record HighwindArrowCapReport(
    int TotalTimeAtCapMs,
    int CastsWhileCapped,
    int TotalCasts,
    int PullDurationMs) : IResult
{
    public HighwindArrowCapReport() : this(0, 0, 0, 0) { }

    public double CapPercentage => PullDurationMs > 0 ? (double)TotalTimeAtCapMs / PullDurationMs : 0;
}
