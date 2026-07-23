using FellowshipAnalyzer.Core.Analysis;

using Microsoft.AspNetCore.Components;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Base class for any report-scoped component that needs access to report-wide context
/// such as fight time bounds. Cascading values are supplied by the report shell
/// (<c>Report.razor</c>) and inherited automatically by derived components via
/// <c>@inherits ReportComponentBase</c>.
/// </summary>
/// <remarks>
/// Future report-scoped cascades (e.g. report filter context, selected player) should be
/// added here as additional <c>[CascadingParameter]</c> properties so all consumers pick
/// them up uniformly.
/// </remarks>
public abstract class ReportComponentBase : ComponentBase
{
    [CascadingParameter]
    protected FightTimeContext FightTime { get; set; } = null!;

    [CascadingParameter]
    protected HeroAnalysisResult Result {get; set; } = null!;

    [CascadingParameter]
    public IHeroAnalyzer Owner { get; set; } = null!;

    /// <summary>
    /// The pull the report is currently clamped to, or <c>null</c> for the whole fight ("Entire
    /// Dungeon"). Supplied by the report shell as a named cascade so every report-scoped component
    /// crops its view to the same pull window.
    /// </summary>
    [CascadingParameter(Name = "SelectedPull")]
    protected Pull? SelectedPull { get; set; }

    /// <summary>Absolute fight start timestamp in milliseconds.</summary>
    protected int FightStartTime => FightTime.StartTime;

    /// <summary>Absolute fight end timestamp in milliseconds.</summary>
    protected int FightEndTime => FightTime.EndTime;
}
