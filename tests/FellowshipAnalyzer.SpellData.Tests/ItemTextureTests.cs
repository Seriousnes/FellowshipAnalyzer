using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Sources;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class ItemTextureTests
{
    [Fact]
    public void Load_PartitionsTexturesByWhetherTheBuildDrawsThemPerRung()
    {
        var icons = IconSource.Load(SourcePaths.Entities);

        icons.TexturesSharedAcrossRungs.ShouldContain("Tex_bracers_09_b");
        icons.TexturesSharedAcrossRungs.ShouldContain("T_Icons_Gems_Sapphire3");
        icons.TexturesSharedAcrossRungs.ShouldNotContain("Icon_Rime_ArcticOwl_Head_R1_T0");
    }

    [Fact]
    public void Load_NamesTextureWithoutItsRungOrExtension()
    {
        var icons = IconSource.Load(SourcePaths.Entities);

        icons.TexturesSharedAcrossRungs.ShouldAllBe(texture => !texture.EndsWith(".png"));
        icons.TexturesSharedAcrossRungs.ShouldAllBe(texture => !texture.Contains('-'));
    }

    [Fact]
    public void Serialize_WritesTexturesSharedAcrossRungsInOrdinalOrder()
    {
        var json = SpellDbWriter.Serialize(MergeEngine.Run(MergeInputs.Load()));

        var texture = System.Text.Json.Nodes.JsonNode.Parse(json)!
            .AsObject()[SpellDbWriter.TexturesSharedAcrossRungsSection]!
            .AsArray()
            .Select(node => node!.GetValue<string>())
            .ToList();

        texture.ShouldBe([.. texture.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public void Deserialize_RoundTripsTexturesSharedAcrossRungs()
    {
        var original = MergeEngine.Run(MergeInputs.Load());
        var restored = SpellDbWriter.Deserialize(SpellDbWriter.Serialize(original));

        restored.TexturesSharedAcrossRungs.ShouldBe(original.TexturesSharedAcrossRungs);
    }
}
