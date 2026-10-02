using Shouldly;

using Xunit;

using Spells = FellowshipAnalyzer.Core.Common.Spells.Vigour.Spells;

using static FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis.VigourAnalysisFixture;

namespace FellowshipAnalyzer.Heroes.Vigour.Tests.Analysis;

public sealed class RadiantBlastAnalyzerTests
{
    [Fact]
    public async Task AvatarOfLightTimeIsNotCountedAsReady()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.AvatarOfLight, PlayerId),
            ApplyBuff(PullStart, Spells.AvatarOfLightBuff, PlayerId),
            Cast(PullStart + 1_000, Spells.RadiantBlast),
            Cast(PullStart + 2_000, Spells.RadiantBlast),
            RemoveBuff(PullStart + 9_000, Spells.AvatarOfLightBuff, PlayerId));

        var analyzer = parser.RadiantBlastAnalyzers.ShouldHaveSingleItem().Analyzer;

        analyzer.CastCount.ShouldBe(2);
        analyzer.AvatarCasts.ShouldBe(2);
        analyzer.ReadyMs.ShouldBeLessThanOrEqualTo(PullDuration - 9_000);
    }

    [Fact]
    public async Task AvatarOfLightEndsTheRadiantBlastAndCircleOfLightCooldowns()
    {
        var parser = await Analyze(
            Cast(PullStart, Spells.RadiantBlast),
            Cast(PullStart, Spells.CircleOfLight, PlayerId),
            Cast(PullStart + 1_000, Spells.AvatarOfLight, PlayerId),
            ApplyBuff(PullStart + 1_000, Spells.AvatarOfLightBuff, PlayerId),
            Cast(PullStart + 2_000, Spells.RadiantBlast),
            RemoveBuff(PullStart + 10_000, Spells.AvatarOfLightBuff, PlayerId));

        parser.AvatarOfLight.ShouldNotBeNull().CooldownsReset.ShouldBe(1);
        parser.SpellUsable.ShouldNotBeNull().IsAvailable(Spells.CircleOfLight.FSLID).ShouldBeTrue();
        parser.SpellUsable.ShouldNotBeNull().IsAvailable(Spells.RadiantBlast.FSLID).ShouldBeTrue();
    }
}
