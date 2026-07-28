using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Heroes.Helena.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Helena.Spells;

using static FellowshipAnalyzer.Heroes.Helena.Tests.Analysis.HelenaAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Helena.Tests.Analysis;

public sealed class EmpoweredShieldSlamAnalyzerTests
{
    [Fact]
    public async Task AnEmpowermentAShieldSlamSpent_CountsAsConsumed()
    {
        var analyzer = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.ShieldSlamAbsorbBuffSelfBuff),
            Cast(PullStart + 2_000, Spells.ShieldSlam),
            RemoveBuff(PullStart + 2_000, Spells.ShieldSlamAbsorbBuffSelfBuff));

        analyzer.EmpowermentsGranted.ShouldBe(1);
        analyzer.EmpowermentsConsumed.ShouldBe(1);
        analyzer.EmpowermentsExpired.ShouldBe(0);
        analyzer.EmpowermentEfficiency.ShouldBe(1d);
    }

    [Fact]
    public async Task AnEmpowermentThatFellOffUnused_CountsAsExpired()
    {
        var analyzer = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.ShieldSlamAbsorbBuffSelfBuff),
            RemoveBuff(PullStart + 13_000, Spells.ShieldSlamAbsorbBuffSelfBuff));

        analyzer.EmpowermentsGranted.ShouldBe(1);
        analyzer.EmpowermentsConsumed.ShouldBe(0);
        analyzer.EmpowermentsExpired.ShouldBe(1);
        analyzer.EmpowermentEfficiency.ShouldBe(0);
    }

    [Fact]
    public async Task AShieldSlamCastOutsideAnEmpowerment_DoesNotConsumeOne()
    {
        var analyzer = await Analyze(
            Cast(PullStart + 500, Spells.ShieldSlam),
            ApplyBuff(PullStart + 1_000, Spells.ShieldSlamAbsorbBuffSelfBuff),
            RemoveBuff(PullStart + 13_000, Spells.ShieldSlamAbsorbBuffSelfBuff));

        analyzer.EmpowermentsExpired.ShouldBe(1);
    }

    [Fact]
    public async Task AbsorbConsumedAndAbsorbLeftOnExpiry_AreTrackedSeparately()
    {
        var analyzer = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.ShieldSlamAbsorb),
            Absorbed(PullStart + 2_000, 3_000),
            Absorbed(PullStart + 3_000, 1_000),
            RemoveBuff(PullStart + 13_000, Spells.ShieldSlamAbsorb, absorb: 500));

        analyzer.AbsorbUsed.ShouldBe(4_000);
        analyzer.AbsorbWasted.ShouldBe(500);
        analyzer.BarriersExpiredUnspent.ShouldBe(1);
        analyzer.AbsorbEfficiency.ShouldBe(4_000 / 4_500d, 0.001);
    }

    [Fact]
    public async Task ABarrierConsumedInFull_LeavesNothingToExpire()
    {
        var analyzer = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.ShieldSlamAbsorb),
            Absorbed(PullStart + 2_000, 5_000),
            RemoveBuff(PullStart + 3_000, Spells.ShieldSlamAbsorb, absorb: 0));

        analyzer.AbsorbUsed.ShouldBe(5_000);
        analyzer.AbsorbWasted.ShouldBe(0);
        analyzer.BarriersExpiredUnspent.ShouldBe(0);
        analyzer.AbsorbEfficiency.ShouldBe(1d);
    }

    [Fact]
    public async Task ABarrierRefreshedWhileUp_StaysOneWindow()
    {
        var analyzer = await Analyze(
            ApplyBuff(PullStart + 1_000, Spells.ShieldSlamAbsorb),
            new RefreshBuffEvent
            {
                Timestamp = PullStart + 5_000,
                SourceId = PlayerId,
                TargetId = PlayerId,
                Ability = new Core.Events.Ability { FSLID = Spells.ShieldSlamAbsorb.FSLID },
                AbilityGameId = Spells.ShieldSlamAbsorb.FSLID,
            },
            RemoveBuff(PullStart + 17_000, Spells.ShieldSlamAbsorb, absorb: 100));

        analyzer.BarrierWindows.ShouldBe(1);
        analyzer.Barriers.ShouldHaveSingleItem().DurationMs.ShouldBe(16_000);
    }

    [Fact]
    public async Task APullWithNoEmpowerment_ReadsZeroRatherThanThrowing()
    {
        var analyzer = await Analyze(Cast(PullStart + 1_000, Spells.ShieldSlam));

        analyzer.EmpowermentsGranted.ShouldBe(0);
        analyzer.BarrierWindows.ShouldBe(0);
        analyzer.AbsorbEfficiency.ShouldBe(0);
        analyzer.EmpowermentEfficiency.ShouldBe(0);
    }

    private static AbsorbedEvent Absorbed(int timestamp, long amount) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = PlayerId,
        Ability = new Core.Events.Ability { FSLID = Spells.ShieldSlamAbsorb.FSLID },
        AbilityGameId = Spells.ShieldSlamAbsorb.FSLID,
        Amount = amount,
    };

    private static async Task<EmpoweredShieldSlamAnalyzer> Analyze(params Event[] events)
    {
        var parser = await HelenaAnalysisFixture.Analyze(events);
        return parser.EmpoweredShieldSlamAnalyzers.ShouldHaveSingleItem().Analyzer;
    }
}
