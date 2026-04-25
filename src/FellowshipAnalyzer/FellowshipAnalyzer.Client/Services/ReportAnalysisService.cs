using System.Text.Json;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;

using Microsoft.Extensions.DependencyInjection;



namespace FellowshipAnalyzer.Client.Services;

/// <summary>
/// Returned by <see cref="ReportAnalysisService.RunAsync"/> once a fight has been
/// fully fetched and analyzed. All display-layer data is read directly from these
/// objects — no redundant string fields.
/// </summary>
public sealed record ReportAnalysisContext(
    ReportInfo ReportInfo,
    ReportFight Fight,
    ReportActor? Player,
    HeroAnalysisResult Analysis,
    IHeroAnalyzer Analyzer,
    int FightStartTime,
    int FightEndTime
);

/// <summary>
/// Orchestrates the full analysis pipeline for a single fight:
/// concurrent API fetch, master-data loading, hero resolution, analysis, and caching.
/// Extracted from Report.razor to keep the component a thin view layer.
/// </summary>
public sealed class ReportAnalysisService(
    IFellowshipLogsClient fellowshipLogs,
    ReportLoadingTracker loadingTracker,
    ReportMasterDataService masterDataService,
    IServiceProvider serviceProvider,
    IReportCacheService reportCache,
    JsonSerializerOptions jsonOptions,
    ReportNavigationState navState)
{
    public async Task<ReportAnalysisContext> RunAsync(string reportCode, int fightId, int playerId)
    {
        loadingTracker.Reset();
        loadingTracker.FetchEventsState = ReportLoadingTracker.StepState.Loading;
        await Task.Yield();

        // Start preload and the local cache check concurrently. The cache check is fast (IndexedDB)
        // and determines whether a live events request is needed at all. Awaiting it first lets us
        // fire the events request immediately on a miss so it overlaps with the remaining preload wait.
        var preloadTask = fellowshipLogs.AnalysisPreload.GetAsync(reportCode);
        var eventsRequest = new FellowshipLogsEventsRequest(reportCode, playerId, fightId);
        var cachedEventsBytesTask = reportCache.GetCachedEventsBytesAsync(reportCode, fightId, playerId).AsTask();

        var cachedEventsBytes = await cachedEventsBytesTask;
        // On a miss, fetch raw UTF-8 JSON bytes only — defer deserialization to its own step so we
        // can measure network I/O vs JSON parsing separately, and cache the network bytes verbatim.
        Task<RawEventsResponse>? liveEventsRawTask = cachedEventsBytes is null
            ? fellowshipLogs.Events.GetRawBytesAsync(eventsRequest)
            : null;

        var preload = await preloadTask;
        var reportInfo = preload.ReportInfo;
        masterDataService.Load(preload.MasterData);

        var fight = reportInfo.Fights.FirstOrDefault(f => f.Id == fightId)
            ?? throw new InvalidOperationException($"Fight {fightId} not found in report.");

        navState.Set(reportCode, reportInfo);

        // The JSON bytes we hold at this point are always shaped as EventsResult: { events: [...], inProgress: bool }.
        // On hit it came from IndexedDB; on miss it came directly from the proxy and has not yet been parsed.
        byte[] eventsResultJsonBytes = cachedEventsBytes ?? (await liveEventsRawTask!).JsonBytes;
        bool isFreshFromNetwork = cachedEventsBytes is null;

        loadingTracker.FetchEventsState = ReportLoadingTracker.StepState.Ok;
        loadingTracker.DeserializeState = ReportLoadingTracker.StepState.Loading;
        await Task.Yield();

        var eventsResult = JsonSerializer.Deserialize<EventsResult>(eventsResultJsonBytes, jsonOptions)
            ?? throw new InvalidOperationException("Event data could not be deserialized.");
        var events = eventsResult.Events.ToList();

        loadingTracker.DeserializeState = ReportLoadingTracker.StepState.Ok;
        await Task.Yield();

        var heroId = masterDataService.GetHeroId(playerId)
            ?? throw new InvalidOperationException($"Could not determine hero for player {playerId}.");
        var analyzer = serviceProvider.GetKeyedService<IHeroAnalyzer>(heroId)
            ?? throw new InvalidOperationException($"No hero analyzer found for '{heroId}'.");

        var fightStartTime = (int)fight.StartTime;
        var fightEndTime = (int)fight.EndTime;

        analyzer.ActorNames = reportInfo.Actors.ToDictionary(a => a.Id, a => a.Name);
        var result = await analyzer.Analyze(events, playerId, fightStartTime);

        loadingTracker.PrepareDisplayState = ReportLoadingTracker.StepState.Loading;
        await Task.Yield();

        // Cache only fresh, completed network responses — never overwrite from a cache-hit path,
        // and never cache an in-progress fight (which may still be receiving events).
        if (isFreshFromNetwork && !eventsResult.InProgress)
        {
            var player = reportInfo.Actors.FirstOrDefault(a => a.Id == playerId);
            var entry = new ReportHistoryEntry(
                reportCode, fightId, playerId,
                fight.Name, player?.Name, heroId,
                DateTimeOffset.UtcNow);
            await reportCache.CacheAsync(entry, eventsResultJsonBytes);
        }

        loadingTracker.PrepareDisplayState = ReportLoadingTracker.StepState.Ok;
        await Task.Yield();

        return new ReportAnalysisContext(
            reportInfo,
            fight,
            reportInfo.Actors.FirstOrDefault(a => a.Id == playerId),
            result,
            analyzer,
            fightStartTime,
            fightEndTime);
    }
}
