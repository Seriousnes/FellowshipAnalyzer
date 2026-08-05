using FellowshipAnalyzer.Core.UI;

using Microsoft.AspNetCore.Components;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Base type for parse-lifetime state built once per report, registered with <c>[AddModule&lt;T&gt;]</c>
/// or <c>[AddState&lt;T&gt;]</c>. A module subscribing to events derives from <see cref="Analyzer"/>
/// instead, which is what <c>[AddAnalyzer&lt;T&gt;]</c> registers.
/// </summary>
public abstract class Module : ComponentBase
{
    /// <summary>Whether this module currently accepts dispatched events. <see cref="EventEmitter"/> checks this per event, so it can be toggled at runtime by dynamic activation conditions (gear, observed talents, etc.).</summary>
    public bool Active { get; protected set; } = true;

    /// <summary>
    /// Dispatch order for this module's handlers relative to other modules', fixed at design time by
    /// overriding this property. Lower runs first; the default is 0. Modules sharing a priority run in
    /// no guaranteed order beyond the pairwise constraints <c>[Before&lt;T&gt;]</c> and
    /// <c>[After&lt;T&gt;]</c> declare.
    /// </summary>
    public virtual int Priority => 0;

    /// <summary>The parser instance that constructed and owns this module.</summary>
    public CombatLogParser Owner { get; set; } = null!;

    /// <summary>
    /// This module's card for the Statistics tab, or null when the module has nothing to report and
    /// is omitted from the tab. Authored as razor markup in the module's companion
    /// <c>{Module}.razor</c> partial.
    /// </summary>
    public virtual RenderFragment? Statistic => null;

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
