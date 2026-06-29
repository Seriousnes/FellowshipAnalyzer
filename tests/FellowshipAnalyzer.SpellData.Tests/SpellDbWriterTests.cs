using System.Text.Json;
using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;
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

    [Fact]
    public void Serialize_SkipsEmptyAndInvalidMembers()
    {
        var prov = new Provenance();
        var result = new MergeResult([
            new MergedSpell("hero", "", 1, SpellKind.Ability, "", "", null, null, 1, null, null, null, new Dictionary<string, int>(), prov),
            new MergedSpell("hero", "123Invalid", 2, SpellKind.Ability, "name", "icon", null, null, 1, null, null, null, new Dictionary<string, int>(), prov),
            new MergedSpell("hero", "Valid", 3, SpellKind.Ability, "name", "icon", null, null, 1, null, null, null, new Dictionary<string, int>(), prov),
        ], []);

        var json = SpellDbWriter.Serialize(result);

        json.ShouldNotContain("\"\":");
        json.ShouldNotContain("\"123Invalid\"");
        json.ShouldContain("\"Valid\"");
        Should.NotThrow(() => JsonDocument.Parse(json));
    }
}
