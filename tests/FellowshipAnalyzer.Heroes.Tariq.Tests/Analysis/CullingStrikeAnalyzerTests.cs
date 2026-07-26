using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Tariq.Analysis;
using FellowshipAnalyzer.Heroes.Tariq.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using TariqSpells = FellowshipAnalyzer.Core.Common.Spells.Tariq.Spells;

namespace FellowshipAnalyzer.Heroes.Tariq.Tests.Analysis;

public sealed class CullingStrikeAnalyzerTests
{
    private const int PlayerId = 7;
    private const int BossId = 11;
    private const int AddId = 12;
    private const int FightEnd = 100_000;

    private static readonly int CullingStrikeId = TariqSpells.CullingStrike.FSLID;

    [Fact]
    public async Task Analyze_CullingStrike_OpensThePhaseWhenTheBossFirstDropsToTheThreshold()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(10_000, BossId, 90, 100),
            Hit(20_000, BossId, 29, 100),
            Hit(30_000, BossId, 20, 100),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.ExecutePhaseStartTimestamp.ShouldBe(20_000);
        analyzer.ExecutePhaseDurationMs.ShouldBe(FightEnd - 20_000);
    }

    [Fact]
    public async Task Analyze_CullingStrike_SkipsGarbageHealthSamplesBeforePickingThePrimaryTarget()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(1_000, AddId, 0, 0),
            Hit(2_000, AddId, 0, 0),
            Hit(3_000, AddId, 5_000, 100),
            Hit(4_000, AddId, 5_000, 100),
            Hit(5_000, AddId, 10, 100),
            Hit(10_000, BossId, 90, 100),
            Hit(20_000, BossId, 25, 100),
            Hit(30_000, BossId, 15, 100),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.ExecutePhaseStartTimestamp.ShouldBe(20_000);
    }

    [Fact]
    public async Task Analyze_CullingStrike_MeasuresCoverageAgainstTheCyclesThePhaseHadRoomFor()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(10_000, BossId, 90, 100),
            Hit(30_000, BossId, 25, 100),
            Cast(31_000, BossId),
            Cast(38_000, BossId),
            Cast(45_000, BossId),
            Cast(52_000, BossId),
            Cast(59_000, BossId),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.ExecutePhaseStartTimestamp.ShouldBe(30_000);
        analyzer.ExecutePhaseDurationMs.ShouldBe(70_000);
        analyzer.CastsInPhase.ShouldBe(5);
        analyzer.PossibleCasts.ShouldBe(70_000 / CullingStrikeAnalyzer.ExpectedCastIntervalMs);
        analyzer.Coverage.ShouldBe(0.5, tolerance: 0.0001);
    }

    [Fact]
    public async Task Analyze_CullingStrike_ClampsCoverageWhenTheHastedCooldownBeatTheModelledPace()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(5_000, BossId, 90, 100),
            Hit(10_000, BossId, 20, 100),
            Cast(11_000, BossId),
            Cast(13_000, BossId),
            Cast(15_000, BossId),
            Cast(17_000, BossId),
            Cast(19_000, BossId),
        ], fightEnd: 30_000);

        var analyzer = Analyzer(parser);

        analyzer.ExecutePhaseDurationMs.ShouldBe(20_000);
        analyzer.CastsInPhase.ShouldBe(5);
        analyzer.PossibleCasts.ShouldBe(5);
        analyzer.Coverage.ShouldBe(1d);
    }

    [Fact]
    public async Task Analyze_CullingStrike_LeavesAPullThatNeverReachedTheThresholdWithNothingToMiss()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(10_000, BossId, 90, 100),
            Hit(20_000, BossId, 50, 100),
            Cast(21_000, BossId),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.ExecutePhaseStartTimestamp.ShouldBeNull();
        analyzer.ExecutePhaseDurationMs.ShouldBe(0);
        analyzer.CastsInPhase.ShouldBe(0);
        analyzer.PossibleCasts.ShouldBe(0);
        analyzer.Coverage.ShouldBe(1d);
        analyzer.CastsAboveThreshold.ShouldBe(1);
    }

    /// <summary>
    /// A boss dropping into execute range in the pull's last seconds leaves a real phase that had room
    /// for no cast at all. The guide has to read <see cref="CullingStrikeAnalyzer.PossibleCasts"/> rather
    /// than the phase timestamp to tell this apart from a phase that was covered perfectly.
    /// </summary>
    [Fact]
    public async Task Analyze_CullingStrike_LeavesAPhaseTooShortForOneCycleWithNothingToMiss()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(5_000, BossId, 90, 100),
            Hit(25_000, BossId, 20, 100),
        ], fightEnd: 30_000);

        var analyzer = Analyzer(parser);

        analyzer.ExecutePhaseStartTimestamp.ShouldBe(25_000);
        analyzer.ExecutePhaseDurationMs.ShouldBe(5_000);
        analyzer.CastsInPhase.ShouldBe(0);
        analyzer.PossibleCasts.ShouldBe(0);
        analyzer.Coverage.ShouldBe(1d);
    }

    [Fact]
    public async Task Analyze_CullingStrike_LeavesAboveThresholdCastsOutOfPhaseDiscipline()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(5_000, BossId, 90, 100),
            Hit(20_000, BossId, 25, 100),
            Hit(30_000, BossId, 20, 100),
            Hit(40_000, BossId, 15, 100),
            Hit(6_000, AddId, 80, 100),
            Hit(25_000, AddId, 80, 100),
            Cast(25_500, BossId),
            Cast(35_000, BossId),
            Cast(26_000, AddId),
            Cast(36_000, AddId),
            Cast(46_000, AddId),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.ExecutePhaseStartTimestamp.ShouldBe(20_000);
        analyzer.TotalCasts.ShouldBe(5);
        analyzer.CastsInPhase.ShouldBe(2);
        analyzer.CastsAboveThreshold.ShouldBe(3);
        analyzer.PossibleCasts.ShouldBe(80_000 / CullingStrikeAnalyzer.ExpectedCastIntervalMs);
    }

    [Fact]
    public async Task Analyze_CullingStrike_ClassifiesEachCastAgainstItsTargetsLatestHealthReading()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(5_000, BossId, 90, 100),
            Hit(20_000, BossId, 25, 100),
            Cast(6_000, BossId),
            Cast(21_000, BossId),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.Casts.Count.ShouldBe(2);

        analyzer.Casts[0].Timestamp.ShouldBe(6_000);
        analyzer.Casts[0].TargetHealthPercent.ShouldNotBeNull().ShouldBe(0.90, tolerance: 0.0001);
        analyzer.Casts[0].InExecutePhase.ShouldBeFalse();
        analyzer.Casts[0].AboveThreshold.ShouldBeTrue();

        analyzer.Casts[1].Timestamp.ShouldBe(21_000);
        analyzer.Casts[1].TargetHealthPercent.ShouldNotBeNull().ShouldBe(0.25, tolerance: 0.0001);
        analyzer.Casts[1].InExecutePhase.ShouldBeTrue();
        analyzer.Casts[1].AboveThreshold.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_CullingStrike_ReadsHealthWhenTheCastAndItsDamageDisagreeOnTargetInstance()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(5_000, BossId, 90, 100, targetInstance: 1),
            Hit(20_000, BossId, 20, 100, targetInstance: 1),
            Cast(21_000, BossId),
        ]);

        var cast = Analyzer(parser).Casts.ShouldHaveSingleItem();

        cast.TargetHealthPercent.ShouldNotBeNull().ShouldBe(0.20, tolerance: 0.0001);
        cast.InExecutePhase.ShouldBeTrue();
        cast.AboveThreshold.ShouldBeFalse();
    }

    [Fact]
    public async Task Analyze_CullingStrike_IgnoresSyntheticCasts()
    {
        var fake = Cast(25_000, BossId);
        fake.Fake = true;

        var parser = await AnalyzeAsync(
        [
            Hit(5_000, BossId, 90, 100),
            Hit(20_000, BossId, 25, 100),
            fake,
            Cast(30_000, BossId),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.TotalCasts.ShouldBe(1);
        analyzer.Casts.ShouldHaveSingleItem().Timestamp.ShouldBe(30_000);
    }

    [Fact]
    public async Task Analyze_CullingStrike_IsNotEvaluatedOnATrashPull()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(5_000, BossId, 20, 100),
            Cast(6_000, BossId),
        ], encounterId: 0);

        parser.CullingStrikeAnalyzers.ShouldBeEmpty();
    }

    [Fact]
    public async Task Analyze_CullingStrike_ExposesPerPullReadPaths()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(5_000, BossId, 20, 100),
            Cast(6_000, BossId),
        ]);

        var entry = parser.CullingStrikeAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.CullingStrikeAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).CullingStrikeAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CullingStrikeAnalyzer Analyzer(TariqCombatLogParser parser) =>
        parser.CullingStrikeAnalyzers.ShouldHaveSingleItem().Analyzer;

    private static CastEvent Cast(int timestamp, int targetId, int? targetInstance = null)
    {
        var cast = FuryEconomyAnalyzerTests.CastWithoutResources(timestamp, CullingStrikeId);
        cast.TargetId = targetId;
        cast.TargetInstance = targetInstance;
        return cast;
    }

    /// <summary>
    /// One player damage event carrying a target health snapshot. Health is not rescaled by the
    /// resource normalizer, so the raw hit points are the ones the analyzer reads.
    /// </summary>
    private static DamageEvent Hit(int timestamp, int targetId, long hitPoints, long maxHitPoints, int? targetInstance = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        TargetInstance = targetInstance,
        Ability = new Ability { FSLID = TariqSpells.HeavyStrike.FSLID, Name = "Heavy Strike" },
        TargetResources = new ActorResources { HitPoints = hitPoints, MaxHitPoints = maxHitPoints },
    };

    private static async Task<TariqCombatLogParser> AnalyzeAsync(
        List<Event> events, int fightEnd = FightEnd, int encounterId = 1)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddTariqAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<TariqCombatLogParser>();
        var fight = new ReportFight(0, "Boss", encounterId, true, 0, fightEnd, null, null, null);
        await parser.Analyze(events, playerId: PlayerId, fight: fight);
        return parser;
    }
}
