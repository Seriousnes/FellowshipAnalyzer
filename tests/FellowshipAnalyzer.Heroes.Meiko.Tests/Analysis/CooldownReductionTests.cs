using FellowshipAnalyzer.Heroes.Meiko.Modules;

using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Meiko.Spells;
using Talents = FellowshipAnalyzer.Core.Common.Spells.Meiko.Talents;

using static FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis.MeikoAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Meiko.Tests.Analysis;

public sealed class CooldownReductionTests
{
    [Fact]
    public async Task GuardiansFluidity_TakesOneSecondOffStoneShieldPerFinisher()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.GuardiansFluidity],
            Cast(PullStart + 1_000, Spells.StoneShieldAlt),
            Cast(PullStart + 1_100, Spells.StoneShieldAlt),
            Cast(PullStart + 2_000, Spells.RisingEarth),
            Cast(PullStart + 3_000, Spells.EarthfistBarrage),
            Cast(PullStart + 4_000, Spells.DoublePalmStrike));

        var analyzer = parser.GetModule<GuardiansFluidityAnalyzer>().ShouldNotBeNull();

        analyzer.Reduction.Total.ShouldBe(3 * GuardiansFluidityAnalyzer.ReductionMs);
        analyzer.Reduction.Effective.ShouldBe(3 * GuardiansFluidityAnalyzer.ReductionMs);
    }

    [Fact]
    public async Task GuardiansFluidity_IsAbsentWithoutTheTalent()
    {
        var parser = await Analyze(Cast(PullStart + 1_000, Spells.RisingEarth));

        parser.GetModule<GuardiansFluidityAnalyzer>().ShouldBeNull();
    }

    [Fact]
    public async Task Earthbourne_TakesAFifthOfTheBaseCooldownOffBulwarkPerShatterEarth()
    {
        var parser = await AnalyzeWithTalents(
            [Talents.Earthbourne],
            Cast(PullStart + 1_000, Spells.TwinSoulsBulwark),
            Cast(PullStart + 2_000, Spells.ShatterEarth));

        var analyzer = parser.GetModule<EarthbourneAnalyzer>().ShouldNotBeNull();

        EarthbourneAnalyzer.ReductionMs.ShouldBe(36_000);
        analyzer.Reduction.Effective.ShouldBe(36_000);
    }
}
