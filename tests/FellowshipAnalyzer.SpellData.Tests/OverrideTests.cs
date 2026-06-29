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
        v.Id.ShouldBe(155);
        v.Cooldown.ShouldBe(90);
        v.Range.ShouldBe(30);
    }

    [Fact]
    public void Patch_OverridesScalar()
    {
        var overrides = OverridesSource.FromInline("""
            { "rime": { "FreezingTorrent": { "channelTickInterval": 0.5 } } }
            """);
        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });
        result.Spells.Single(x => x.Scope == "rime" && x.Member == "FreezingTorrent")
              .ChannelTickInterval.ShouldBe(0.5);
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
        spell.Provenance.ChannelTickInterval.ShouldBe(ProvenanceSource.Override);
        spell.Provenance.Cooldown.ShouldBe(ProvenanceSource.HeroData);
    }
}
