using FellowshipAnalyzer.SpellData;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class SpellDbWriterTests
{
    [Fact]
    public void Serialize_IsStableAndRoundTrips()
    {
        var result = MergeEngine.Run(MergeInputs.Load());
        var json1 = SpellDbWriter.Serialize(result);
        var json2 = SpellDbWriter.Serialize(SpellDbWriter.Deserialize(json1));
        json1.ShouldBe(json2);
        json1.ShouldContain("\"BurstingIce\"");
        json1.ShouldContain("\"id\": 1031");
    }
}
