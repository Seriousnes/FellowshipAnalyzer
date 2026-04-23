using FellowshipAnalyzer.Core.FellowshipLogs;

namespace FellowshipAnalyzer.Client.Services;

/// <summary>
/// Scoped service that caches report info during a navigation session,
/// allowing the analysis page to display fight/player names without an extra API call.
/// </summary>
public sealed class ReportNavigationState
{
    private readonly Dictionary<string, FellowshipLogsReportInfo> _cache = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string code, FellowshipLogsReportInfo reportInfo) =>
        _cache[code] = reportInfo;

    public bool TryGet(string code, out FellowshipLogsReportInfo? reportInfo) =>
        _cache.TryGetValue(code, out reportInfo);

    public void Clear() => _cache.Clear();
}
