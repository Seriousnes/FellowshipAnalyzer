using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Ardeos;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Ardeos.Core;

namespace FellowshipAnalyzer.Heroes.Ardeos.Modules;

[ForPull(PullKind.Multi)]
public sealed partial class SearingBlazeSpreadAnalyzer : DotSpreadAnalyzer, ISearingBlazeAnalyzer
{
    protected override List<Dot> Dots { get; } = [ArdeosDots.SearingBlaze];

    public int DistinctTargets => TargetsOf(ArdeosDots.SearingBlaze);

    public double Coverage => CoverageOf(ArdeosDots.SearingBlaze);

    public int TotalApplications => ApplicationsOf(ArdeosDots.SearingBlaze);

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnApplied(ApplyDebuffEvent e) => RecordApplication(e);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.SearingBlazeDot))]
    private void OnRefreshed(RefreshDebuffEvent e) => RecordRefresh(e);
}
