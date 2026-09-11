using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Common.Spells.Aeona;

using Shouldly;

using Xunit;

using static FellowshipAnalyzer.Heroes.Aeona.Tests.AeonaLog;

using AeonaAbilities = FellowshipAnalyzer.Heroes.Aeona.Modules.Abilities;
using Spells = FellowshipAnalyzer.Core.Common.Spells.Aeona.Spells;

namespace FellowshipAnalyzer.Heroes.Aeona.Tests.Modules;

public sealed class AeonaBuildTests
{
    [Fact]
    public async Task WithMassEntropy_EntropyClaimHasTwoChargesAndTheLongerDuration()
    {
        var parser = await Analyze(BossPull(), Info([], AeonaLegendaries.MassEntropy));

        var build = parser.AeonaBuild.ShouldNotBeNull();
        build.MassEntropy.ShouldBeTrue();
        build.Legendary.ShouldBe(Legendaries.MassEntropy);
        build.EntropyClaimCharges.ShouldBe(2);
        build.EntropyClaimDurationMs.ShouldBe(8_000);
        parser.GetModule<AeonaAbilities>()!.GetMaxCharges(Spells.EntropyClaim.FSLID).ShouldBe(2);
    }

    [Fact]
    public async Task WithoutALegendary_EntropyClaimKeepsItsOwnChargeAndDuration()
    {
        var parser = await Analyze(BossPull(), Info([]));

        var build = parser.AeonaBuild.ShouldNotBeNull();
        build.Legendary.ShouldBeNull();
        build.MassEntropy.ShouldBeFalse();
        build.EntropyClaimCharges.ShouldBe(1);
        build.EntropyClaimDurationMs.ShouldBe(6_000);
        parser.GetModule<AeonaAbilities>()!.GetMaxCharges(Spells.EntropyClaim.FSLID).ShouldBe(1);
    }

    [Fact]
    public async Task WithLonesomeSong_OblivionGrantsTheLargerAcceleration()
    {
        var parser = await Analyze(BossPull(), Info([], AeonaLegendaries.LonesomeSong));

        var build = parser.AeonaBuild.ShouldNotBeNull();
        build.LonesomeSong.ShouldBeTrue();
        build.ConvergingTimelinesOnOblivion.ShouldBe(2.0);
    }
}
