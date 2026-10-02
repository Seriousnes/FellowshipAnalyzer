using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Vigour.Spells;

using static FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis.VigourAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class RunicProliferationAnalyzerTests
{
    [Fact]
    public async Task SpendersInsideTheWindowAreCountedWithTheirTargets()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.RunicProliferation, PlayerId, runes: 5),
            ApplyBuff(PullStart + 1_000, Spells.RunicProliferationBuff, PlayerId),
            Cast(PullStart + 2_000, Spells.RuneOfRenewal, TankId),
            ApplyBuff(PullStart + 2_000, Spells.RuneOfRenewalBuff, TankId),
            RefreshBuff(PullStart + 2_000, Spells.RuneOfRenewalBuff, AllyId),
            ApplyBuff(PullStart + 2_001, Spells.RuneOfRenewalBuff, PlayerId),
            Cast(PullStart + 3_000, Spells.LuminousBarrier, TankId),
            ApplyBuff(PullStart + 3_000, Spells.LuminousBarrierAbsorb, TankId),
            RemoveBuff(PullStart + 9_000, Spells.RunicProliferationBuff, PlayerId),
            Cast(PullStart + 10_000, Spells.Soulbrand),
            ApplyDebuff(PullStart + 10_000, Spells.SoulbrandDot));

        var cast = parser.RunicProliferationAnalyzers.ShouldHaveSingleItem().Analyzer.Casts.ShouldHaveSingleItem();

        cast.RunesBefore.ShouldBe(5);
        cast.RunesLost.ShouldBe(2);
        cast.SpenderCount.ShouldBe(2);
        cast.TargetsHit.ShouldBe(4);
        cast.Spenders[0].Targets.ShouldBe(3);
    }

    [Fact]
    public async Task AnAuraLandingAfterTheLinkWindowIsNotCreditedToTheSpender()
    {
        var parser = await Analyze(
            Cast(PullStart + 1_000, Spells.RunicProliferation, PlayerId, runes: 2),
            ApplyBuff(PullStart + 1_000, Spells.RunicProliferationBuff, PlayerId),
            Cast(PullStart + 2_000, Spells.RuneOfRenewal, TankId),
            ApplyBuff(PullStart + 2_000, Spells.RuneOfRenewalBuff, TankId),
            RefreshBuff(PullStart + 4_000, Spells.RuneOfRenewalBuff, AllyId));

        var cast = parser.RunicProliferationAnalyzers.ShouldHaveSingleItem().Analyzer.Casts.ShouldHaveSingleItem();

        cast.RunesLost.ShouldBe(0);
        cast.TargetsHit.ShouldBe(1);
    }
}
