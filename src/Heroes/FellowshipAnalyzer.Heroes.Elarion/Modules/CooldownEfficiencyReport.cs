using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Heroes.Elarion.Modules;

/// <summary>Per-ability cooldown-usage snapshot for one pull.</summary>
public readonly record struct CooldownSnapshot(int Casts, int HeldMs, int BuffUptimeMs);

/// <summary>
/// Immutable per-pull projection of <see cref="CooldownEfficiencyAnalyzer"/> state, produced by
/// <see cref="CooldownEfficiencyAnalyzer.OnPullEnd"/> for each pull.
/// </summary>
public sealed record CooldownEfficiencyReport(
    CooldownSnapshot Grace,
    CooldownSnapshot EventHorizon,
    int PullDurationMs) : IResult
{
    public CooldownEfficiencyReport() : this(default, default, 0) { }
}
