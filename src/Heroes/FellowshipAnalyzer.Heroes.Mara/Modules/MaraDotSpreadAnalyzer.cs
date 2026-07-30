using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Mara;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Heroes.Mara.Modules;

[ForPull(PullKind.Multi)]
public sealed partial class MaraDotSpreadAnalyzer : DotSpreadAnalyzer, IMaraDotAnalyzer
{
    protected override IReadOnlyList<Dot> Dots => MaraDots.All;

    public int HemorrhageTargets => TargetsOf(MaraDots.Hemorrhage);

    public int HemorrhageApplications => ApplicationsOf(MaraDots.Hemorrhage);

    public int SeethingPoisonTargets => TargetsOf(MaraDots.SeethingPoison);

    public int VolatilePoisonTargets => TargetsOf(MaraDots.VolatilePoison);

    public int VolatilePoisonApplications => ApplicationsOf(MaraDots.VolatilePoison);

    public int VolatilePoisonRefreshes => RefreshesOf(MaraDots.VolatilePoison);

    public double Coverage => CoverageOf(MaraDots.Hemorrhage);

    [On<ApplyDebuffEvent>(By = Actor.Player, Spells = [
        nameof(Spells.WidowBitePoison),
        nameof(Spells.HemorrhagingStrikeBleed),
        nameof(Spells.SkitteringBladesPoison)])]
    private void OnApplied(ApplyDebuffEvent e) => RecordApplication(e);

    [On<RefreshDebuffEvent>(By = Actor.Player, Spells = [
        nameof(Spells.WidowBitePoison),
        nameof(Spells.HemorrhagingStrikeBleed),
        nameof(Spells.SkitteringBladesPoison)])]
    private void OnRefreshed(RefreshDebuffEvent e) => RecordRefresh(e);
}
