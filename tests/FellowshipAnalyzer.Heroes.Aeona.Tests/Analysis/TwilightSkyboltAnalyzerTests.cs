using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

/// <summary>
/// Exercises Twilight Skybolt's charge reconstruction, which rides <c>SpellUsable</c> and therefore
/// depends on Twilight Skybolt being present in Aeona's spellbook.
/// </summary>
public sealed class TwilightSkyboltAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 40;
    private const int DungeonEndTime = 60_000;

    private static readonly ReportDungeon Dungeon =
        new(Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
            StartTime: 0, EndTime: DungeonEndTime, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    private static int RechargeMs => (int)(CoreItems.TwilightSkybolt.Cooldown!.Value * 1000);

    [Fact]
    public async Task NoCast_LeavesThePullAtEveryChargeThroughout()
    {
        var analyzer = await Analyze();

        analyzer.CastCount.ShouldBe(0);
        analyzer.MaxCharges.ShouldBe(CoreItems.TwilightSkybolt.Charges);
        analyzer.TimeAtMaxChargesMs.ShouldBe(DungeonEndTime);
        analyzer.TimeAtMaxChargesShare.ShouldBe(1d, 0.0001);
    }

    [Fact]
    public async Task OneCast_StopsTheClockUntilTheChargeIsBack()
    {
        var castTime = 10_000;
        var analyzer = await Analyze(SkyboltCast(castTime));

        analyzer.CastCount.ShouldBe(1);
        analyzer.RechargeDurationMs.ShouldBe(RechargeMs);
        analyzer.TimeAtMaxChargesMs.ShouldBe(DungeonEndTime - RechargeMs);
    }

    [Fact]
    public async Task ChargeSamples_OpenOnThePullStartAndFollowEachTransition()
    {
        var analyzer = await Analyze(SkyboltCast(10_000));

        var samples = analyzer.ChargeSamples;
        samples[0].Timestamp.ShouldBe(0);
        samples[0].ChargesAvailable.ShouldBe(CoreItems.TwilightSkybolt.Charges);
        samples.ShouldContain(sample => sample.Timestamp == 10_000 && sample.ChargesAvailable == 1);
        samples.ShouldContain(sample => sample.Timestamp == 10_000 + RechargeMs
            && sample.ChargesAvailable == CoreItems.TwilightSkybolt.Charges);
    }

    [Fact]
    public async Task BothChargesSpent_LeavesNoTimeAtTheMaximumUntilBothReturn()
    {
        var firstCast = 1_000;
        var analyzer = await Analyze(SkyboltCast(firstCast), SkyboltCast(2_000));

        var bothBack = firstCast + (2 * RechargeMs);

        analyzer.CastCount.ShouldBe(2);
        analyzer.TimeAtMaxChargesMs.ShouldBe(firstCast + (DungeonEndTime - bothBack));
    }

    [Fact]
    public async Task ChargesLost_CountsTheRechargesTheIdleTimeHadRoomFor()
    {
        var analyzer = await Analyze();

        analyzer.ChargesLost.ShouldBe(DungeonEndTime / RechargeMs);
    }

    [Fact]
    public async Task ALaterPull_OpensOnTheChargeStateTheEarlierPullLeftBehind()
    {
        var firstPullEnd = 20_000;
        var secondPullStart = 25_000;
        var castTime = 19_000;

        var dungeon = Dungeon with
        {
            DungeonPulls =
            [
                new DungeonPull(Id: 1, EncounterId: 1, Kill: true, StartTime: 0, EndTime: firstPullEnd, Name: "First", EnemyNpcs: null),
                new DungeonPull(Id: 2, EncounterId: 2, Kill: true, StartTime: secondPullStart, EndTime: DungeonEndTime, Name: "Second", EnemyNpcs: null),
            ],
        };

        var analyzers = await AnalyzeAll(dungeon, SkyboltCast(castTime));

        analyzers.Count.ShouldBe(2);
        analyzers[0].TimeAtMaxChargesMs.ShouldBe(castTime);

        var chargeBack = castTime + RechargeMs;
        analyzers[1].ChargeSamples[0].ChargesAvailable.ShouldBe(CoreItems.TwilightSkybolt.Charges - 1);
        analyzers[1].TimeAtMaxChargesMs.ShouldBe(DungeonEndTime - chargeBack);
    }

    private static async Task<TwilightSkyboltAnalyzer> Analyze(params Event[] events)
    {
        var analyzers = await AnalyzeAll(Dungeon, events);
        return analyzers.ShouldHaveSingleItem();
    }

    private static async Task<List<TwilightSkyboltAnalyzer>> AnalyzeAll(ReportDungeon dungeon, params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, dungeon);

        var entries = parser.TwilightSkyboltAnalyzers;
        entries.ShouldNotBeEmpty();
        foreach (var entry in entries)
            entry.Pull.TwilightSkyboltAnalyzer.ShouldBeSameAs(entry.Analyzer);

        return [.. entries.Select(entry => entry.Analyzer)];
    }

    private static CastEvent SkyboltCast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = new Ability { Id = CoreItems.TwilightSkybolt.FSLID },
    };
}
