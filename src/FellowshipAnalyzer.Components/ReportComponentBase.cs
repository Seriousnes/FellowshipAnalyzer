using FellowshipAnalyzer.Core.Analysis;
using Microsoft.AspNetCore.Components;

namespace FellowshipAnalyzer.Components;

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

    /// <summary>Absolute fight start timestamp in milliseconds.</summary>
    protected int FightStartTime => FightTime.StartTime;

    /// <summary>Absolute fight end timestamp in milliseconds.</summary>
    protected int FightEndTime => FightTime.EndTime;
}
