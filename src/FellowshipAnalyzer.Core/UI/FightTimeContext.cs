namespace FellowshipAnalyzer.Core.UI;

/// <summary>
/// Report-scoped fight time information cascaded from the report shell to all
/// report-scoped components. Components inheriting <see cref="ReportComponentBase"/>
/// receive this automatically.
/// </summary>
/// <param name="StartTime">Absolute fight start timestamp in milliseconds (matches the log timeline).</param>
/// <param name="EndTime">Absolute fight end timestamp in milliseconds.</param>
public sealed record FightTimeContext(int StartTime, int EndTime)
{
    /// <summary>Total fight duration in milliseconds.</summary>
    public int Duration => EndTime - StartTime;
}
