using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Core.Utility;

public static class ReportFightExtensions
{
    public static TimeSpan Duration(this ReportFight fight) => TimeSpan.FromMilliseconds(fight.EndTime - fight.StartTime);
}