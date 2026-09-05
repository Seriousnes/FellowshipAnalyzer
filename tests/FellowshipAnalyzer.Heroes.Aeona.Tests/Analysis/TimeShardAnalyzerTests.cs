using FellowshipAnalyzer.Core;
using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common;
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
using CoreItems = FellowshipAnalyzer.Core.Common.Items.Items;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Analysis;

/// <summary>
/// Exercises the Time Shard cast reconstruction. Chrona snapshots are written at the raw log scale
/// because <c>ResourceNormalizer</c> divides them by 100 before dispatch, which puts the maximum at 100
/// and the Synchronicity threshold at 50.
/// </summary>
public sealed class TimeShardAnalyzerTests
{
    private const int PlayerId = 7;
    private const int EnemyId = 40;
    private const int OtherEnemyId = 41;
    private const int DungeonEndTime = 60_000;
    private const int RawChronaCap = 10_000;

    private static readonly ReportDungeon Dungeon =
        new(Id: 0, Name: "Boss", EncounterId: 1, Kill: true,
            StartTime: 0, EndTime: DungeonEndTime, Difficulty: null,
            FriendlyPlayers: null, CompletionPercentage: null);

    [Fact]
    public async Task NoEvents_StillOpensThePullSurface()
    {
        var analyzer = await Analyze();

        analyzer.CastCount.ShouldBe(0);
        analyzer.Casts.ShouldBeEmpty();
        analyzer.ContinuumShiftWindows.ShouldBeEmpty();
        analyzer.EmpoweredCasts.ShouldBe(0);
        analyzer.CastsWithoutUnfoldingDoomShare.ShouldBe(0d, 0.0001);
        analyzer.ContinuumShiftLostShare.ShouldBe(0d, 0.0001);
    }

    [Fact]
    public async Task CastIntoADebuffedTarget_IsNotCountedAgainstTheTarget()
    {
        var analyzer = await Analyze(
            DoomApply(EnemyId, 1_000),
            TimeShardCastEvent(5_000, EnemyId),
            TimeShardDamage(5_100, EnemyId, 40_000),
            DoomRemove(EnemyId, 21_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.TargetDebuffed.ShouldBeTrue();
        cast.Target.ShouldBe(new UnitKey(EnemyId, 0));
        analyzer.CastsWithoutUnfoldingDoom.ShouldBe(0);
        analyzer.CastsWithoutUnfoldingDoomShare.ShouldBe(0d, 0.0001);
    }

    [Fact]
    public async Task CastWithNoUnfoldingDoomOnTheTarget_IsCounted()
    {
        var analyzer = await Analyze(
            DoomApply(EnemyId, 1_000),
            TimeShardCastEvent(5_000, EnemyId),
            TimeShardDamage(5_100, EnemyId, 40_000),
            DoomRemove(EnemyId, 6_000),
            TimeShardCastEvent(9_000, EnemyId),
            TimeShardDamage(9_100, EnemyId, 40_000));

        analyzer.CastsWithoutUnfoldingDoom.ShouldBe(1);
        analyzer.CastsWithoutUnfoldingDoomShare.ShouldBe(0.5, 0.0001);
    }

    [Fact]
    public async Task UnfoldingDoomOnAnotherEnemy_DoesNotCoverThisTarget()
    {
        var analyzer = await Analyze(
            DoomApply(OtherEnemyId, 1_000),
            TimeShardCastEvent(5_000, EnemyId),
            TimeShardDamage(5_100, EnemyId, 40_000));

        analyzer.Casts.ShouldHaveSingleItem().TargetDebuffed.ShouldBeFalse();
    }

    [Fact]
    public async Task UnfoldingDoomRemovedBeforeTheCast_ReadsAsNotActive()
    {
        var analyzer = await Analyze(
            DoomApply(EnemyId, 1_000),
            DoomRemove(EnemyId, 4_000),
            TimeShardCastEvent(5_000, EnemyId),
            TimeShardDamage(5_100, EnemyId, 40_000));

        analyzer.Casts.ShouldHaveSingleItem().TargetDebuffed.ShouldBeFalse();
    }

    [Fact]
    public async Task DamageInsideTheCastWindow_IsAttributedToTheCast()
    {
        var analyzer = await Analyze(
            TimeShardCastEvent(5_000, EnemyId),
            TimeShardDamage(5_100, EnemyId, 40_000, critical: true),
            VehementDisdainDamage(5_200, EnemyId, 9_000),
            TimeShardDamage(50_000, EnemyId, 111_111));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.Damage.ShouldBe(40_000);
        cast.Hits.ShouldBe(1);
        cast.CriticalHits.ShouldBe(1);
        cast.VehementDisdainDamage.ShouldBe(9_000);
        cast.VehementErupted.ShouldBeTrue();
        cast.TotalDamage.ShouldBe(49_000);
        analyzer.TotalDamage.ShouldBe(40_000);
        analyzer.TotalVehementDisdainDamage.ShouldBe(9_000);
        analyzer.AverageDamagePerCast.ShouldBe(49_000);
    }

    [Fact]
    public async Task DamageIsFiledAgainstTheMostRecentCast()
    {
        var analyzer = await Analyze(
            TimeShardCastEvent(5_000, EnemyId),
            TimeShardDamage(5_100, EnemyId, 10_000),
            TimeShardCastEvent(9_000, EnemyId),
            TimeShardDamage(9_100, EnemyId, 30_000));

        analyzer.Casts.Count.ShouldBe(2);
        analyzer.Casts[0].Damage.ShouldBe(10_000);
        analyzer.Casts[1].Damage.ShouldBe(30_000);
    }

    [Fact]
    public async Task TargetlessCast_TakesItsTargetFromTheDamage()
    {
        var cast = TimeShardCastEvent(5_000, EnemyId);
        cast.TargetId = -1;

        var analyzer = await Analyze(
            DoomApply(EnemyId, 1_000),
            cast,
            TimeShardDamage(5_100, EnemyId, 40_000));

        var entry = analyzer.Casts.ShouldHaveSingleItem();
        entry.Target.ShouldBe(new UnitKey(EnemyId, 0));
        entry.TargetDebuffed.ShouldBeTrue();
    }

    [Fact]
    public async Task ContinuumShiftRemovedBesideATimeShard_EmpowersThatCast()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift]),
            ContinuumShiftApply(3_000),
            TimeShardCastEvent(5_000, EnemyId),
            ContinuumShiftRemove(5_000),
            TimeShardDamage(5_100, EnemyId, 400_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.Empowered.ShouldBeTrue();
        cast.ContinuumShiftStart.ShouldBe(3_000);
        analyzer.EmpoweredCasts.ShouldBe(1);

        var window = analyzer.ContinuumShiftWindows.ShouldHaveSingleItem();
        window.Outcome.ShouldBe(ContinuumShiftOutcome.TimeShard);
        window.ConsumedByTimestamp.ShouldBe(5_000);
        analyzer.ContinuumShiftSpentOnTimeShard.ShouldBe(1);
        analyzer.ContinuumShiftLost.ShouldBe(0);
        analyzer.ContinuumShiftLostShare.ShouldBe(0d, 0.0001);
    }

    [Fact]
    public async Task ContinuumShiftRemovedWithNoTimeShardBeside_IsLost()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift]),
            ContinuumShiftApply(3_000),
            ContinuumShiftRemove(20_000),
            TimeShardCastEvent(40_000, EnemyId));

        var window = analyzer.ContinuumShiftWindows.ShouldHaveSingleItem();
        window.Outcome.ShouldBe(ContinuumShiftOutcome.Lost);
        window.ConsumedByTimestamp.ShouldBeNull();
        analyzer.ContinuumShiftLost.ShouldBe(1);
        analyzer.ContinuumShiftLostShare.ShouldBe(1d, 0.0001);
        analyzer.Casts.ShouldHaveSingleItem().Empowered.ShouldBeFalse();
    }

    [Fact]
    public async Task ContinuumShiftStillOpenAtThePullEnd_IsInNeitherLostNorTheTotal()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift]),
            ContinuumShiftApply(3_000));

        var window = analyzer.ContinuumShiftWindows.ShouldHaveSingleItem();
        window.Outcome.ShouldBe(ContinuumShiftOutcome.OpenAtPullEnd);
        window.End.ShouldBe(DungeonEndTime);
        analyzer.ContinuumShiftProcs.ShouldBe(1);
        analyzer.ContinuumShiftClosedWindows.ShouldBe(0);
        analyzer.ContinuumShiftLost.ShouldBe(0);
        analyzer.ContinuumShiftLostShare.ShouldBe(0d, 0.0001);
    }

    [Fact]
    public async Task EachWindowClaimsItsOwnTimeShard()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift]),
            ContinuumShiftApply(3_000),
            TimeShardCastEvent(5_000, EnemyId),
            ContinuumShiftRemove(5_100),
            ContinuumShiftApply(20_000),
            TimeShardCastEvent(22_000, EnemyId),
            ContinuumShiftRemove(22_100));

        analyzer.ContinuumShiftWindows.Count.ShouldBe(2);
        analyzer.ContinuumShiftWindows[0].ConsumedByTimestamp.ShouldBe(5_000);
        analyzer.ContinuumShiftWindows[1].ConsumedByTimestamp.ShouldBe(22_000);
        analyzer.EmpoweredCasts.ShouldBe(2);
    }

    [Fact]
    public async Task ASecondWindowClosingOnAnAlreadyClaimedCast_IsLost()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift]),
            ContinuumShiftApply(3_000),
            TimeShardCastEvent(5_000, EnemyId),
            ContinuumShiftRemove(5_100),
            ContinuumShiftApply(5_200),
            ContinuumShiftRemove(5_300));

        analyzer.ContinuumShiftWindows.Count.ShouldBe(2);
        analyzer.ContinuumShiftWindows[0].Outcome.ShouldBe(ContinuumShiftOutcome.TimeShard);
        analyzer.ContinuumShiftWindows[1].Outcome.ShouldBe(ContinuumShiftOutcome.Lost);
        analyzer.ContinuumShiftLost.ShouldBe(1);
        analyzer.ContinuumShiftLostShare.ShouldBe(0.5, 0.0001);
    }

    [Fact]
    public async Task TheChronaThresholdIsHalfTheMaximumAndTheCastMustBeStrictlyAboveIt()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift, AeonaTalents.Synchronicity]),
            ContinuumShiftApply(3_500),
            TimeShardCastEvent(5_000, EnemyId, rawChrona: 5_000),
            ContinuumShiftRemove(5_000),
            TimeShardDamage(5_100, EnemyId, 400_000));

        analyzer.ChronaThreshold.ShouldBe(50);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.ChronaAtCast.ShouldBe(50);
        cast.AboveChronaThreshold.ShouldBe(false);
        analyzer.SynchronicityPairings.ShouldBe(0);
        analyzer.ChronaMeasuredEmpoweredCasts.ShouldBe(1);
    }

    [Fact]
    public async Task ACastAboveTheChronaThreshold_PassesTheSynchronicityPairing()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift, AeonaTalents.Synchronicity]),
            ContinuumShiftApply(3_500),
            TimeShardCastEvent(5_000, EnemyId, rawChrona: 7_000),
            ContinuumShiftRemove(5_000),
            TimeShardDamage(5_100, EnemyId, 400_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.ChronaAtCast.ShouldBe(70);
        cast.AboveChronaThreshold.ShouldBe(true);
        analyzer.SynchronicityPairings.ShouldBe(1);
    }

    [Fact]
    public async Task ACastNoSnapshotPrecedes_IsInNeitherTheSynchronicityPassNorItsTotal()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift, AeonaTalents.Synchronicity]),
            ContinuumShiftApply(3_500),
            TimeShardCastEvent(5_000, EnemyId),
            ContinuumShiftRemove(5_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.ChronaAtCast.ShouldBeNull();
        cast.AboveChronaThreshold.ShouldBeNull();
        analyzer.SynchronicityPairings.ShouldBe(0);
        analyzer.ChronaMeasuredEmpoweredCasts.ShouldBe(0);
    }

    [Fact]
    public async Task ChronaTheCastGeneratedAboveTheMaximum_IsRecordedOnTheCast()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift]),
            ContinuumShiftApply(3_500),
            TimeShardCastEvent(5_000, EnemyId, rawChrona: 9_900),
            ContinuumShiftRemove(5_000),
            TimeShardDamage(5_100, EnemyId, 400_000, rawChrona: RawChronaCap));

        analyzer.Casts.ShouldHaveSingleItem().ChronaOvercap.ShouldBe(5);
    }

    [Fact]
    public async Task TheVehementEruptingInsideTheCastWindow_PassesItsPairing()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift], vehementBlessing: true),
            VehementApply(2_100),
            VehementStack(2_200, 2),
            ContinuumShiftApply(3_500),
            TimeShardCastEvent(5_000, EnemyId),
            ContinuumShiftRemove(5_000),
            VehementRemove(5_100),
            TimeShardDamage(5_100, EnemyId, 400_000),
            VehementDisdainDamage(5_200, EnemyId, 20_000));

        analyzer.VehementEquipped.ShouldBeTrue();

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.VehementStacksAtCast.ShouldBe(2);
        cast.VehementStacksConsumed.ShouldBe(2);
        cast.VehementErupted.ShouldBeTrue();
        analyzer.VehementPairings.ShouldBe(1);
    }

    [Fact]
    public async Task AnEmpoweredCastTheVehementDidNotEruptOn_FailsItsPairing()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift], vehementBlessing: true),
            VehementApply(2_100),
            ContinuumShiftApply(3_500),
            TimeShardCastEvent(5_000, EnemyId),
            ContinuumShiftRemove(5_000),
            TimeShardDamage(5_100, EnemyId, 400_000));

        analyzer.Casts.ShouldHaveSingleItem().VehementErupted.ShouldBeFalse();
        analyzer.VehementPairings.ShouldBe(0);
    }

    [Fact]
    public async Task WithoutTheVehementBlessing_TheBuildGateReadsFalse()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift]),
            ContinuumShiftApply(3_500),
            TimeShardCastEvent(5_000, EnemyId),
            ContinuumShiftRemove(5_000));

        analyzer.VehementEquipped.ShouldBeFalse();
        analyzer.MartialInitiativeTaken.ShouldBeFalse();
    }

    [Fact]
    public async Task MartialInitiativeIsReadWhenTheDamageLands()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift], martialInitiativeTrait: true),
            ContinuumShiftApply(3_500),
            TimeShardCastEvent(5_000, EnemyId),
            ContinuumShiftRemove(5_000),
            MartialInitiativeApply(5_050),
            TimeShardDamage(5_100, EnemyId, 400_000));

        analyzer.MartialInitiativeTaken.ShouldBeTrue();
        analyzer.Casts.ShouldHaveSingleItem().MartialInitiativeActive.ShouldBeTrue();
        analyzer.MartialInitiativePairings.ShouldBe(1);
    }

    [Fact]
    public async Task MartialInitiativeEndingBeforeTheDamageLands_FailsItsPairing()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift], martialInitiativeTrait: true),
            MartialInitiativeApply(2_000),
            MartialInitiativeRemove(5_050),
            ContinuumShiftApply(3_500),
            TimeShardCastEvent(5_000, EnemyId),
            ContinuumShiftRemove(5_000),
            TimeShardDamage(5_100, EnemyId, 400_000));

        analyzer.Casts.ShouldHaveSingleItem().MartialInitiativeActive.ShouldBeFalse();
        analyzer.MartialInitiativePairings.ShouldBe(0);
    }

    [Fact]
    public async Task ASkyboltInsideTheContinuumShiftWindow_PassesItsPairing()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift]),
            ContinuumShiftApply(3_500),
            SkyboltCast(4_000),
            TimeShardCastEvent(5_000, EnemyId),
            ContinuumShiftRemove(5_000),
            TimeShardDamage(5_100, EnemyId, 400_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.SkyboltBeforeCast.ShouldBeTrue();
        cast.SkyboltLeadMs.ShouldBe(1_000);
        analyzer.SkyboltPairings.ShouldBe(1);
    }

    [Fact]
    public async Task ASkyboltCastBeforeTheContinuumShiftWindowOpened_FailsItsPairing()
    {
        var analyzer = await Analyze(
            Combatant(talents: [AeonaTalents.ContinuumShift]),
            SkyboltCast(1_000),
            ContinuumShiftApply(3_500),
            TimeShardCastEvent(5_000, EnemyId),
            ContinuumShiftRemove(5_000),
            TimeShardDamage(5_100, EnemyId, 400_000));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.SkyboltBeforeCast.ShouldBeFalse();
        cast.SkyboltLeadMs.ShouldBe(4_000);
        analyzer.SkyboltPairings.ShouldBe(0);
    }

    [Fact]
    public async Task WithoutTheContinuumShiftTalent_EveryCastReadsUnempowered()
    {
        var analyzer = await Analyze(
            DoomApply(EnemyId, 1_000),
            TimeShardCastEvent(5_000, EnemyId),
            TimeShardDamage(5_100, EnemyId, 40_000));

        analyzer.ContinuumShiftTalented.ShouldBeFalse();
        analyzer.ContinuumShiftProcs.ShouldBe(0);
        analyzer.EmpoweredCasts.ShouldBe(0);

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.Empowered.ShouldBeFalse();
        cast.ContinuumShiftStart.ShouldBeNull();
        cast.SkyboltBeforeCast.ShouldBeFalse();
    }

    [Fact]
    public async Task SkyboltLead_IsMeasuredFromTheMostRecentCastBefore()
    {
        var analyzer = await Analyze(
            SkyboltCast(1_000),
            SkyboltCast(4_000),
            TimeShardCastEvent(5_000, EnemyId),
            SkyboltCast(6_000));

        analyzer.Casts.ShouldHaveSingleItem().SkyboltLeadMs.ShouldBe(1_000);
        analyzer.SkyboltCasts.Count.ShouldBe(3);
    }

    [Fact]
    public async Task NoSkyboltBeforeTheCast_LeavesTheLeadUnmeasured()
    {
        var analyzer = await Analyze(TimeShardCastEvent(5_000, EnemyId));

        analyzer.Casts.ShouldHaveSingleItem().SkyboltLeadMs.ShouldBeNull();
    }

    [Fact]
    public async Task VehementStacksRemovedOutsideTheCastWindow_AreNotAttributed()
    {
        var analyzer = await Analyze(
            Combatant(vehementBlessing: true),
            VehementApply(2_000),
            VehementStack(2_100, 3),
            TimeShardCastEvent(5_000, EnemyId),
            VehementRemove(5_000 + TimeShardAnalyzer.DamageWindowMs + 1));

        var cast = analyzer.Casts.ShouldHaveSingleItem();
        cast.VehementStacksAtCast.ShouldBe(3);
        cast.VehementStacksConsumed.ShouldBe(0);
        cast.VehementErupted.ShouldBeFalse();
    }

    private static async Task<TimeShardAnalyzer> Analyze(params Event[] events)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCoreAnalysisServices();
        services.AddCoreAnalysis();
        services.AddAeonaAnalysis();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var parser = scope.ServiceProvider.GetRequiredService<AeonaCombatLogParser>();
        await parser.Analyze([.. events], PlayerId, Dungeon);

        var entry = parser.TimeShardAnalyzers.ShouldHaveSingleItem();
        entry.Pull.TimeShardAnalyzer.ShouldBeSameAs(entry.Analyzer);
        return entry.Analyzer;
    }

    private static CombatantInfoEvent Combatant(
        int[]? talents = null,
        bool vehementBlessing = false,
        bool martialInitiativeTrait = false) => new()
    {
        SourceId = PlayerId,
        Talents = [.. (talents ?? []).Select(id => new TalentInfo { Id = id })],
        Gear =
        [
            new Item
            {
                Id = 1,
                Name = "Test Gear",
                Blessings = vehementBlessing
                    ? [new ItemBlessing { Id = 1, Level = 1, Name = TimeShardAnalyzer.VehementBlessing }]
                    : [],
                Traits = martialInitiativeTrait
                    ? [new ItemTrait { Id = CoreItems.MartialInitiativeTrait.FSLID, Rank = 1, Name = "Martial Initiative" }]
                    : [],
            },
        ],
    };

    private static CastEvent TimeShardCastEvent(int timestamp, int targetId, int? rawChrona = null)
    {
        var cast = Cast(timestamp, AeonaSpells.TimeShard.FSLID, targetId);
        if (rawChrona is { } chrona) cast.SourceResources = Snapshot(chrona);

        return cast;
    }

    private static CastEvent SkyboltCast(int timestamp) =>
        Cast(timestamp, CoreItems.TwilightSkybolt.FSLID, EnemyId);

    private static CastEvent Cast(int timestamp, int abilityId, int targetId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = abilityId },
    };

    private static DamageEvent TimeShardDamage(
        int timestamp, int targetId, long amount, bool critical = false, int? rawChrona = null)
    {
        var damage = Damage(timestamp, AeonaSpells.TimeShard.FSLID, targetId, amount, critical);
        if (rawChrona is { } chrona) damage.SourceResources = Snapshot(chrona);

        return damage;
    }

    private static DamageEvent VehementDisdainDamage(int timestamp, int targetId, long amount) =>
        Damage(timestamp, CoreItems.TheVehementsDisdain.FSLID, targetId, amount, critical: false);

    private static DamageEvent Damage(int timestamp, int abilityId, int targetId, long amount, bool critical) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Amount = amount,
        HitType = critical ? HitType.Crit : HitType.Normal,
        Ability = new Ability { Id = abilityId },
    };

    private static ActorResources Snapshot(int rawChrona) => new()
    {
        HitPoints = 20_000,
        MaxHitPoints = 30_000,
        Resources = [new ClassResource { Type = ResourceTypes.Primary, Amount = rawChrona, Max = RawChronaCap }],
    };

    private static ApplyDebuffEvent DoomApply(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = AeonaSpells.UnfoldingDoomDebuff.FSLID },
    };

    private static RemoveDebuffEvent DoomRemove(int targetId, int timestamp) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = targetId,
        Ability = new Ability { Id = AeonaSpells.UnfoldingDoomDebuff.FSLID },
    };

    private static ApplyBuffEvent ContinuumShiftApply(int timestamp) =>
        BuffApply(timestamp, AeonaSpells.ContinuumShift.FSLID);

    private static RemoveBuffEvent ContinuumShiftRemove(int timestamp) =>
        BuffRemove(timestamp, AeonaSpells.ContinuumShift.FSLID);

    private static ApplyBuffEvent MartialInitiativeApply(int timestamp) =>
        BuffApply(timestamp, CoreItems.MartialInitiative.FSLID);

    private static RemoveBuffEvent MartialInitiativeRemove(int timestamp) =>
        BuffRemove(timestamp, CoreItems.MartialInitiative.FSLID);

    private static ApplyBuffEvent VehementApply(int timestamp) =>
        BuffApply(timestamp, CoreItems.TheVehement.FSLID);

    private static RemoveBuffEvent VehementRemove(int timestamp) =>
        BuffRemove(timestamp, CoreItems.TheVehement.FSLID);

    private static ApplyBuffStackEvent VehementStack(int timestamp, int stack) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Stack = stack,
        Ability = new Ability { Id = CoreItems.TheVehement.FSLID },
    };

    private static ApplyBuffEvent BuffApply(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };

    private static RemoveBuffEvent BuffRemove(int timestamp, int abilityId) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Ability { Id = abilityId },
    };
}
