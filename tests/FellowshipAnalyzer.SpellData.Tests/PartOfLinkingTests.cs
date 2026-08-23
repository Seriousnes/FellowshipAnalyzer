using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class PartOfLinkingTests
{
    private static ExportSource Export() => ExportSource.Load(SourcePaths.Entities, SourcePaths.Settings);

    [Fact]
    public void EffectIsLinkedToTheAbilityItIsPartOf()
    {
        var burstingIceDamage = Export().Effects[1396];
        burstingIceDamage.PartOf!.Id.ShouldBe(1031);
        burstingIceDamage.PartOf.Name.ShouldBe("Bursting Ice");
        burstingIceDamage.Role.ShouldBe("Damage");
    }

    [Fact]
    public void EffectMemberIsTheAbilityMemberPlusItsRole() =>
        MergeEngine.Run(MergeInputs.Load()).Spells
            .ShouldContain(s => s.Scope == "rime" && s.Member == "BurstingIceDamage" && s.Spell.Id == 1396);

    [Fact]
    public void EveryEffectLinkedToAnAbility_HasARole()
    {
        var linked = Export().Effects.Values.Where(e => e.PartOf is { Type: "ability" }).ToList();

        linked.ShouldNotBeEmpty();
        linked.ShouldAllBe(e => e.Role != null && e.Role.Length > 0);
    }

    [Fact]
    public void NoLinkedEffect_IsDroppedForWantingARole() =>
        MergeEngine.Run(MergeInputs.Load() with { Overrides = OverridesSource.FromInline("{}") })
            .Gaps.ShouldNotContain(g => g.Kind == GapKind.UnresolvedEffect);

    [Fact]
    public void KitAbilityWithoutAName_IsSkippedAndRaisedAsAGap()
    {
        var nameless = Export().Abilities[1005];
        nameless.Name.ShouldBeNull();
        nameless.Heroes.ShouldContain("Aeona");

        var result = MergeEngine.Run(MergeInputs.Load());
        result.Gaps.ShouldContain(g => g.Scope == "aeona" && g.Kind == GapKind.MissingName
                                       && g.Member == "ability 1005");
        result.Spells.ShouldNotContain(s => s.Scope == "aeona" && s.Spell.Id == 1005);
    }
}
