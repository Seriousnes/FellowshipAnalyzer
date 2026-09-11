using FellowshipAnalyzer.Core.Common.Spells.Aeona;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Modules;

public sealed class StaggerIntakeTests
{
    [Fact]
    public async Task EveryStaggeredHit_IsIntakeOnTheAllyItStruck()
    {
        var parser = await Analyze(BossPull(),
            Info([]),
            Absorbed(1_000, Spells.AuraOfDeferredFate, TankId, 900),
            Absorbed(1_500, Spells.AuraOfDeferredFate, TankId, 1_500),
            Absorbed(2_000, Spells.AuraOfDeferredFate, AllyId, 400));

        var tracker = parser.StaggerTracker.ShouldNotBeNull();
        tracker.IntakeFor(TankId).Select(intake => intake.Amount).ShouldBe([900L, 1_500L]);
        tracker.IntakeBetween(TankId, 0, 2_000).ShouldBe(2_400);
        tracker.IntakeBetween(TankId, 1_200, 2_000).ShouldBe(1_500);
        tracker.IntakeBetween(AllyId, 0, 2_000).ShouldBe(400);
    }

    [Fact]
    public async Task IntakePerSecond_IsTheAmountOverTheLookbackDividedByItsLength()
    {
        var parser = await Analyze(BossPull(),
            Info([]),
            Absorbed(1_000, Spells.AuraOfDeferredFate, TankId, 2_000),
            Absorbed(5_000, Spells.AuraOfDeferredFate, TankId, 4_000));

        var tracker = parser.StaggerTracker.ShouldNotBeNull();
        tracker.IntakePerSecond(TankId, 9_000, 8_000).ShouldBe(750.0, 0.0001);
        tracker.IntakePerSecond(TankId, 6_000, 2_000).ShouldBe(2_000.0, 0.0001);
    }
}
