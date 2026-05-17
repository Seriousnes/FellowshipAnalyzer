using System.Diagnostics;
using System.Text.Json;

using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;

using Microsoft.Extensions.Logging;



namespace FellowshipAnalyzer.Services;

/// <summary>
/// Returned by <see cref="ReportAnalysisService.RunAsync"/> once a fight has been
/// fully fetched and analyzed. All display-layer data is read directly from these
/// objects — no redundant string fields.
/// <para>
/// When <see cref="Analysis"/> is <c>null</c> the report belongs to a hero with no
/// implemented analysis yet (WIP). In that case the events fetch and analysis are
/// skipped entirely; the host should render the WIP placeholder.
/// </para>
/// </summary>
public sealed record ReportAnalysisContext(
    ReportInfo ReportInfo,
    ReportFight Fight,
    ReportActor Player,
    HeroAnalysisResult? Analysis,
    IHeroAnalyzer Analyzer,
    int FightStartTime,
    int FightEndTime
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
/// Orchestrates the full analysis pipeline for a single fight:
/// concurrent API fetch, master-data loading, hero resolution, analysis, and caching.
/// Extracted from Report.razor to keep the component a thin view layer.
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
    private const int DeserializeProgressBatchSize = 250;

    public async Task<ReportAnalysisContext> RunAsync(
        string reportCode,
        int fightId,
        int playerId,
        Func<ReportInfo, Task>? reportInfoLoaded = null)
    {
        loadingTracker.Reset();
        loadingTracker.FetchEventsState = ReportLoadingTracker.StepState.Loading;
        await Task.Yield();

        // Fetch the preload first so we can determine which hero is being analyzed.
        // Knowing the hero lets us short-circuit WIP heroes (no GuideComponent registered)
        // *before* paying for the events fetch / deserialize / analysis.
        var preload = await fellowshipLogs.GetAnalysisPreloadAsync(reportCode);
        var reportInfo = preload.ReportInfo;
        masterDataService.Load(preload.MasterData);

        var fight = reportInfo.Fights.FirstOrDefault(f => f.Id == fightId)
            ?? throw new InvalidOperationException($"Fight {fightId} not found in report.");
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

        var fightStartTime = (int)fight.StartTime;
        var fightEndTime = (int)fight.EndTime;

        // WIP short-circuit: hero has no implemented analysis. Skip the events API call,
        // skip deserialization and analysis entirely. The host renders a WIP placeholder.
        if (analyzer.GuideComponent is null)
        {
            loadingTracker.FetchEventsState = ReportLoadingTracker.StepState.Ok;
            loadingTracker.DeserializeState = ReportLoadingTracker.StepState.Ok;
            loadingTracker.NormalizeState = ReportLoadingTracker.StepState.Ok;
            loadingTracker.AnalyzeState = ReportLoadingTracker.StepState.Ok;
            loadingTracker.PrepareDisplayState = ReportLoadingTracker.StepState.Ok;
            return new ReportAnalysisContext(
                reportInfo,
                fight,
                player,
                Analysis: null,
                analyzer,
                fightStartTime,
                fightEndTime);
        }

        // Local cache check is fast (IndexedDB); on a miss, fetch raw UTF-8 JSON bytes only —
        // defer deserialization to its own step so we can measure network I/O vs JSON parsing
        // separately, and cache the network bytes verbatim.
        var sw = Stopwatch.StartNew();
        logger.LogInformation(
            "RunAsync events fetch starting reportCode={ReportCode} fightId={FightId} playerId={PlayerId}",
            reportCode, fightId, playerId);

        logger.LogInformation("RunAsync IndexedDB cache lookup starting t={ElapsedMs}ms", sw.ElapsedMilliseconds);
        var cachedEventsBytes = await reportCache.GetCachedEventsBytesAsync(reportCode, fightId, playerId);
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
            var networkResponse = await fellowshipLogs.GetRawEventsAsync(reportCode, playerId, fightId);
            logger.LogInformation(
                "RunAsync network fetch returned bytes={Bytes} t={ElapsedMs}ms",
                networkResponse.Bytes.Length, sw.ElapsedMilliseconds);
            eventsResultJsonBytes = networkResponse.Bytes;
            eventsExpiresAt = networkResponse.ExpiresAt;
            isFreshFromNetwork = true;
        }

        loadingTracker.FetchEventsState = ReportLoadingTracker.StepState.Ok;
        loadingTracker.DeserializeState = ReportLoadingTracker.StepState.Loading;
        await Task.Yield();

        var eventsResult = await DeserializeEventsResultAsync(eventsResultJsonBytes);
        var events = eventsResult.Events;

        loadingTracker.DeserializeState = ReportLoadingTracker.StepState.Ok;
        await Task.Yield();

        analyzer.ActorNames = reportInfo.Actors.ToDictionary(a => a.Id, a => a.Name);
        var result = await analyzer.Analyze(events, playerId, fightStartTime);

        loadingTracker.PrepareDisplayState = ReportLoadingTracker.StepState.Loading;
        await Task.Yield();

        // Cache only fresh, completed network responses — never overwrite from a cache-hit path,
        // and never cache an in-progress fight (which may still be receiving events).
        if (isFreshFromNetwork && !eventsResult.InProgress)
        {
            var entry = new ReportHistoryEntry(
                reportCode, fightId, playerId,
                fight.Name, player.Name, hero.Name,
                DateTimeOffset.UtcNow);
            await reportCache.CacheAsync(entry, eventsResultJsonBytes, eventsExpiresAt);
        }

        loadingTracker.PrepareDisplayState = ReportLoadingTracker.StepState.Ok;
        await Task.Yield();

        return new ReportAnalysisContext(
            reportInfo,
            fight,
            player,
            result,
            analyzer,
            fightStartTime,
            fightEndTime);
    }

    private async Task<EventsResult> DeserializeEventsResultAsync(byte[] jsonBytes)
    {
        var metadata = ReadEventsResultMetadata(jsonBytes);

        loadingTracker.TotalDeserializeEventCount = metadata.EventRanges.Count;
        loadingTracker.DeserializedEventCount = 0;
        await Task.Yield();

        var events = new List<Event>(metadata.EventRanges.Count);
        for (var i = 0; i < metadata.EventRanges.Count; i++)
        {
            events.Add(DeserializeEvent(jsonBytes, metadata.EventRanges[i], jsonContext));

            var completed = i + 1;
            if (completed % DeserializeProgressBatchSize == 0 || completed == metadata.EventRanges.Count)
            {
                loadingTracker.DeserializedEventCount = completed;
                await Task.Yield();
            }
        }

        return new EventsResult(events, metadata.InProgress);
    }

    private static Event DeserializeEvent(
        byte[] jsonBytes,
        EventJsonRange range,
        FellowshipAnalyzerJsonContext jsonContext)
    {
        return JsonSerializer.Deserialize(jsonBytes.AsSpan(range.Start, range.Length), jsonContext.Event)
            ?? throw new InvalidOperationException("Event data contained a null event.");
    }

    private static EventsResultMetadata ReadEventsResultMetadata(byte[] jsonBytes)
    {
        var reader = new Utf8JsonReader(jsonBytes);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new InvalidOperationException("Event data was not a JSON object.");
        }

        bool? inProgress = null;
        List<EventJsonRange>? eventRanges = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new InvalidOperationException($"Unexpected token in event data: {reader.TokenType}.");
            }

            var propertyName = reader.GetString();
            if (!reader.Read())
            {
                throw new InvalidOperationException("Event data ended while reading a property value.");
            }

            switch (propertyName)
            {
                case "inProgress":
                case "InProgress":
                    if (reader.TokenType is not JsonTokenType.True and not JsonTokenType.False)
                    {
                        throw new InvalidOperationException("Event data inProgress value was not a boolean.");
                    }
                    inProgress = reader.GetBoolean();
                    break;
                case "events":
                case "Events":
                    if (reader.TokenType != JsonTokenType.StartArray)
                    {
                        throw new InvalidOperationException("Event data events value was not an array.");
                    }
                    eventRanges = ReadEventRanges(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return new EventsResultMetadata(
            inProgress ?? throw new InvalidOperationException("Event data did not include inProgress."),
            eventRanges ?? throw new InvalidOperationException("Event data did not include events."));
    }

    private static List<EventJsonRange> ReadEventRanges(ref Utf8JsonReader reader)
    {
        var ranges = new List<EventJsonRange>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return ranges;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"Unexpected token in events array: {reader.TokenType}.");
            }

            var start = checked((int)reader.TokenStartIndex);
            reader.Skip();
            var end = checked((int)reader.BytesConsumed);
            ranges.Add(new EventJsonRange(start, end - start));
        }

        throw new InvalidOperationException("Event data ended while reading the events array.");
    }

    private readonly record struct EventJsonRange(int Start, int Length);

    private sealed record EventsResultMetadata(bool InProgress, List<EventJsonRange> EventRanges);
}
