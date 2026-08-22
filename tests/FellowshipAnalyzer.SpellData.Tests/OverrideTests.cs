using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.UI;
using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class OverrideTests
{
    [Fact]
    public void Add_Item_EnrichesFromTheExportById()
    {
        var overrides = OverridesSource.FromInline("""
            { "items": { "VoidbringerTouch": { "id": 155 } } }
            """);
        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });
        var v = result.Spells.Single(x => x.Scope == "items" && x.Member == "VoidbringerTouch");
        v.Spell.Id.ShouldBe(155);
        v.Spell.Cooldown.ShouldBe(90);
        v.Spell.Range.ShouldBe(30);
    }

    [Fact]
    public void Patch_OverridesScalar()
    {
        var overrides = OverridesSource.FromInline("""
            { "rime": { "FreezingTorrent": { "channelTickInterval": 0.5 } } }
            """);
        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });
        result.Spells.Single(x => x.Scope == "rime" && x.Member == "FreezingTorrent")
              .Spell.ChannelTickInterval.ShouldBe(0.5);
    }

    [Fact]
    public void Add_WithoutId_IsFlaggedAsGap()
    {
        var overrides = OverridesSource.FromInline("""
            { "items": { "Mystery": { "name": "Mystery" } } }
            """);
        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });
        result.Gaps.ShouldContain(g => g.Member == "Mystery" && g.Kind == GapKind.MissingId);
    }

    [Fact]
    public void Patch_ProvenanceRecordsOverrideSource()
    {
        var overrides = OverridesSource.FromInline("""
            { "rime": { "FreezingTorrent": { "channelTickInterval": 0.5 } } }
            """);
        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });
        var spell = result.Spells.Single(x => x.Scope == "rime" && x.Member == "FreezingTorrent");
        spell.Provenance.For("channelTickInterval").ShouldBe(ProvenanceSource.Override);
        spell.Provenance.For("cooldown").ShouldBe(ProvenanceSource.Export);
    }

    [Fact]
    public void Patch_SetsAbilityCategory()
    {
        var overrides = OverridesSource.FromInline("""
            { "rime": { "FreezingTorrent": { "abilityCategory": "Control" } } }
            """);
        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });
        var spell = result.Spells.Single(x => x.Scope == "rime" && x.Member == "FreezingTorrent");
        spell.Spell.AbilityCategory.ShouldBe(AbilityCategory.Control);
        spell.Provenance.For("abilityCategory").ShouldBe(ProvenanceSource.Override);
    }

    [Fact]
    public void Add_Ability_InheritsTheExportAbilityCategory()
    {
        var overrides = OverridesSource.FromInline("""
            { "rime": { "WintersBlessing": { "id": 1026 } } }
            """);
        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });
        var spell = result.Spells.Single(x => x.Scope == "rime" && x.Member == "WintersBlessing");
        spell.Spell.AbilityCategory.ShouldBe(AbilityCategory.Major);
        spell.Provenance.For("abilityCategory").ShouldBe(ProvenanceSource.Export);
    }

    [Fact]
    public void Add_Effect_DoesNotInheritCategoryFromAbilityOfSameNativeId()
    {
        var overrides = OverridesSource.FromInline("""
            { "rime": { "WintersBlessingBuff": { "id": 1026, "kind": "effect" } } }
            """);
        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });
        result.Spells.Single(x => x.Scope == "rime" && x.Member == "WintersBlessingBuff")
              .Spell.AbilityCategory.ShouldBeNull();
    }

    [Fact]
    public void Override_WithId_SupersedesAutoSelectedMemberOfSameGuid()
    {
        var overrides = OverridesSource.FromInline("""
            { "rime": { "RenamedBurst": { "id": 1031 } } }
            """);
        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });

        var rimeWithGuid1031 = result.Spells.Where(s => s.Scope == "rime" && s.FSLID.Value == 1031).ToList();
        rimeWithGuid1031.Count.ShouldBe(1);
        rimeWithGuid1031[0].Member.ShouldBe("RenamedBurst");
        result.Spells.ShouldNotContain(s => s.Scope == "rime" && s.Member == "BurstingIce");
        result.Spells.ShouldContain(s => s.Scope == "rime" && s.Member == "FreezingTorrent");
    }

    [Fact]
    public void SoleHero_RemovesTheAbilityFromEveryOtherScope()
    {
        var overrides = OverridesSource.FromInline("""
            { "helena": { "IronWall": { "id": 973, "soleHero": true } } }
            """);
        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });

        result.Spells.ShouldContain(s => s.Scope == "helena" && s.Member == "IronWall" && s.FSLID.Value == 973);
        result.Spells.ShouldNotContain(s => s.Scope == "xavian" && s.FSLID.Value == 973);
        result.Spells.Count(s => s.FSLID.Value == 973).ShouldBe(1);
    }

    [Fact]
    public void SoleHero_RemovesTheLinkedEffectsFromEveryOtherScope()
    {
        var overrides = OverridesSource.FromInline("""
            { "helena": { "IronWall": { "id": 973, "soleHero": true } } }
            """);
        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });

        var ironWallBuff = FSLID.FromNative(SpellKind.Effect, 1254).Value;
        result.Spells.ShouldContain(s => s.Scope == "helena" && s.Member == "IronWallBuff" && s.FSLID.Value == ironWallBuff);
        result.Spells.ShouldNotContain(s => s.Scope == "xavian" && s.FSLID.Value == ironWallBuff);
        result.Spells.Count(s => s.FSLID.Value == ironWallBuff).ShouldBe(1);
    }

    [Fact]
    public void SoleHero_WithoutAnId_TakesTheIdFromTheMemberItPatches()
    {
        var overrides = OverridesSource.FromInline("""
            { "helena": { "IronWall": { "soleHero": true } } }
            """);
        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });

        var ironWallBuff = FSLID.FromNative(SpellKind.Effect, 1254).Value;
        result.Spells.ShouldContain(s => s.Scope == "helena" && s.Member == "IronWall" && s.FSLID.Value == 973);
        result.Spells.ShouldNotContain(s => s.Scope == "xavian" && s.FSLID.Value == 973);
        result.Spells.ShouldContain(s => s.Scope == "helena" && s.Member == "IronWallBuff" && s.FSLID.Value == ironWallBuff);
        result.Spells.ShouldNotContain(s => s.Scope == "xavian" && s.FSLID.Value == ironWallBuff);
    }

    [Fact]
    public void SoleHero_OnAnAbilityTheExportAlreadyGivesOneHero_ChangesNothing()
    {
        var overrides = OverridesSource.FromInline("""
            { "rime": { "FreezingTorrent": { "id": 1027, "soleHero": true } } }
            """);
        var claimed = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });
        var unclaimed = MergeEngine.Run(MergeInputs.Load() with { Overrides = OverridesSource.FromInline("{}") });

        claimed.Spells.ShouldContain(s => s.Scope == "rime" && s.Member == "FreezingTorrent" && s.FSLID.Value == 1027);
        claimed.Spells.ShouldContain(s => s.Scope == "rime" && s.Member == "FreezingTorrentTickDamage");
        claimed.Spells.ShouldContain(s => s.Scope == "rime" && s.Member == "FreezingTorrentAoeDamage");
        claimed.Spells
            .Select(s => (s.Scope, s.Member, s.FSLID.Value))
            .ShouldBe(unclaimed.Spells.Select(s => (s.Scope, s.Member, s.FSLID.Value)));
    }
}
