using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;
using FellowshipAnalyzer.Heroes.Aeona.Modules;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Modules;

public sealed class FreeCastTrackerTests
{
    private static readonly int[] Uchronia = [AeonaTalents.Uchronia];

    [Fact]
    public async Task TheCastAtAUchroniaRemoval_IsFree()
    {
        var parser = await Analyze(BossPull(),
            Info(Uchronia),
            ApplyBuff(1_000, Spells.Uchronia),
            ApplyBuffStack(2_000, Spells.Uchronia, 2),
            ApplyBuffStack(3_000, Spells.Uchronia, 3),
            Activation(3_500, Spells.Oblivion),
            RemoveBuff(3_500, Spells.Uchronia));

        var tracker = parser.FreeCastTracker.ShouldNotBeNull();
        var free = tracker.FreeCasts.ShouldHaveSingleItem();
        free.Timestamp.ShouldBe(3_500);
        free.AbilityId.ShouldBe((int)Spells.Oblivion.FSLID);
        free.Source.ShouldBe(FreeCastSource.Uchronia);
        tracker.IsFree(3_500, Spells.Oblivion.FSLID).ShouldBeTrue();
        tracker.FreeCastAt(3_520, Spells.Oblivion.FSLID).ShouldNotBeNull();
    }

    [Fact]
    public async Task ARemovalLoggedBeforeItsCast_StillPairsWithThatCast()
    {
        var parser = await Analyze(BossPull(),
            Info(Uchronia),
            ApplyBuff(1_000, Spells.Uchronia),
            RemoveBuff(3_500, Spells.Uchronia),
            Activation(3_500, Spells.RestoreContinuity, TankId));

        var free = parser.FreeCastTracker.ShouldNotBeNull().FreeCasts.ShouldHaveSingleItem();
        free.AbilityId.ShouldBe((int)Spells.RestoreContinuity.FSLID);
    }

    [Fact]
    public async Task ACastOutsideTheTolerance_IsNotFree()
    {
        var parser = await Analyze(BossPull(),
            Info(Uchronia),
            ApplyBuff(1_000, Spells.Uchronia),
            Activation(3_000, Spells.Oblivion),
            RemoveBuff(3_500, Spells.Uchronia),
            Activation(4_000, Spells.Oblivion));

        parser.FreeCastTracker.ShouldNotBeNull().FreeCasts.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACastInsideAnEpochBreakWindow_IsFreeFromEpochBreak()
    {
        var parser = await Analyze(BossPull(),
            Info([]),
            ApplyBuff(1_000, Spells.EpochBreakSelfBuff),
            Activation(2_000, Spells.AmendFate, TankId),
            RemoveBuff(5_000, Spells.EpochBreakSelfBuff),
            Activation(6_000, Spells.AmendFate, TankId));

        var tracker = parser.FreeCastTracker.ShouldNotBeNull();
        var free = tracker.FreeCasts.ShouldHaveSingleItem();
        free.Timestamp.ShouldBe(2_000);
        free.Source.ShouldBe(FreeCastSource.EpochBreak);
    }

    [Fact]
    public async Task AUchroniaRemovalLoggedBeforeAnEpochBreakCast_IsSpentByThatCast()
    {
        var parser = await Analyze(BossPull(),
            Info(Uchronia),
            ApplyBuff(1_000, Spells.EpochBreakSelfBuff),
            ApplyBuff(1_000, Spells.Uchronia),
            RemoveBuff(3_000, Spells.Uchronia),
            Activation(3_000, Spells.Oblivion),
            RemoveBuff(3_010, Spells.EpochBreakSelfBuff),
            Activation(3_040, Spells.AmendFate, TankId));

        var free = parser.FreeCastTracker.ShouldNotBeNull().FreeCasts.ShouldHaveSingleItem();
        free.AbilityId.ShouldBe((int)Spells.Oblivion.FSLID);
        free.Source.ShouldBe(FreeCastSource.EpochBreak);
    }

    [Fact]
    public async Task AUchroniaRemovalLoggedAfterAnEpochBreakCast_IsSpentByThatCast()
    {
        var parser = await Analyze(BossPull(),
            Info(Uchronia),
            ApplyBuff(1_000, Spells.EpochBreakSelfBuff),
            ApplyBuff(1_000, Spells.Uchronia),
            Activation(3_000, Spells.Oblivion),
            RemoveBuff(3_005, Spells.Uchronia),
            RemoveBuff(3_010, Spells.EpochBreakSelfBuff),
            Activation(3_040, Spells.AmendFate, TankId));

        var free = parser.FreeCastTracker.ShouldNotBeNull().FreeCasts.ShouldHaveSingleItem();
        free.AbilityId.ShouldBe((int)Spells.Oblivion.FSLID);
        free.Source.ShouldBe(FreeCastSource.EpochBreak);
    }

    [Fact]
    public async Task Opportunities_CountEveryUchroniaAndEpochBreakWindowOpenedInTheRange()
    {
        var parser = await Analyze(BossPull(),
            Info(Uchronia),
            ApplyBuff(1_000, Spells.Uchronia),
            RemoveBuff(2_000, Spells.Uchronia),
            ApplyBuff(4_000, Spells.EpochBreakSelfBuff),
            RemoveBuff(8_000, Spells.EpochBreakSelfBuff),
            ApplyBuff(9_000, Spells.Uchronia),
            RemoveBuff(9_500, Spells.Uchronia));

        var tracker = parser.FreeCastTracker.ShouldNotBeNull();
        tracker.OpportunitiesBetween(0, 10_000).ShouldBe(3);
        tracker.OpportunitiesBetween(3_000, 10_000).ShouldBe(2);
    }
}
