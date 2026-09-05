using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Heroes.Aeona.Analysis;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

using AeonaSpells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;
using AeonaTalents = FellowshipAnalyzer.Core.Common.Spells.AeonaTalents;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

/// <summary>
/// Exercises Entropy's Claim over one boss pull. Chrona amounts are written at the raw log
/// scale because <c>ResourceNormalizer</c> divides them by 100 before dispatch. The availability tests
/// feed real <see cref="CastEvent"/>s and read the cooldown stream <see cref="SpellUsable"/> fabricates
/// from them, rather than hand-building <see cref="UpdateSpellUsableEvent"/>.
/// </summary>
public sealed class EntropyClaimAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyA = 100;
    private const int EnemyB = 101;
    private const int EnemyC = 102;
    private const int ShortPullEnd = 20_000;
    private const int LongPullEnd = 120_000;
    private const int RawChronaCap = 10_000;

    [Fact]
    public async Task DotWindowsOnSeveralEnemies_MeasureUptimeAsTheirUnion()
    {
        var analyzer = await Analyze(
            [
                DotApplied(EnemyA, 1_000),
                DotApplied(EnemyB, 5_000),
                DotRemoved(EnemyA, 7_000),
                DotRemoved(EnemyB, 11_000),
            ]);

        analyzer.ActiveMs.ShouldBe(10_000);
        analyzer.Uptime.ShouldBe(0.5, 0.0001);
        analyzer.TotalActiveMs.ShouldBe(12_000);
        analyzer.TargetUptimes.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ACast_RecordsItsTargetAndTicks()
    {
        var analyzer = await Analyze(
            [
                Cast(1_000),
                DotApplied(EnemyA, 1_000),
                DotTick(EnemyA, 2_500),
                DotTick(EnemyA, 4_000),
                DotTick(EnemyA, 5_500),
                DotRemoved(EnemyA, 7_000),
            ]);

        analyzer.CastCount.ShouldBe(1);
        analyzer.TickCount.ShouldBe(3);
        analyzer.TicksPerCast.ShouldBe(3d, 0.0001);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.Timestamp.ShouldBe(1_000);
        cast.DotApplied.ShouldBeTrue();
        cast.Target.ShouldBe(new UnitKey(EnemyA, 0));
        cast.DotStart.ShouldBe(1_000);
        cast.DotEnd.ShouldBe(7_000);
        cast.Ticks.ShouldBe(3);
    }

    [Fact]
    public async Task ACastThatAppliedNoDot_IsRecordedWithNoDotWindow()
    {
        var analyzer = await Analyze([Cast(1_000)]);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.DotApplied.ShouldBeFalse();
        cast.DotStart.ShouldBeNull();
        cast.DotEnd.ShouldBeNull();
        cast.ChronaGenerated.ShouldBe(0);
        cast.ChronaOvercapped.ShouldBe(0);
        analyzer.ActiveMs.ShouldBe(0);
        analyzer.Uptime.ShouldBe(0d, 0.0001);
    }

    [Fact]
    public async Task ADotWithNoCastEvent_CountsTowardUptimeWithoutOpeningACastRow()
    {
        var analyzer = await Analyze([DotApplied(EnemyA, 1_000), DotRemoved(EnemyA, 7_000)]);

        analyzer.Casts.ShouldBeEmpty();
        analyzer.CastCount.ShouldBe(0);
        analyzer.ActiveMs.ShouldBe(6_000);
    }

    [Fact]
    public async Task ADotThatNeverExpires_ClosesAtItsLastTick()
    {
        var analyzer = await Analyze(
            [
                Cast(1_000),
                DotApplied(EnemyA, 1_000),
                DotTick(EnemyA, 5_000),
            ]);

        analyzer.Casts.ShouldHaveSingleItem().DotEnd.ShouldBe(5_000);
        analyzer.ActiveMs.ShouldBe(4_000);
    }

    [Fact]
    public async Task ARefreshInsideAnOpenWindow_ExtendsItWithoutOpeningASecondCast()
    {
        var analyzer = await Analyze(
            [
                Cast(1_000),
                DotApplied(EnemyA, 1_000),
                DotRefreshed(EnemyA, 4_000),
                DotRemoved(EnemyA, 9_000),
            ]);

        analyzer.Casts.ShouldHaveSingleItem().DotEnd.ShouldBe(9_000);
        analyzer.ActiveMs.ShouldBe(8_000);
    }

    [Fact]
    public async Task EntropyClaimIsAvailableUntilItIsCastAndAgainOnceItRecharges()
    {
        var analyzer = await Analyze(
            [
                Cast(10_000),
                DotApplied(EnemyA, 10_000),
                DotRemoved(EnemyA, 16_000),
            ],
            LongPull());

        analyzer.AvailableMs.ShouldBe(100_000);
    }

    [Fact]
    public async Task EachWaitForACharge_IsMeasuredAndTheOneStillRunningAtThePullEndCountsToIt()
    {
        var analyzer = await Analyze(
            [
                Cast(10_000),
                DotApplied(EnemyA, 10_000),
                DotRemoved(EnemyA, 16_000),
            ],
            LongPull());

        analyzer.DelaysAfterReady.ShouldBe([10_000, 90_000]);
        analyzer.AverageDelayAfterReadyMs.ShouldBe(50_000d, 0.0001);
        analyzer.Casts.ShouldHaveSingleItem().DelayAfterReadyMs.ShouldBe(10_000);
    }

    [Fact]
    public async Task TheTicksOfAnApplication_RecordTheChronaTheyGenerated()
    {
        var analyzer = await Analyze(
            [
                Cast(1_000),
                DotApplied(EnemyA, 1_000),
                DotTick(EnemyA, 2_500, rawChrona: 2_000),
                DotTick(EnemyA, 4_000, rawChrona: 2_600),
                DotRemoved(EnemyA, 7_000),
            ]);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.ChronaGenerated.ShouldBe(6);
        cast.ChronaOvercapped.ShouldBe(0);
        analyzer.ChronaGenerated.ShouldBe(6);
        analyzer.ChronaOvercapped.ShouldBe(0);
    }

    [Fact]
    public async Task ATickThatRunsIntoTheMaximum_ReportsTheStatedAmountAsOvercapped()
    {
        var analyzer = await Analyze(
            [
                Cast(1_000),
                DotApplied(EnemyA, 1_000),
                DotTick(EnemyA, 2_500, rawChrona: 9_900),
                DotTick(EnemyA, 4_000, rawChrona: RawChronaCap),
                DotRemoved(EnemyA, 7_000),
            ]);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.ChronaGenerated.ShouldBe(1);
        cast.ChronaOvercapped.ShouldBe(3);
        analyzer.ChronaOvercapped.ShouldBe(3);
    }

    [Fact]
    public async Task TwoApplicationsRunningTogether_DoNotShareEachOthersChrona()
    {
        var analyzer = await Analyze(
            [
                Cast(1_000),
                DotApplied(EnemyA, 1_000),
                Cast(3_000),
                DotApplied(EnemyB, 3_000),
                DotTick(EnemyA, 4_000, rawChrona: 2_000),
                DotTick(EnemyA, 5_000, rawChrona: 2_600),
                DotTick(EnemyB, 6_000, rawChrona: 3_200),
                DotRemoved(EnemyA, 7_000),
                DotRemoved(EnemyB, 9_000),
            ]);

        analyzer.Casts.Count.ShouldBe(2);
        analyzer.Casts[0].Target.ShouldBe(new UnitKey(EnemyA, 0));
        analyzer.Casts[0].ChronaGenerated.ShouldBe(6);
        analyzer.Casts[1].Target.ShouldBe(new UnitKey(EnemyB, 0));
        analyzer.Casts[1].ChronaGenerated.ShouldBe(6);
    }

    [Fact]
    public async Task WithoutTheTalent_EveryEntropicBurstMeasureIsAbsent()
    {
        var analyzer = await Analyze(
            [
                Cast(1_000),
                DotApplied(EnemyA, 1_000),
                DotRemoved(EnemyA, 7_000),
            ]);

        analyzer.EntropicBurstTaken.ShouldBeFalse();
        analyzer.EntropicBurstStacks.ShouldBeNull();
        analyzer.EntropicBurstStacksPerCast.ShouldBeNull();
        analyzer.EntropicBurstUptime.ShouldBeNull();
        analyzer.EntropicBurstActiveMs.ShouldBeNull();
        analyzer.EntropicBurstUnitActiveMs.ShouldBeNull();
        analyzer.EntropicBurstStackMs.ShouldBeNull();
        analyzer.EntropicBurstAverageStacks.ShouldBeNull();
    }

    [Fact]
    public async Task EntropicBurstAppliedAtTheExpiry_IsCreditedToThatCast()
    {
        var analyzer = await Analyze(
            [
                Talented(),
                Cast(1_000),
                DotApplied(EnemyA, 1_000),
                DotRemoved(EnemyA, 7_000),
                BurstApplied(EnemyB, 7_000),
                BurstApplied(EnemyC, 7_000),
                BurstRemoved(EnemyB, 16_000),
                BurstRemoved(EnemyC, 16_000),
            ]);

        analyzer.EntropicBurstTaken.ShouldBeTrue();
        analyzer.EntropicBurstStacks.ShouldBe(2);
        analyzer.EntropicBurstStacksPerCast!.Value.ShouldBe(2d, 0.0001);
        analyzer.EntropicBurstActiveMs.ShouldBe(9_000);
        analyzer.EntropicBurstUnitActiveMs.ShouldBe(18_000);
        analyzer.EntropicBurstUptime!.Value.ShouldBe(0.45, 0.0001);
        analyzer.EntropicBurstStackMs.ShouldBe(18_000);
        analyzer.EntropicBurstAverageStacks!.Value.ShouldBe(1d, 0.0001);

        analyzer.Casts.ShouldHaveSingleItem().EntropicBurstStacks.ShouldBe(2);
    }

    [Fact]
    public async Task EntropicBurstStacksWeightTheActiveTime()
    {
        var analyzer = await Analyze(
            [
                Talented(),
                BurstApplied(EnemyA, 1_000),
                BurstStacked(EnemyA, 3_000, stack: 2),
                BurstRemoved(EnemyA, 5_000),
            ]);

        analyzer.EntropicBurstStackMs.ShouldBe(6_000);
        analyzer.EntropicBurstUnitActiveMs.ShouldBe(4_000);
        analyzer.EntropicBurstAverageStacks!.Value.ShouldBe(1.5, 0.0001);
        analyzer.EntropicBurstActiveMs.ShouldBe(4_000);
        analyzer.EntropicBurstUptime!.Value.ShouldBe(0.2, 0.0001);
        analyzer.EntropicBurstStacks.ShouldBe(0);
    }

    [Fact]
    public async Task EntropicBurstAppliedLongAfterAnExpiry_IsNotCreditedToThatCast()
    {
        var analyzer = await Analyze(
            [
                Talented(),
                Cast(1_000),
                DotApplied(EnemyA, 1_000),
                DotRemoved(EnemyA, 7_000),
                BurstApplied(EnemyB, 7_000 + EntropyClaimAnalyzer.EntropicBurstAttributionMs + 1),
                BurstRemoved(EnemyB, 16_000),
            ]);

        analyzer.Casts.ShouldHaveSingleItem().EntropicBurstStacks.ShouldBe(0);
        analyzer.EntropicBurstStacks.ShouldBe(0);
    }

    [Fact]
    public async Task NothingRecorded_ReadsZeroWithoutFailing()
    {
        var analyzer = await Analyze([]);

        analyzer.CastCount.ShouldBe(0);
        analyzer.Casts.ShouldBeEmpty();
        analyzer.ActiveMs.ShouldBe(0);
        analyzer.Uptime.ShouldBe(0d, 0.0001);
        analyzer.TickCount.ShouldBe(0);
        analyzer.TicksPerCast.ShouldBe(0d, 0.0001);
        analyzer.ChronaGenerated.ShouldBe(0);
        analyzer.ChronaOvercapped.ShouldBe(0);
        analyzer.AvailableMs.ShouldBe(ShortPullEnd);
        analyzer.AverageDelayAfterReadyMs.ShouldBe((double)ShortPullEnd, 0.0001);
    }

    [Fact]
    public async Task TheAnalyzerIsReachableThroughEveryGeneratedPullReadPath()
    {
        var parser = await AnalyzeParser([DotApplied(EnemyA, 1_000), DotRemoved(EnemyA, 7_000)], BossPull());

        var entry = parser.EntropyClaimAnalyzers.ShouldHaveSingleItem();
        var analyzer = entry.Analyzer.ShouldBeOfType<EntropyClaimAnalyzer>();
        entry.Pull.EntropyClaimAnalyzer.ShouldBeSameAs(analyzer);
        parser.For(entry.Pull).EntropyClaimAnalyzer.ShouldBeSameAs(analyzer);
    }

    private static CombatantInfoEvent Talented() => new()
    {
        SourceId = PlayerId,
        Talents = [new TalentInfo { Id = AeonaTalents.EntropicBurst }],
    };

    private static CastEvent Cast(int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = EnemyA,
        Ability = new Ability { FSLID = AeonaSpells.EntropyClaim.FSLID, Name = "Entropy's Claim" },
        Target = new StubCastTarget(),
        Channel = new EndChannelEvent(),
    };

    private static ApplyDebuffEvent DotApplied(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = AeonaSpells.EntropyClaimDot.FSLID, Name = "Entropy's Claim" },
    };

    private static RefreshDebuffEvent DotRefreshed(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = AeonaSpells.EntropyClaimDot.FSLID, Name = "Entropy's Claim" },
    };

    private static RemoveDebuffEvent DotRemoved(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = AeonaSpells.EntropyClaimDot.FSLID, Name = "Entropy's Claim" },
    };

    private static DamageEvent DotTick(int targetId, int timestamp, int? rawChrona = null) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Amount = 300,
        Tick = true,
        Ability = new Ability { FSLID = AeonaSpells.EntropyClaimDot.FSLID, Name = "Entropy's Claim" },
        SourceResources = rawChrona is null ? null : new ActorResources
        {
            HitPoints = 20_000,
            MaxHitPoints = 30_000,
            Resources =
            [
                new ClassResource { Type = ResourceTypes.Primary, Amount = rawChrona.Value, Max = RawChronaCap },
            ],
        },
    };

    private static ApplyDebuffEvent BurstApplied(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = AeonaSpells.EntropicBurst.FSLID, Name = "Entropic Burst" },
    };

    private static ApplyDebuffStackEvent BurstStacked(int targetId, int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Stack = stack,
        Ability = new Ability { FSLID = AeonaSpells.EntropicBurst.FSLID, Name = "Entropic Burst" },
    };

    private static RemoveDebuffEvent BurstRemoved(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { FSLID = AeonaSpells.EntropicBurst.FSLID, Name = "Entropic Burst" },
    };

    private static ReportDungeon BossPull() => new(0, "Boss", 1, true, 0, ShortPullEnd, null, null, null);

    private static ReportDungeon LongPull() => new(0, "Boss", 1, true, 0, LongPullEnd, null, null, null);

    private static async Task<EntropyClaimAnalyzer> Analyze(Event[] events, ReportDungeon? dungeon = null)
    {
        var parser = await AnalyzeParser(events, dungeon ?? BossPull());
        return parser.EntropyClaimAnalyzers
            .ShouldHaveSingleItem()
            .Analyzer
            .ShouldBeOfType<EntropyClaimAnalyzer>();
    }

    private static async Task<AeonaCombatLogParser> AnalyzeParser(Event[] events, ReportDungeon dungeon)
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
        return parser;
    }

    private sealed class StubCastTarget : ICastTarget
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public int Guid { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}
