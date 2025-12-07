using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.FellowshipLogs.API.Functions;

namespace FellowshipAnalyzer.FellowshipLogs.API;

internal sealed class ApiClient : IFellowshipLogsClient
{
    public ApiClient(IApiRequestExecutor api, FellowshipLogsClientOptions options)
    {
        Report = new ReportFunction(api, options);
        Events = new EventsFunction(api, options);
    }

    public IReportFunction Report { get; }
    public IEventsFunction Events { get; }
}
