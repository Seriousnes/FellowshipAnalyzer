using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis;

public abstract class Module
{
    public bool Active { get; protected set; } = true;

    public int Priority { get; set; }

    public CombatLogParser Owner { get; set; } = null!;

    /// <summary>
    /// Override with a non-null <see cref="Type"/> to make this module's statistics
    /// auto-collected and rendered on the Statistics tab via DynamicComponent.
    /// The type must be a Razor component that inherits AnalyzerStatistic&lt;T&gt;.
    /// </summary>
    public virtual Type? StatisticsComponentType => null;

    /// <summary>
    /// Section of the Statistics tab this module's statistic renders under.
    /// Defaults to <see cref="StatisticCategory.General"/>.
    /// </summary>
    public virtual StatisticCategory StatisticCategory => StatisticCategory.General;

    /// <summary>
    /// Position within the statistic's section.
    /// Defaults to <see cref="StatisticOrder.Default"/>.
    /// </summary>
    public virtual StatisticOrder StatisticOrder => StatisticOrder.Default;

    protected int PlayerId => Owner.PlayerId;
}
