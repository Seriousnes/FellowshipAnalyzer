using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

[Dependency<SylvieManaTracker>]
public sealed partial class SylvieHealingEfficiencyTracker : HealingEfficiencyTracker
{
    public override StatisticCategory StatisticCategory => StatisticCategory.General;

    protected override int ResourceCostOf(CastEvent castEvent) => SylvieManaTracker.CostOf(castEvent);
}
