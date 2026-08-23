using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Gunde;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Gunde.Modules;

[ForPull(PullKind.Multi)]
public sealed partial class RendSpreadAnalyzer : DotSpreadAnalyzer, IRendAnalyzer
{
    protected override List<Dot> Dots => GundeDots.All;

    public int DistinctTargets => TargetsOf(GundeDots.Rend);

    public double Coverage => CoverageOf(GundeDots.Rend);

    public int TotalApplications => ApplicationsOf(GundeDots.Rend);

    [On<ApplyDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnApplied(ApplyDebuffEvent e) => RecordApplication(e);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spell = nameof(Spells.Rend))]
    private void OnRefreshed(RefreshDebuffEvent e) => RecordRefresh(e);
}
