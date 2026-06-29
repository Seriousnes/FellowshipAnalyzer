using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class MergeEngineTests
{
    private static MergeResult Run() => MergeEngine.Run(MergeInputs.Load());

    [Fact]
    public void Rime_BurstingIce_IsSelectedAsAbility()
    {
        var s = Run().Spells.Single(x => x.Scope == "rime" && x.Member == "BurstingIce");
        s.Id.ShouldBe(1031);
        s.Kind.ShouldBe(SpellKind.Ability);
        s.Name.ShouldBe("Bursting Ice");
    }

    [Fact]
    public void Rime_FreezingTorrent_CarriesNormalizedScalars()
    {
        var s = Run().Spells.Single(x => x.Scope == "rime" && x.Member == "FreezingTorrent");
        s.Cooldown.ShouldBe(15);
        s.Range.ShouldBe(30);
        s.ChannelDuration.ShouldBe(2.0);
        s.ChannelTickInterval.ShouldBe(0.4);
    }

    [Fact]
    public void Rime_GlacialBlast_HasWinterOrbCost() =>
        Run().Spells.Single(x => x.Scope == "rime" && x.Member == "GlacialBlast").Costs["winterOrb"].ShouldBe(2);

    [Fact]
    public void LinkedEffect_IsNamedAbilityPlusRole() =>
        Run().Spells.ShouldContain(x => x.Scope == "rime" && x.Kind == SpellKind.Effect && x.Id == 1396);

    [Fact]
    public void Rime_NamedEffectsNotLinkedToAbility_AreEmittedAsGaps() =>
        Run().Gaps.ShouldContain(g => g.Scope == "rime" && g.Kind == GapKind.UnresolvedEffect);

    [Fact]
    public void NonShippedHeroes_AreExcludedFromOutput()
    {
        var result = Run();
        result.Spells.ShouldNotContain(s => s.Scope == "gunde");
        result.Spells.ShouldContain(s => s.Scope == "rime");
    }
}
