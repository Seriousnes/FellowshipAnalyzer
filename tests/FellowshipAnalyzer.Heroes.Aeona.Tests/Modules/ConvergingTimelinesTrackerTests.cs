using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Modules;

public sealed class ConvergingTimelinesTrackerTests
{
    [Fact]
    public async Task AnOblivionCast_CountsOneOblivionApplication_AndRevokesTheModifierOnRemoval()
    {
        var parser = await Analyze(BossPull(),
            Info([]),
            Activation(1_000, Spells.Oblivion),
            ApplyBuff(1_000, Spells.ConvergingTimelines),
            RemoveBuff(1_500, Spells.ConvergingTimelines),
            Activation(5_000, Spells.TimeShard));

        var tracker = parser.ConvergingTimelinesTracker.ShouldNotBeNull();
        tracker.Applications.ShouldBe(1);
        tracker.OblivionApplications.ShouldBe(1);
        tracker.Windows.ShouldHaveSingleItem().ShouldBe(new AuraWindow(1_000, 1_500));
        parser.GetModule<StatTracker>()!.CurrentCooldownAcceleration(null).ShouldBe(0.0, 0.0001);
    }

    [Fact]
    public async Task WithLonesomeSong_AnOblivionAndACleanse_CountOneApplicationEach()
    {
        var parser = await Analyze(BossPull(),
            Info([], AeonaLegendaries.LonesomeSong),
            Activation(1_000, Spells.Oblivion),
            ApplyBuff(1_000, Spells.ConvergingTimelines),
            RemoveBuff(1_500, Spells.ConvergingTimelines),
            Activation(3_000, Spells.AmendFate, TankId),
            ApplyBuff(3_000, Spells.ConvergingTimelines),
            RemoveBuff(3_500, Spells.ConvergingTimelines));

        var tracker = parser.ConvergingTimelinesTracker.ShouldNotBeNull();
        tracker.OblivionApplications.ShouldBe(1);
        tracker.CleanseApplications.ShouldBe(1);
        tracker.Windows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ARefreshInsideTheWindow_KeepsOneModifierAndExtendsTheWindow()
    {
        var parser = await Analyze(BossPull(),
            Info([]),
            Activation(1_000, Spells.Oblivion),
            ApplyBuff(1_000, Spells.ConvergingTimelines),
            Activation(1_200, Spells.Oblivion),
            RefreshBuff(1_200, Spells.ConvergingTimelines),
            RemoveBuff(1_700, Spells.ConvergingTimelines));

        var tracker = parser.ConvergingTimelinesTracker.ShouldNotBeNull();
        tracker.Applications.ShouldBe(2);
        tracker.Windows.ShouldHaveSingleItem().ShouldBe(new AuraWindow(1_000, 1_700));
        parser.GetModule<StatTracker>()!.CurrentCooldownAcceleration(null).ShouldBe(0.0, 0.0001);
    }

    [Fact]
    public async Task ALonesomeSongApplication_AddsAnAccelerationTermOfTwo_WhileTheBuffIsActive()
    {
        var parser = await Analyze(BossPull(),
            Info([], AeonaLegendaries.LonesomeSong),
            Activation(1_000, Spells.Oblivion),
            ApplyBuff(1_000, Spells.ConvergingTimelinesLonesomeSong));

        var tracker = parser.ConvergingTimelinesTracker.ShouldNotBeNull();
        tracker.Applications.ShouldBe(1);
        tracker.OblivionApplications.ShouldBe(1);
        tracker.Windows.ShouldHaveSingleItem().Start.ShouldBe(1_000);
        ConvergingTimelinesAcceleration(parser).ShouldBe(2.0, 0.0001);
    }

    [Fact]
    public async Task ALonesomeSongWindow_RevokesItsTermOnRemoval()
    {
        var parser = await Analyze(BossPull(),
            Info([], AeonaLegendaries.LonesomeSong),
            Activation(1_000, Spells.Oblivion),
            ApplyBuff(1_000, Spells.ConvergingTimelinesLonesomeSong),
            RemoveBuff(1_500, Spells.ConvergingTimelinesLonesomeSong),
            Activation(5_000, Spells.TimeShard));

        var tracker = parser.ConvergingTimelinesTracker.ShouldNotBeNull();
        tracker.Windows.ShouldHaveSingleItem().ShouldBe(new AuraWindow(1_000, 1_500));
        ConvergingTimelinesAcceleration(parser).ShouldBe(0.0, 0.0001);
    }

    [Fact]
    public async Task ALonesomeSongRefresh_KeepsOneTermAndExtendsTheWindow()
    {
        var parser = await Analyze(BossPull(),
            Info([], AeonaLegendaries.LonesomeSong),
            Activation(1_000, Spells.Oblivion),
            ApplyBuff(1_000, Spells.ConvergingTimelinesLonesomeSong),
            Activation(1_200, Spells.Oblivion),
            RefreshBuff(1_200, Spells.ConvergingTimelinesLonesomeSong),
            RemoveBuff(1_700, Spells.ConvergingTimelinesLonesomeSong),
            Activation(5_000, Spells.TimeShard));

        var tracker = parser.ConvergingTimelinesTracker.ShouldNotBeNull();
        tracker.Applications.ShouldBe(2);
        tracker.Windows.ShouldHaveSingleItem().ShouldBe(new AuraWindow(1_000, 1_700));
        ConvergingTimelinesAcceleration(parser).ShouldBe(0.0, 0.0001);
    }

    [Fact]
    public async Task ALonesomeSongRefresh_LeavesOneTermOfTwo()
    {
        var parser = await Analyze(BossPull(),
            Info([], AeonaLegendaries.LonesomeSong),
            Activation(1_000, Spells.Oblivion),
            ApplyBuff(1_000, Spells.ConvergingTimelinesLonesomeSong),
            Activation(1_200, Spells.Oblivion),
            RefreshBuff(1_200, Spells.ConvergingTimelinesLonesomeSong));

        ConvergingTimelinesAcceleration(parser).ShouldBe(2.0, 0.0001);
    }

    [Fact]
    public async Task BothEffectsActive_SumToAnAccelerationOfThree()
    {
        var parser = await Analyze(BossPull(),
            Info([], AeonaLegendaries.LonesomeSong),
            Activation(1_000, Spells.AmendFate, TankId),
            ApplyBuff(1_000, Spells.ConvergingTimelines),
            Activation(1_200, Spells.Oblivion),
            ApplyBuff(1_200, Spells.ConvergingTimelinesLonesomeSong));

        ConvergingTimelinesAcceleration(parser).ShouldBe(3.0, 0.0001);
    }

    [Fact]
    public async Task RemovingTheCleanseEffect_KeepsTheLonesomeSongTerm()
    {
        var parser = await Analyze(BossPull(),
            Info([], AeonaLegendaries.LonesomeSong),
            Activation(1_000, Spells.AmendFate, TankId),
            ApplyBuff(1_000, Spells.ConvergingTimelines),
            Activation(1_200, Spells.Oblivion),
            ApplyBuff(1_200, Spells.ConvergingTimelinesLonesomeSong),
            RemoveBuff(1_500, Spells.ConvergingTimelines));

        var tracker = parser.ConvergingTimelinesTracker.ShouldNotBeNull();
        tracker.CleanseApplications.ShouldBe(1);
        tracker.OblivionApplications.ShouldBe(1);
        tracker.Windows.Count.ShouldBe(2);
        tracker.Windows[0].ShouldBe(new AuraWindow(1_000, 1_500));
        tracker.Windows[1].Start.ShouldBe(1_200);
        ConvergingTimelinesAcceleration(parser).ShouldBe(2.0, 0.0001);
    }

    private static double ConvergingTimelinesAcceleration(CombatLogParser parser) =>
        parser.GetModule<StatTracker>()!.CurrentCooldownAcceleration(null) - parser.SelectedCombatant.Stats.CooldownAcceleration.Total(null);
}
