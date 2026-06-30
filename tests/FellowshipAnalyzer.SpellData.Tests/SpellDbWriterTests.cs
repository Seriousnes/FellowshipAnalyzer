using System.Text.Json;
using FellowshipAnalyzer.Core.Common.Spells;
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
        var result = new MergeResult([
            new CuratedSpell("hero", "", new Spell { Id = 1, Name = "", Icon = "" }, Provenance.Empty),
            new CuratedSpell("hero", "123Invalid", new Spell { Id = 2, Name = "name", Icon = "icon" }, Provenance.Empty),
            new CuratedSpell("hero", "Valid", new Spell { Id = 3, Name = "name", Icon = "icon" }, Provenance.Empty),
        ], []);

        var json = SpellDbWriter.Serialize(result);

        json.ShouldNotContain("\"\":");
        json.ShouldNotContain("\"123Invalid\"");
        json.ShouldContain("\"Valid\"");
        Should.NotThrow(() => JsonDocument.Parse(json));
    }
}
