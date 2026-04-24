using System.Text.Json;

using FellowshipAnalyzer.Core.Analysis;
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

        // Start both requests concurrently. Await preload first — it is typically lighter
        // than the full events payload and lets us populate header fields sooner.
        var preloadTask = fellowshipLogs.AnalysisPreload.GetAsync(reportCode);
        var eventsRequest = new FellowshipLogsEventsRequest(reportCode, playerId, fightId);
        var eventsTask = fellowshipLogs.Events.GetAsync(eventsRequest);

        var preload = await preloadTask;
        var reportInfo = preload.ReportInfo;
        masterDataService.Load(preload.MasterData);

        var fight = reportInfo.Fights.FirstOrDefault(f => f.Id == fightId)
            ?? throw new InvalidOperationException($"Fight {fightId} not found in report.");

        navState.Set(reportCode, reportInfo);

        var eventsResult = await eventsTask;

        loadingTracker.FetchEventsState = ReportLoadingTracker.StepState.Ok;
        loadingTracker.DeserializeState = ReportLoadingTracker.StepState.Loading;
        await Task.Yield();

        var events = eventsResult.Events.ToList();

        string? pendingCacheEventsJson = !eventsResult.InProgress
            ? JsonSerializer.Serialize(eventsResult.Events, jsonOptions)
            : null;

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

        if (pendingCacheEventsJson is not null)
        {
            var player = reportInfo.Actors.FirstOrDefault(a => a.Id == playerId);
            var entry = new ReportHistoryEntry(
                reportCode, fightId, playerId,
                fight.Name, player?.Name, heroId,
                DateTimeOffset.UtcNow);
            await reportCache.CacheAsync(entry, pendingCacheEventsJson);
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
