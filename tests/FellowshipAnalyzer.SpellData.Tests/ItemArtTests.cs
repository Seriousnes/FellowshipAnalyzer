using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Sources;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class ItemArtTests
{
    [Fact]
    public void Load_PartitionsArtByWhetherTheBuildDrawsItPerRung()
    {
        var icons = IconSource.Load(SourcePaths.Entities);

        icons.ArtSharedAcrossRungs.ShouldContain("Tex_bracers_09_b");
        icons.ArtSharedAcrossRungs.ShouldContain("T_Icons_Gems_Sapphire3");
        icons.ArtSharedAcrossRungs.ShouldNotContain("Icon_Rime_ArcticOwl_Head_R1_T0");
    }

    [Fact]
    public void Load_NamesArtWithoutItsRungOrExtension()
    {
        var icons = IconSource.Load(SourcePaths.Entities);

        icons.ArtSharedAcrossRungs.ShouldAllBe(art => !art.EndsWith(".png"));
        icons.ArtSharedAcrossRungs.ShouldAllBe(art => !art.Contains('-'));
    }

    [Fact]
    public void Serialize_WritesArtSharedAcrossRungsInOrdinalOrder()
    {
        var json = SpellDbWriter.Serialize(MergeEngine.Run(MergeInputs.Load()));

        var art = System.Text.Json.Nodes.JsonNode.Parse(json)!
            .AsObject()[SpellDbWriter.ArtSharedAcrossRungsSection]!
            .AsArray()
            .Select(node => node!.GetValue<string>())
            .ToList();

        art.ShouldBe([.. art.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public void Deserialize_RoundTripsArtSharedAcrossRungs()
    {
        var original = MergeEngine.Run(MergeInputs.Load());
        var restored = SpellDbWriter.Deserialize(SpellDbWriter.Serialize(original));

        restored.ArtSharedAcrossRungs.ShouldBe(original.ArtSharedAcrossRungs);
    }
}
