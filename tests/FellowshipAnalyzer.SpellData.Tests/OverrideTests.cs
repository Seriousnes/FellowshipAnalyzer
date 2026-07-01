using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class OverrideTests
{
    [Fact]
    public void Add_Item_EnrichesFromGearById()
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
        spell.Provenance.For("cooldown").ShouldBe(ProvenanceSource.HeroData);
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
}
