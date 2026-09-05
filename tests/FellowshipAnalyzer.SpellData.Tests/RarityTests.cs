using FellowshipAnalyzer.SpellData;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class RarityTests
{
    [Fact]
    public void Run_TakesEveryRungTheExportDeclares()
    {
        var result = MergeEngine.Run(MergeInputs.Load());

        result.Rarities[0].ShouldBe("Common");
        result.Rarities[6].ShouldBe("Legendary");
    }

    [Fact]
    public void Run_TakesTheStoredNameRatherThanThePrintedOne()
    {
        var result = MergeEngine.Run(MergeInputs.Load());

        result.Rarities[4].ShouldBe("Champion");
        result.Rarities[5].ShouldBe("Heroic");
    }

    [Fact]
    public void Serialize_WritesRaritiesSectionSortedByTier()
    {
        var json = SpellDbWriter.Serialize(MergeEngine.Run(MergeInputs.Load()));

        var rarities = System.Text.Json.Nodes.JsonNode.Parse(json)!
            .AsObject()[SpellDbWriter.RaritiesSection]!
            .AsObject();

        var tiers = rarities.Select(pair => int.Parse(pair.Key)).ToList();
        tiers.ShouldBe([.. tiers.Order()]);
    }

    [Fact]
    public void Deserialize_RoundTripsTheRaritiesSection()
    {
        var original = MergeEngine.Run(MergeInputs.Load());
        var restored = SpellDbWriter.Deserialize(SpellDbWriter.Serialize(original));

        restored.Rarities.ShouldBe(original.Rarities);
    }
}
