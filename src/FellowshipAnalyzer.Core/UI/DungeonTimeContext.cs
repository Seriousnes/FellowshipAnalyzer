namespace FellowshipAnalyzer.Core.UI;

/// <summary>
/// Report-scoped dungeon time information cascaded from the report shell to all
/// report-scoped components. Components inheriting <see cref="FellowshipAnalyzer.Core.UI.Components.ReportComponent"/>
/// receive this automatically.
/// </summary>
/// <param name="StartTime">Absolute dungeon start timestamp in milliseconds (matches the log timeline).</param>
/// <param name="EndTime">Absolute dungeon end timestamp in milliseconds.</param>
public sealed record DungeonTimeContext(int StartTime, int EndTime)
{
    /// <summary>Total dungeon duration in milliseconds.</summary>
    public int Duration => EndTime - StartTime;
}
