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
}
