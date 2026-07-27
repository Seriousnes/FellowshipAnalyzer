using FellowshipAnalyzer.Core.UI;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Base type for parse-lifetime state built once per report. A module subscribes its
/// <c>[On&lt;T&gt;]</c> handlers via the generated <c>RegisterSubscriptions()</c> and accumulates
/// state across the whole event stream, unlike a pull-lifetime <see cref="Analyzer"/>.
/// </summary>
public abstract class Module
{
    /// <summary>Whether this module currently accepts dispatched events. <see cref="EventEmitter"/> checks this per event, so it can be toggled at runtime by dynamic activation conditions (gear, observed talents, etc.).</summary>
    public bool Active { get; protected set; } = true;

    /// <summary>The order this module is constructed and subscribed relative to other modules, assigned by the parser from <c>[Before&lt;T&gt;]</c> / <c>[After&lt;T&gt;]</c> ordering and declaration order.</summary>
    public int Priority { get; set; }

    /// <summary>The parser instance that constructed and owns this module.</summary>
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

    /// <summary>The FellowshipLogs actor id of the player this module's owning parser is analyzing.</summary>
    protected int PlayerId => Owner.PlayerId;
}
