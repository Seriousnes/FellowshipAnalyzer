using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Modules;
using FellowshipAnalyzer.Heroes.Tariq.Statistics;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

public sealed class RisingSpiritTrackerTests
{
    private const int PlayerId = 7;
    private const int FightEnd = 100_000;

    private static readonly int RisingSpiritId = TariqSpells.RisingSpirit.FSLID;

    [Fact]
    public async Task Analyze_RisingSpirit_MeasuresUptimeOverTheFight()
    {
        var tracker = await AnalyzeAsync(
        [
            Apply(10_000),
            Remove(60_000),
        ]);

        tracker.Applications.ShouldBe(1);
        tracker.TotalActiveMs.ShouldBe(50_000);
        tracker.FightDurationMs.ShouldBe(FightEnd);
        tracker.UptimeShare.ShouldBe(0.5, tolerance: 0.0001);
        tracker.CurrentStacks.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_RisingSpirit_ClosesABuffStillHeldWhenTheFightEnds()
    {
        var tracker = await AnalyzeAsync(
        [
            Apply(50_000),
        ]);

        tracker.Applications.ShouldBe(1);
        tracker.TotalActiveMs.ShouldBe(50_000);
        tracker.UptimeShare.ShouldBe(0.5, tolerance: 0.0001);
        tracker.CurrentStacks.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_RisingSpirit_CountsSeparateWindowsIntoOneUptime()
    {
        var tracker = await AnalyzeAsync(
        [
            Apply(10_000),
            Remove(20_000),
            Apply(40_000),
            Remove(70_000),
        ]);

        tracker.Applications.ShouldBe(2);
        tracker.TotalActiveMs.ShouldBe(40_000);
        tracker.UptimeShare.ShouldBe(0.4, tolerance: 0.0001);
    }

    [Fact]
    public async Task Analyze_RisingSpirit_TracksTheDeepestStackFromStackApplications()
    {
        var tracker = await AnalyzeAsync(
        [
            Apply(10_000),
            StackGain(11_000, stack: 2),
            StackGain(12_000, stack: 3),
            Remove(20_000),
        ]);

        tracker.MaxStacks.ShouldBe(3);
    }

    [Fact]
    public async Task Analyze_RisingSpirit_HoldsTheDeepestStackToTheEffectsCap()
    {
        var tracker = await AnalyzeAsync(
        [
            Apply(10_000),
            StackGain(11_000, stack: 9),
            Remove(20_000),
        ]);

        tracker.MaxStacks.ShouldBe(RisingSpiritTracker.StackCap);
    }

    [Fact]
    public async Task Analyze_RisingSpirit_WeightsAverageStacksByTheTimeSpentAtEachDepth()
    {
        var tracker = await AnalyzeAsync(
        [
            Apply(0),
            StackGain(10_000, stack: 3),
            Remove(20_000),
        ]);

        tracker.TotalActiveMs.ShouldBe(20_000);
        tracker.AverageStacks.ShouldBe(2d, tolerance: 0.0001);
    }

    [Fact]
    public async Task Analyze_RisingSpirit_OpensAWindowOnAStackEventWithNoPrecedingApplication()
    {
        var tracker = await AnalyzeAsync(
        [
            StackGain(40_000, stack: 2),
            Remove(60_000),
        ]);

        tracker.Applications.ShouldBe(0);
        tracker.MaxStacks.ShouldBe(2);
        tracker.TotalActiveMs.ShouldBe(20_000);
    }

    [Fact]
    public async Task Analyze_RisingSpirit_ContributesNoStatisticWhenTheBuffNeverLanded()
    {
        var tracker = await AnalyzeAsync([]);

        tracker.Applications.ShouldBe(0);
        tracker.TotalActiveMs.ShouldBe(0);
        tracker.UptimeShare.ShouldBe(0d);
        tracker.AverageStacks.ShouldBe(0d);
        tracker.StatisticsComponentType.ShouldBeNull();
    }

    [Fact]
    public async Task Analyze_RisingSpirit_ContributesItsStatisticOnceTheBuffLanded()
    {
        var tracker = await AnalyzeAsync(
        [
            Apply(10_000),
            Remove(20_000),
        ]);

        tracker.StatisticsComponentType.ShouldBe(typeof(RisingSpiritStatistics));
    }

    private static ApplyBuffEvent Apply(int timestamp) =>
        FuryEconomyAnalyzerTests.Buff<ApplyBuffEvent>(timestamp, RisingSpiritId);

    private static RemoveBuffEvent Remove(int timestamp) =>
        FuryEconomyAnalyzerTests.Buff<RemoveBuffEvent>(timestamp, RisingSpiritId);

    /// <summary>
    /// An <c>applybuffstack</c> carrying the new stack total, which the shared buff helper leaves unset.
    /// </summary>
    private static ApplyBuffStackEvent StackGain(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = RisingSpiritId, Name = "Rising Spirit" },
        Stack = stack,
    };

    private static async Task<RisingSpiritTracker> AnalyzeAsync(List<Event> events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddTariqAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<TariqCombatLogParser>();
        var fight = new ReportFight(0, "Boss", 1, true, 0, FightEnd, null, null, null);
        await parser.Analyze(events, playerId: PlayerId, fight: fight);
        return parser.GetModule<RisingSpiritTracker>().ShouldNotBeNull();
    }
}
