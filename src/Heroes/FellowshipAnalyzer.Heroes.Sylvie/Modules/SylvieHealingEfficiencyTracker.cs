using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Heroes.Sylvie.Modules;

/// <summary>
/// What each of Sylvie's spells healed for against the mana and the global cooldown it cost. The costs
/// come from <see cref="SylvieManaTracker"/>, which prices them from the game data because the log
/// attaches no cost to a cast.
/// </summary>
[Dependency<SylvieManaTracker>]
public sealed partial class SylvieHealingEfficiencyTracker : HealingEfficiencyTracker
{
    /// <inheritdoc/>
    public override StatisticCategory StatisticCategory => StatisticCategory.General;

    /// <inheritdoc/>
    protected override int ResourceCostOf(CastEvent castEvent) => SylvieManaTracker.CostOf(castEvent);
}
