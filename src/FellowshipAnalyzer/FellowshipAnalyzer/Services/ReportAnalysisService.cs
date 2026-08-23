using System.Diagnostics;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;



namespace FellowshipAnalyzer.Services;

/// <summary>
/// Returned by <see cref="ReportAnalysisService.RunAsync"/> once a dungeon has been
/// fully fetched and analyzed. All display-layer data is read directly from these
/// objects - no redundant string fields.
/// <para>
/// When <see cref="Analysis"/> is <c>null</c> the report belongs to a hero with no
/// implemented analysis yet (WIP). In that case the events fetch and analysis are
/// skipped entirely; the host should render the WIP placeholder.
/// </para>
/// </summary>
public sealed record ReportAnalysisContext(
    ReportInfo ReportInfo,
    ReportDungeon Dungeon,
    ReportActor Player,
    HeroAnalysisResult? Analysis,
    IHeroAnalyzer Analyzer,
    int DungeonStartTime,
    int DungeonEndTime
)
{
    /// <summary>
    /// True when the resolved hero analyzer has no guide component, i.e. analysis
    /// has not been implemented for this hero. In this state <see cref="Analysis"/>
    /// is <c>null</c> and no events were fetched.
    /// </summary>
    public bool IsWip => Analysis is null;
}

/// <summary>
/// Orchestrates the full analysis pipeline for a single dungeon:
/// concurrent API fetch, master-data loading, hero resolution, analysis, and caching.
/// </summary>
public sealed class ReportAnalysisService(
    FellowshipLogsApiClient fellowshipLogs,
    ReportLoadingTracker loadingTracker,
    ReportMasterDataService masterDataService,
    IServiceProvider serviceProvider,
    IReportCacheService reportCache,
    FellowshipAnalyzerJsonContext jsonContext,
    ReportNavigationState navState,
    ILogger<ReportAnalysisService> logger)
{
    public async Task<ReportAnalysisContext> RunAsync(
        string reportCode,
        int dungeonId,
        int playerId,
        Func<ReportInfo, Task>? reportInfoLoaded = null)
    {
        loadingTracker.Reset();
        loadingTracker.FetchEventsState = ReportLoadingTracker.StepState.Loading;
        await Task.Yield();

        var preload = await fellowshipLogs.GetAnalysisPreloadAsync(reportCode, dungeonId);
        var reportInfo = preload.ReportInfo;
        masterDataService.Load(preload.MasterData);

        var dungeon = reportInfo.Dungeons.FirstOrDefault(f => f.Id == dungeonId)
            ?? throw new InvalidOperationException($"Dungeon {dungeonId} not found in report.");
        var player = reportInfo.Actors.FirstOrDefault(a => a.Id == playerId)
            ?? throw new InvalidOperationException($"Player {playerId} not found in report.");

        navState.Set(reportCode, reportInfo);
        if (reportInfoLoaded is not null)
        {
            await reportInfoLoaded(reportInfo);
        }

        var hero = masterDataService.GetHero(playerId)
            ?? throw new InvalidOperationException($"Could not determine hero for player {playerId}.");
        var analyzer = serviceProvider.GetKeyedService<IHeroAnalyzer>(hero.Name)
            ?? throw new InvalidOperationException($"No hero analyzer found for '{hero.Name}'.");

        var dungeonStartTime = (int)dungeon.StartTime;
        var dungeonEndTime = (int)dungeon.EndTime;

        if (analyzer.GuideComponent is null)
        {
            loadingTracker.FetchEventsState = ReportLoadingTracker.StepState.Ok;
            loadingTracker.DeserializeState = ReportLoadingTracker.StepState.Ok;
            loadingTracker.NormalizeState = ReportLoadingTracker.StepState.Ok;
            loadingTracker.AnalyzeState = ReportLoadingTracker.StepState.Ok;
            loadingTracker.PrepareDisplayState = ReportLoadingTracker.StepState.Ok;
            return new ReportAnalysisContext(
                reportInfo,
                dungeon,
                player,
                Analysis: null,
                analyzer,
                dungeonStartTime,
                dungeonEndTime);
        }

        var sw = Stopwatch.StartNew();
        logger.LogInformation(
            "RunAsync events fetch starting reportCode={ReportCode} dungeonId={DungeonId} playerId={PlayerId}",
            reportCode, dungeonId, playerId);

        logger.LogInformation("RunAsync IndexedDB cache lookup starting t={ElapsedMs}ms", sw.ElapsedMilliseconds);
        var cachedEventsBytes = await reportCache.GetCachedEventsBytesAsync(reportCode, dungeonId, playerId);
        logger.LogInformation(
            "RunAsync IndexedDB cache lookup result hit={Hit} bytes={Bytes} t={ElapsedMs}ms",
            cachedEventsBytes is not null, cachedEventsBytes?.Length, sw.ElapsedMilliseconds);

        byte[] eventsResultJsonBytes;
        DateTimeOffset? eventsExpiresAt = null;
        bool isFreshFromNetwork;

        if (cachedEventsBytes is not null)
        {
            eventsResultJsonBytes = cachedEventsBytes;
            isFreshFromNetwork = false;
        }
        else
        {
            logger.LogInformation("RunAsync network fetch starting t={ElapsedMs}ms", sw.ElapsedMilliseconds);
            var eventsFetch = fellowshipLogs.GetRawEventsAsync(reportCode, playerId, dungeonId);
            var deathsFetch = fellowshipLogs.GetRawDeathsAsync(reportCode, dungeonId);
            await Task.WhenAll(eventsFetch, deathsFetch);
            var eventsResponse = await eventsFetch;
            var deathsResponse = await deathsFetch;
            logger.LogInformation(
                "RunAsync network fetch returned eventBytes={EventBytes} deathBytes={DeathBytes} t={ElapsedMs}ms",
                eventsResponse.Bytes.Length, deathsResponse.Bytes.Length, sw.ElapsedMilliseconds);

            eventsResultJsonBytes = EventStreamMerger.Merge(eventsResponse.Bytes, deathsResponse.Bytes);
            eventsExpiresAt = (eventsResponse.ExpiresAt, deathsResponse.ExpiresAt) switch
            {
                ({ } eventsExpiry, { } deathsExpiry) => eventsExpiry <= deathsExpiry ? eventsExpiry : deathsExpiry,
                ({ } eventsExpiry, null) => eventsExpiry,
                (null, var deathsExpiry) => deathsExpiry,
            };
            isFreshFromNetwork = true;
        }

        loadingTracker.FetchEventsState = ReportLoadingTracker.StepState.Ok;
        loadingTracker.DeserializeState = ReportLoadingTracker.StepState.Loading;
        await Task.Yield();

        var eventsResult = await DeserializeEventsResultAsync(eventsResultJsonBytes);
        var events = eventsResult.Events;

        loadingTracker.DeserializeState = ReportLoadingTracker.StepState.Ok;
        await Task.Yield();

        analyzer.Actors = reportInfo.Actors;
        var result = await analyzer.Analyze(events, playerId, dungeon);

        loadingTracker.PrepareDisplayState = ReportLoadingTracker.StepState.Loading;
        await Task.Yield();

        if (isFreshFromNetwork && !eventsResult.InProgress && events.Count > 0)
        {
            var entry = new ReportHistoryEntry(
                reportCode, dungeonId, playerId,
                dungeon.Name, player.Name, hero.Name,
                DateTimeOffset.UtcNow);
            await reportCache.CacheAsync(entry, eventsResultJsonBytes, eventsExpiresAt);
        }

        loadingTracker.PrepareDisplayState = ReportLoadingTracker.StepState.Ok;
        await Task.Yield();

        return new ReportAnalysisContext(
            reportInfo,
            dungeon,
            player,
            result,
            analyzer,
            dungeonStartTime,
            dungeonEndTime);
    }

    private async Task<EventsResult> DeserializeEventsResultAsync(byte[] jsonBytes)
    {
        var metadata = EventStreamReader.ReadMetadata(jsonBytes, jsonContext);

        loadingTracker.TotalDeserializeEventCount = metadata.EventRanges.Count;
        loadingTracker.DeserializedEventCount = 0;
        await Task.Yield();

        var events = new List<Event>(metadata.EventRanges.Count);
        var pacer = new ProgressPacer();
        for (var i = 0; i < metadata.EventRanges.Count; i++)
        {
            events.Add(EventStreamReader.ReadEvent(jsonBytes, metadata.EventRanges[i]));

            if (pacer.ShouldYield(i))
            {
                loadingTracker.DeserializedEventCount = i + 1;
                await Task.Yield();
            }
        }

        loadingTracker.DeserializedEventCount = metadata.EventRanges.Count;

        return new EventsResult(events, metadata.InProgress);
    }
}
