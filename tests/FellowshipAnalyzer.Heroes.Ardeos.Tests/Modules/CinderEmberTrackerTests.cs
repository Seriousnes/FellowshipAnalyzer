using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Ardeos.Analysis;
using FellowshipAnalyzer.Heroes.Ardeos.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Ardeos.Tests.Modules;

public sealed class CinderEmberTrackerTests
{
    private const int PlayerId = 7;
    private const int RawLogMax = 40000;

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(50, 0, 50)]
    [InlineData(99, 0, 99)]
    [InlineData(100, 1, 0)]
    [InlineData(101, 1, 1)]
    [InlineData(199, 1, 99)]
    [InlineData(250, 2, 50)]
    [InlineData(400, 4, 0)]
    public void Decomposition_SplitsCinderTotalIntoEmbersAndCinders(int totalCinders, int embers, int cinders)
    {
        CinderEmberTracker.EmbersFromCinders(totalCinders).ShouldBe(embers);
        CinderEmberTracker.PartialCinders(totalCinders).ShouldBe(cinders);
    }

    [Fact]
    public async Task Tracker_DerivesEmbersAndCindersFromNormalizedSnapshot()
    {
        var tracker = await TrackAsync([PrimarySnapshot(timestamp: 1000, rawLogAmount: 25000)]);

        tracker.TotalCinders.ShouldBe(250);
        tracker.CurrentEmbers.ShouldBe(2);
        tracker.CurrentCinders.ShouldBe(50);
    }

    [Fact]
    public async Task Tracker_FollowsEmberSpendDrop()
    {
        var tracker = await TrackAsync(
        [
            PrimarySnapshot(timestamp: 1000, rawLogAmount: 40000),
            PrimarySnapshot(timestamp: 2000, rawLogAmount: 30000),
        ]);

        tracker.TotalCinders.ShouldBe(300);
        tracker.CurrentEmbers.ShouldBe(3);
        tracker.CurrentCinders.ShouldBe(0);
    }

    private static CastEvent PrimarySnapshot(int timestamp, int rawLogAmount) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        Ability = new Ability { Id = 1 },
        SourceResources = new ActorResources
        {
            Resources = [new ClassResource { Type = ResourceTypes.Primary, Amount = rawLogAmount, Max = RawLogMax }],
        },
    };

    private static async Task<CinderEmberTracker> TrackAsync(List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddArdeosAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ArdeosCombatLogParser>();
        await parser.Analyze(events, PlayerId, new ReportFight(0, "", 0, null, 0, 10000, null, null, null));
        return parser.CinderEmberTracker.ShouldNotBeNull();
    }
}
