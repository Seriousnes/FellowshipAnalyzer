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
    private const int DungeonEnd = 100_000;

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
        analyzer.ExecutePhaseDurationMs.ShouldBe(DungeonEnd - 20_000);
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
    public async Task Analyze_CullingStrike_CountsTheCastsThatLandedInsideThePhase()
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
    }

    [Fact]
    public async Task Analyze_CullingStrike_LeavesAPullThatNeverReachedTheThresholdWithoutAPhase()
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
        analyzer.CastsAboveThreshold.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_CullingStrike_KeepsAPhaseTooShortForACastSeparateFromNoPhaseAtAll()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(5_000, BossId, 90, 100),
            Hit(25_000, BossId, 20, 100),
        ], dungeonEnd: 30_000);

        var analyzer = Analyzer(parser);

        analyzer.ExecutePhaseStartTimestamp.ShouldBe(25_000);
        analyzer.ExecutePhaseDurationMs.ShouldBe(5_000);
        analyzer.CastsInPhase.ShouldBe(0);
    }

    [Fact]
    public async Task Analyze_CullingStrike_LeavesAboveThresholdCastsOutOfTheInPhaseCount()
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
    }

    /// <summary>
    /// The game gate makes an above-threshold cast impossible without an Executioner's Grin buff, so
    /// one recorded without the buff is a bad reading rather than a bad cast, and the analyzer has to
    /// separate the two.
    /// </summary>
    [Fact]
    public async Task Analyze_CullingStrike_AttributesAboveThresholdCastsToTheExecutionersGrinBuff()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(5_000, BossId, 90, 100),
            GrinApplied(9_000),
            Cast(10_000, BossId),
            GrinRemoved(11_000),
            Cast(12_000, BossId),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.CastsAboveThreshold.ShouldBe(2);
        analyzer.Casts[0].GrinActive.ShouldBeTrue();
        analyzer.Casts[1].GrinActive.ShouldBeFalse();
        analyzer.UnexplainedCastsAboveThreshold.ShouldBe(1);
    }

    [Fact]
    public async Task Analyze_CullingStrike_TreatsAGrinBuffStillUpAtPullEndAsActive()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(5_000, BossId, 90, 100),
            GrinApplied(9_000),
            Cast(50_000, BossId),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.Casts.ShouldHaveSingleItem().GrinActive.ShouldBeTrue();
        analyzer.UnexplainedCastsAboveThreshold.ShouldBe(0);
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

    /// <summary>
    /// Each stretch of the execute phase with a charge in hand is one opportunity: the phase opening with
    /// Culling Strike already available is one, and every cooldown that ends inside it is another. The
    /// last one runs to the end of the pull because nothing spent it.
    /// </summary>
    [Fact]
    public async Task Analyze_CullingStrike_CountsEveryChargeTheExecutePhaseOffered()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(10_000, BossId, 90, 100),
            Hit(20_000, BossId, 25, 100),
            Cast(21_000, BossId),
            Cast(27_500, BossId),
        ]);

        var analyzer = Analyzer(parser);
        var opportunities = analyzer.Opportunities;

        opportunities.Count.ShouldBe(3);

        opportunities[0].ReadyAt.ShouldBe(20_000);
        opportunities[0].CastAt.ShouldBe(21_000);
        opportunities[0].HeldMs.ShouldBe(1_000);
        opportunities[0].Prompt.ShouldBeTrue();

        opportunities[1].ReadyAt.ShouldBe(27_000);
        opportunities[1].CastAt.ShouldBe(27_500);
        opportunities[1].Prompt.ShouldBeTrue();

        opportunities[2].CastAt.ShouldBeNull();
        opportunities[2].Prompt.ShouldBeFalse();

        analyzer.PromptCasts.ShouldBe(2);
        analyzer.HeldWithFury.ShouldBe(1);
        analyzer.HeldWithoutFury.ShouldBe(0);
    }

    /// <summary>
    /// A charge held because Fury was short is a Fury economy problem, so it is separated from one held
    /// with the <see cref="CullingStrikeAnalyzer.FullStrengthFury"/> a full-strength cast converts.
    /// </summary>
    [Fact]
    public async Task Analyze_CullingStrike_SeparatesAHoldForFuryFromAHoldWithFuryBanked()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(10_000, BossId, 90, 100),
            Hit(20_000, BossId, 25, 100),
            FuryEconomyAnalyzerTests.Cast(21_000, CullingStrikeId, rawFury: 8_000, rawMaxFury: 10_000),
            FuryEconomyAnalyzerTests.Cast(26_000, TariqSpells.WildSwing.FSLID, rawFury: 500, rawMaxFury: 10_000),
        ]);

        var analyzer = Analyzer(parser);

        var held = analyzer.Opportunities.Last();
        held.ReadyAt.ShouldBe(27_000);
        held.FuryAtReady.ShouldBe(5);
        held.HadFury.ShouldBeFalse();

        analyzer.HeldWithoutFury.ShouldBe(1);
        analyzer.HeldWithFury.ShouldBe(0);
    }

    /// <summary>
    /// Culling Strike's cooldown is hasted and accelerates further with gear, so the game routinely allows
    /// a cast the modelled recharge has not finished. Report <c>a:NcqHDKzamL7n6YFv</c> lands 17 casts in an
    /// execute phase the model only offers 9 charges for. Each early cast has to open its own zero-length
    /// opportunity, or the phase reads as though it had room for far fewer casts than it did.
    /// </summary>
    [Fact]
    public async Task Analyze_CullingStrike_CountsACastTheCooldownModelDidNotExpectToBePossible()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(10_000, BossId, 90, 100),
            Hit(20_000, BossId, 25, 100),
            Cast(21_000, BossId),
            Cast(24_000, BossId),
        ]);

        var analyzer = Analyzer(parser);

        analyzer.CastsInPhase.ShouldBe(2);

        var early = analyzer.Opportunities[1];
        early.ReadyAt.ShouldBe(24_000);
        early.CastAt.ShouldBe(24_000);
        early.HeldMs.ShouldBe(0);
        early.Prompt.ShouldBeTrue();

        analyzer.Opportunities.Count(opportunity => opportunity.CastAt is not null)
            .ShouldBe(analyzer.CastsInPhase);
    }

    [Fact]
    public async Task Analyze_CullingStrike_OffersNoOpportunitiesWithoutAnExecutePhase()
    {
        var parser = await AnalyzeAsync(
        [
            Hit(10_000, BossId, 90, 100),
            Hit(20_000, BossId, 50, 100),
            Cast(21_000, BossId),
        ]);

        Analyzer(parser).Opportunities.ShouldBeEmpty();
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

    private static ApplyBuffEvent GrinApplied(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = TariqSpells.ExecutionersGrin.FSLID, Name = "Executioner's Grin" },
    };

    private static RemoveBuffEvent GrinRemoved(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { FSLID = TariqSpells.ExecutionersGrin.FSLID, Name = "Executioner's Grin" },
    };

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
        List<Event> events, int dungeonEnd = DungeonEnd, int encounterId = 1)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddTariqAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<TariqCombatLogParser>();
        var dungeon = new ReportDungeon(0, "Boss", encounterId, true, 0, dungeonEnd, null, null, null);
        await parser.Analyze(events, playerId: PlayerId, dungeon: dungeon);
        return parser;
    }
}
