using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
using FellowshipAnalyzer.Core.Common.Spells.Elarion;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Heroes.Elarion.Analysis;
using FellowshipAnalyzer.Heroes.Elarion.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using SpellAbility = FellowshipAnalyzer.Core.Events.Ability;

namespace FellowshipAnalyzer.Heroes.Elarion.Tests.Modules;

public sealed class CooldownPairingAnalyzerTests
{
    private const int PlayerId = 1;
    private const int EnemyId = 20;
    private const int OtherEnemyId = 21;

    private static readonly ReportDungeon Dungeon = SpanningDungeon(0, 40_000);
    private static readonly ReportDungeon LongDungeon = SpanningDungeon(0, 90_000);

    [Fact]
    public async Task EventHorizonWithGraceRightAfterIt_PairsAtThatDistance()
    {
        var analyzer = await Analyze(
            EventHorizonCast(10_000),
            GraceCast(10_100));

        analyzer.EventHorizonCasts.ShouldBe(1);
        analyzer.GraceCasts.ShouldBe(1);

        var pairing = analyzer.EventHorizonPairings.ShouldHaveSingleItem();
        pairing.Timestamp.ShouldBe(10_000);
        pairing.NearestGraceDeltaMs.ShouldBe(100);

        analyzer.GraceHeldMs.ShouldBe(0);
    }

    [Fact]
    public async Task EventHorizonWithNoGraceCastInThePull_HasNoDelta()
    {
        var analyzer = await Analyze(EventHorizonCast(10_000));

        analyzer.GraceCasts.ShouldBe(0);
        analyzer.EventHorizonPairings.ShouldHaveSingleItem().NearestGraceDeltaMs.ShouldBeNull();
    }

    [Fact]
    public async Task TwoEventHorizonCastsAroundOneGrace_BothPairWithIt()
    {
        var analyzer = await Analyze(
            EventHorizonCast(18_000),
            GraceCast(18_100),
            EventHorizonCast(19_200));

        analyzer.EventHorizonPairings.Count.ShouldBe(2);
        analyzer.EventHorizonPairings[0].NearestGraceDeltaMs.ShouldBe(100);
        analyzer.EventHorizonPairings[1].NearestGraceDeltaMs.ShouldBe(1_100);
    }

    [Fact]
    public async Task GraceCastBeforeTheUltimate_PairsOnAbsoluteDistance()
    {
        var analyzer = await Analyze(
            GraceCast(9_500),
            EventHorizonCast(10_000));

        analyzer.EventHorizonPairings.ShouldHaveSingleItem().NearestGraceDeltaMs.ShouldBe(500);
    }

    [Fact]
    public async Task AChannelWithNoEndChannel_RanToCompletion()
    {
        var analyzer = await Analyze(
            BarrageCast(1_000),
            BarrageChannelBegin(1_000));

        analyzer.BarrageChannels.ShouldBe(1);
        analyzer.BarrageChannelsClipped.ShouldBe(0);
        analyzer.BarrageChannelLostMs.ShouldBe(0);
        analyzer.BarrageChannelsClippedPercentage.ShouldBe(0d);
    }

    [Fact]
    public async Task AChannelEndedEarly_ReportsTheTimeLost()
    {
        var begin = BarrageChannelBegin(1_000);

        var analyzer = await Analyze(
            BarrageCast(1_000),
            begin,
            BarrageChannelEnd(1_600, begin));

        analyzer.BarrageChannels.ShouldBe(1);
        analyzer.BarrageChannelsClipped.ShouldBe(1);
        analyzer.BarrageChannelLostMs.ShouldBe(1_400);
        analyzer.BarrageChannelsClippedPercentage.ShouldBe(100d);
    }

    [Fact]
    public async Task AnEndChannelAtTheFullDuration_LosesNothing()
    {
        var begin = BarrageChannelBegin(1_000);

        var analyzer = await Analyze(
            BarrageCast(1_000),
            begin,
            BarrageChannelEnd(3_000, begin));

        analyzer.BarrageChannels.ShouldBe(1);
        analyzer.BarrageChannelsClipped.ShouldBe(0);
        analyzer.BarrageChannelLostMs.ShouldBe(0);
    }

    [Fact]
    public async Task ClippedTimeAccumulatesAcrossChannels()
    {
        var first = BarrageChannelBegin(1_000);
        var second = BarrageChannelBegin(10_000);

        var analyzer = await Analyze(
            BarrageCast(1_000),
            first,
            BarrageChannelEnd(1_500, first),
            BarrageCast(10_000),
            second,
            BarrageChannelEnd(12_000, second),
            BarrageCast(20_000),
            BarrageChannelBegin(20_000));

        analyzer.BarrageChannels.ShouldBe(3);
        analyzer.BarrageChannelsClipped.ShouldBe(1);
        analyzer.BarrageChannelLostMs.ShouldBe(1_500);
    }

    [Fact]
    public async Task NoChannels_ReportsNothingClipped()
    {
        var analyzer = await Analyze(MarkCast(1_000));

        analyzer.BarrageChannels.ShouldBe(0);
        analyzer.BarrageChannelsClippedPercentage.ShouldBe(0d);
    }

    [Fact]
    public async Task GraceOffCooldownThenRecast_AccumulatesTheHeldTime()
    {
        var analyzer = await Analyze(
            GraceUsable(5_000, UpdateSpellUsableType.EndCooldown),
            GraceUsable(9_000, UpdateSpellUsableType.BeginCooldown));

        analyzer.GraceHeldMs.ShouldBe(4_000);
    }

    [Fact]
    public async Task GraceOffCooldownThenActuallyCast_ClosesTheHoldAtTheCast()
    {
        var analyzer = await Analyze(
            GraceUsable(5_000, UpdateSpellUsableType.EndCooldown),
            GraceCast(9_000));

        analyzer.GraceCasts.ShouldBe(1);
        analyzer.GraceHeldMs.ShouldBe(4_000);
    }

    [Fact]
    public async Task GraceLeftAvailableAtPullEnd_RunsTheHoldToThePullEnd()
    {
        var analyzer = await Analyze(GraceUsable(5_000, UpdateSpellUsableType.EndCooldown));

        analyzer.GraceHeldMs.ShouldBe(35_000);
    }

    [Fact]
    public async Task GraceHeldTime_UsesTheTrueRechargeInstantNotTheObservingTimestamp()
    {
        var lateObservation = GraceUsable(9_000, UpdateSpellUsableType.EndCooldown);
        lateObservation.ExpectedRechargeTimestamp = 5_000;

        var analyzer = await Analyze(
            lateObservation,
            GraceUsable(9_000, UpdateSpellUsableType.BeginCooldown));

        analyzer.GraceHeldMs.ShouldBe(4_000);
    }

    [Fact]
    public async Task NeverHeldGrace_ReportsNoHeldTime()
    {
        var analyzer = await Analyze(GraceCast(1_000));

        analyzer.GraceHeldMs.ShouldBe(0);
    }

    [Fact]
    public async Task BuffUptimes_AccumulateAcrossEveryApplyRemovePair()
    {
        var analyzer = await Analyze(
            LongDungeon,
            ApplyGraceBuff(1_000),
            RemoveGraceBuff(21_000),
            ApplyGraceBuff(50_000),
            RemoveGraceBuff(70_000),
            ApplyEventHorizonBuff(1_000),
            RemoveEventHorizonBuff(21_000));

        analyzer.GraceBuffUptimeMs.ShouldBe(40_000);
        analyzer.EventHorizonBuffUptimeMs.ShouldBe(20_000);
    }

    [Fact]
    public async Task BuffStillUpAtPullEnd_ClosesAtThePullEnd()
    {
        var analyzer = await Analyze(
            ApplyGraceBuff(30_000),
            ApplyEventHorizonBuff(35_000));

        analyzer.GraceBuffUptimeMs.ShouldBe(10_000);
        analyzer.EventHorizonBuffUptimeMs.ShouldBe(5_000);
    }

    [Fact]
    public async Task NoBuffsAtAll_ReportsNoUptime()
    {
        var analyzer = await Analyze(EventHorizonCast(1_000));

        analyzer.GraceBuffUptimeMs.ShouldBe(0);
        analyzer.EventHorizonBuffUptimeMs.ShouldBe(0);
    }

    [Fact]
    public async Task PairingTimestamps_AscendAndAreNeverBackDated()
    {
        var (parser, _) = await AnalyzeAsync(
            LongDungeon,
            EventHorizonCast(10_000),
            GraceCast(10_100),
            MarkCast(11_000),
            BarrageCast(11_200),
            EventHorizonCast(60_000),
            MarkCast(61_000),
            BarrageCast(61_500));

        var analyzer = parser.CooldownPairingAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.EventHorizonPairings
            .Select(pairing => pairing.Timestamp)
            .ShouldBe([10_000, 60_000]);

        foreach (var pairing in analyzer.EventHorizonPairings)
            pairing.Timestamp.ShouldBeGreaterThanOrEqualTo(analyzer.Pull.StartTime);

        var timestamps = parser.Events.Select(e => e.Timestamp).ToList();
        timestamps.ShouldBe([.. timestamps.Order()]);
    }

    [Fact]
    public async Task Analyze_CooldownPairing_ExposesPerPullReadPaths()
    {
        var (parser, _) = await AnalyzeAsync(
            Dungeon,
            EventHorizonCast(10_000),
            GraceCast(10_100));

        var entry = parser.CooldownPairingAnalyzers.ShouldHaveSingleItem();
        var pull = entry.Pull;
        pull.Index.ShouldBe(0);

        pull.CooldownPairingAnalyzer.ShouldBeSameAs(entry.Analyzer);
        parser.For(pull).CooldownPairingAnalyzer.ShouldBeSameAs(entry.Analyzer);
    }

    private static CastEvent GraceCast(int timestamp) =>
        SelfCast(timestamp, Spells.SkystridersGrace.FSLID, Spells.SkystridersGrace.Name);

    private static CastEvent EventHorizonCast(int timestamp) =>
        SelfCast(timestamp, Spells.EventHorizon.FSLID, Spells.EventHorizon.Name);

    private static CastEvent MarkCast(int timestamp) =>
        EnemyCast(timestamp, Spells.LunarlightMark.FSLID, Spells.LunarlightMark.Name);

    private static CastEvent BarrageCast(int timestamp, int targetId = EnemyId) =>
        EnemyCast(timestamp, Spells.HeartseekerBarrage.FSLID, Spells.HeartseekerBarrage.Name, targetId);

    private static BeginChannelEvent BarrageChannelBegin(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyId,
        Ability = BarrageAbility(),
    };

    private static EndChannelEvent BarrageChannelEnd(int timestamp, BeginChannelEvent begin)
    {
        var end = new EndChannelEvent
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            Ability = BarrageAbility(),
            BeginChannel = begin,
        };
        begin.EndChannel = end;
        return end;
    }

    private static SpellAbility BarrageAbility() => new()
    {
        FSLID = Spells.HeartseekerBarrage.FSLID,
        Name = Spells.HeartseekerBarrage.Name,
    };

    private static CastEvent SelfCast(int timestamp, int abilityId, string name) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = -1,
        Activation = true,
        Ability = new SpellAbility { FSLID = abilityId, Name = name },
    };

    private static CastEvent EnemyCast(int timestamp, int abilityId, string name, int targetId = EnemyId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Activation = true,
        Ability = new SpellAbility { FSLID = abilityId, Name = name },
    };

    private static UpdateSpellUsableEvent GraceUsable(int timestamp, UpdateSpellUsableType updateType)
    {
        var available = updateType == UpdateSpellUsableType.EndCooldown;
        return new UpdateSpellUsableEvent
        {
            Timestamp = timestamp,
            SourceId = PlayerId,
            TargetId = PlayerId,
            SourceIsFriendly = true,
            TargetIsFriendly = true,
            UpdateType = updateType,
            IsOnCooldown = !available,
            IsAvailable = available,
            ChargesAvailable = available ? 1 : 0,
            MaxCharges = 1,
            OverallStartTimestamp = timestamp,
            ChargeStartTimestamp = timestamp,
            ExpectedRechargeTimestamp = timestamp,
            ExpectedRechargeDuration = 120_000,
            Ability = new SpellAbility
            {
                FSLID = Spells.SkystridersGrace.FSLID,
                Name = Spells.SkystridersGrace.Name,
            },
        };
    }

    private static ApplyBuffEvent ApplyGraceBuff(int timestamp) =>
        ApplyBuff(timestamp, Spells.SkystridersGraceBuff.FSLID, Spells.SkystridersGraceBuff.Name);

    private static RemoveBuffEvent RemoveGraceBuff(int timestamp) =>
        RemoveBuff(timestamp, Spells.SkystridersGraceBuff.FSLID, Spells.SkystridersGraceBuff.Name);

    private static ApplyBuffEvent ApplyEventHorizonBuff(int timestamp) =>
        ApplyBuff(timestamp, Spells.EventHorizonBuff.FSLID, Spells.EventHorizonBuff.Name);

    private static RemoveBuffEvent RemoveEventHorizonBuff(int timestamp) =>
        RemoveBuff(timestamp, Spells.EventHorizonBuff.FSLID, Spells.EventHorizonBuff.Name);

    private static ApplyBuffEvent ApplyBuff(int timestamp, int effectId, string name) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new SpellAbility { FSLID = effectId, Name = name },
    };

    private static RemoveBuffEvent RemoveBuff(int timestamp, int effectId, string name) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new SpellAbility { FSLID = effectId, Name = name },
    };

    private static ReportDungeon SpanningDungeon(int startTime, int endTime) =>
        new(Id: 0, Name: "Boss", EncounterId: 31, Kill: true,
            StartTime: startTime, EndTime: endTime, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    private static Task<CooldownPairingAnalyzer> Analyze(params Event[] events) => Analyze(Dungeon, events);

    private static async Task<CooldownPairingAnalyzer> Analyze(ReportDungeon dungeon, params Event[] events)
    {
        var (parser, _) = await AnalyzeAsync(dungeon, events);
        return parser.CooldownPairingAnalyzers.ShouldHaveSingleItem().Analyzer;
    }

    private static async Task<(ElarionCombatLogParser Parser, HeroAnalysisResult Result)> AnalyzeAsync(
        ReportDungeon dungeon,
        params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddElarionAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<ElarionCombatLogParser>();
        var result = await parser.Analyze([.. events], PlayerId, dungeon);
        return (parser, result);
    }
}
