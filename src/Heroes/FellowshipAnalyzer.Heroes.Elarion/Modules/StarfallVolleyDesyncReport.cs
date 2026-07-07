using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>
/// Immutable per-pull projection of <see cref="StarfallVolleyDesyncAnalyzer"/> state, produced by
/// <see cref="StarfallVolleyDesyncAnalyzer.OnPullEnd"/> for each pull it runs on.
/// </summary>
public sealed record StarfallVolleyDesyncReport(
    IReadOnlyList<StarfallVolleyDesyncAnalyzer.DesyncEvent> Events,
    int CloseGapCount,
    int VolleyCount,
    double AverageGapMs) : IResult
{
    public StarfallVolleyDesyncReport() : this([], 0, 0, 0) { }
}
