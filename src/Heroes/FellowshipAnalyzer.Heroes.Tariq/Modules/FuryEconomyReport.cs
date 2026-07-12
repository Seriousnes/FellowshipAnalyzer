using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Tariq.Modules;

/// <summary>A pull-level Fury-economy observation surfaced in the guide's findings list.</summary>
public sealed record TariqFinding(string Severity, string Message);

/// <summary>
/// Immutable per-pull projection of <see cref="FuryEconomyAnalyzer"/> state, produced by
/// <see cref="FuryEconomyAnalyzer.OnPullEnd"/> for each pull. The scored axis is Fury overcap
/// (generation performed while Fury is already at cap is wasted); empowered-spend share and
/// execute usage are reported alongside as build-robust supporting facts.
/// </summary>
public sealed record FuryEconomyReport(
    AnalyzerScoreCard ScoreCard,
    int GeneratorCasts,
    int OvercapCasts,
    int SpenderCasts,
    int EmpoweredSpenderCasts,
    int ThunderCallWindows,
    int WindowTimeMs,
    int ActiveSpanMs,
    int CullingStrikeCasts,
    int ExecuteCullingStrikes,
    int SkullCrusherCasts,
    int HammerStormCasts,
    IReadOnlyList<TariqFinding> Findings,
    int GrinEnabledCullingStrikes) : IResult
{
    public FuryEconomyReport() : this(
        new AnalyzerScoreCard("Fury Economy", 0, "No Fury activity detected.", "ember"),
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], 0)
    {
    }

    /// <summary>Fraction of generator casts wasted by generating at Fury cap.</summary>
    public double OvercapRate => GeneratorCasts == 0 ? 0 : (double)OvercapCasts / GeneratorCasts;

    /// <summary>Fraction of Fury spenders cast inside a Thunder Call empowerment window.</summary>
    public double EmpoweredSpendShare => SpenderCasts == 0 ? 0 : (double)EmpoweredSpenderCasts / SpenderCasts;

    /// <summary>
    /// Fraction of the active pull span the Thunder Call window was up — the neutral baseline the
    /// empowered-spend share is read against (share above uptime means spenders were concentrated).
    /// </summary>
    public double WindowUptimeShare => ActiveSpanMs <= 0 ? 0 : Math.Min(1.0, (double)WindowTimeMs / ActiveSpanMs);
}
