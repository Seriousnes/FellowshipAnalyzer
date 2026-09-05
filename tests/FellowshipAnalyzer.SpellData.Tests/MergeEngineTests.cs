using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Game;
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
        s.Spell.Id.ShouldBe(1031);
        s.Kind.ShouldBe(SpellKind.Ability);
        s.Spell.Name.ShouldBe("Bursting Ice");
    }

    [Fact]
    public void Rime_FreezingTorrent_HasNormalizedScalars()
    {
        var s = Run().Spells.Single(x => x.Scope == "rime" && x.Member == "FreezingTorrent");
        s.Spell.Cooldown.ShouldBe(15);
        s.Spell.Range.ShouldBe(3000);
        s.Spell.ChannelDuration.ShouldBe(2.0);
        s.Spell.ChannelTickInterval.ShouldBe(0.4);
    }

    [Fact]
    public void Gunde_Slaughter_HasItsRadiusInGameUnits()
    {
        var s = Run().Spells.Single(x => x.Scope == "gunde" && x.Member == "Slaughter");
        s.Spell.Range.ShouldBe(700);
        s.Spell.Radius.ShouldBe(700);
    }

    [Fact]
    public void Gunde_GrimCarve_HasARangeThatOutrunsItsRadius()
    {
        var s = Run().Spells.Single(x => x.Scope == "gunde" && x.Member == "GrimCarve");
        s.Spell.Range.ShouldBe(3000);
        s.Spell.Radius.ShouldBe(700);
    }

    [Fact]
    public void Rime_GlacialBlast_HasTertiaryCost() =>
        Run().Spells.Single(x => x.Scope == "rime" && x.Member == "GlacialBlast").Spell.Costs[ResourceTypes.Tertiary].ShouldBe(2);

    [Fact]
    public void LinkedEffect_IsNamedAbilityPlusRole() =>
        Run().Spells.ShouldContain(x => x.Scope == "rime" && x.Kind == SpellKind.Effect && x.Spell.Id == 1396);

    [Fact]
    public void EveryEffectLinkedToAnAbility_ResolvesToAMember() =>
        Run().Gaps.ShouldNotContain(g => g.Kind == GapKind.UnresolvedEffect);

    [Fact]
    public void HeroesPresentInData_AreIncludedInOutput()
    {
        var result = Run();
        result.Spells.ShouldContain(s => s.Scope == "gunde");
        result.Spells.ShouldContain(s => s.Scope == "rime");
    }

    [Fact]
    public void EveryDataHeroScope_MapsToAKnownHeroName() =>
        Run().Gaps.ShouldNotContain(g => g.Kind == GapKind.UnknownScope);
}
