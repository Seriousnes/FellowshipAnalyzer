using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Sources;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class SchoolTests
{
    [Theory]
    [InlineData("Physical", MagicSchool.Physical)]
    [InlineData("Magic", MagicSchool.Magic)]
    [InlineData("Magic/Physical", MagicSchool.Magic | MagicSchool.Physical)]
    [InlineData("physical", MagicSchool.Physical)]
    public void ParseSchool_ReadsEveryFormTheDumpWrites(string text, MagicSchool expected) =>
        SpellDataSource.ParseSchool(text).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseSchool_UnclassifiedIsDefault(string? text) =>
        SpellDataSource.ParseSchool(text).ShouldBe(default);

    [Theory]
    [InlineData("Chaos")]
    [InlineData("Magic/Chaos")]
    public void ParseSchool_ThrowsOnASchoolTheEnumDoesNotName(string text) =>
        Should.Throw<ArgumentException>(() => SpellDataSource.ParseSchool(text));

    [Fact]
    public void SpellData_CarriesSchoolOnAbilitiesAndEffects()
    {
        var source = SpellDataSource.Load(SourcePaths.SpellData);

        source.Abilities[2190].School.ShouldBe(MagicSchool.Physical);
        source.Effects[3047].School.ShouldBe(MagicSchool.Physical);
        source.Effects[3005].School.ShouldBe(MagicSchool.Magic);
        source.Abilities[255].School.ShouldBe(MagicSchool.Magic | MagicSchool.Physical);
        source.Abilities[2187].School.ShouldBe(default);
    }

    [Fact]
    public void Merge_KeysSchoolsByFslIdAcrossBothSections()
    {
        var schools = MergeEngine.Run(MergeInputs.Load()).Schools;

        schools[2190].ShouldBe(MagicSchool.Physical);
        schools[1_003_047].ShouldBe(MagicSchool.Physical);
        schools[1_003_005].ShouldBe(MagicSchool.Magic);
        schools[255].ShouldBe(MagicSchool.Magic | MagicSchool.Physical);
    }

    [Theory]
    [InlineData(2031, MagicSchool.Magic)]
    [InlineData(2113, MagicSchool.Magic)]
    [InlineData(2116, MagicSchool.Magic)]
    [InlineData(2187, MagicSchool.Physical)]
    public void Merge_AppliesCuratedSchoolsOverAnUnclassifiedEntry(int fslId, MagicSchool expected) =>
        MergeEngine.Run(MergeInputs.Load()).Schools[fslId].ShouldBe(expected);

    [Fact]
    public void Overrides_AddAnUnclassifiedEnemyAbilityAsAnOrdinarySharedEntry()
    {
        var overrides = OverridesSource.FromInline("""
            { "shared": { "SanguineClaws": { "id": 2187, "school": "Physical" } } }
            """);

        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });
        var spell = result.Spells.Single(s => s.Scope == "shared" && s.Member == "SanguineClaws");

        spell.Spell.School.ShouldBe(MagicSchool.Physical);
        spell.Spell.Name.ShouldBe("Sanguine Claws");
        result.Schools[2187].ShouldBe(MagicSchool.Physical);
    }

    [Fact]
    public void Overrides_SpellSchoolWinsOverTheDump()
    {
        var overrides = OverridesSource.FromInline("""
            { "shared": { "EnemyAttack": { "id": 2190, "school": "Magic" } } }
            """);

        MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides })
            .Schools[2190].ShouldBe(MagicSchool.Magic);
    }

    /// <summary>
    /// A curated <c>school</c> is a <see cref="MagicSchool"/> in .NET flags notation, so a spell that
    /// deals both schools is written <c>"Magic, Physical"</c>. The flat map keeps the dump's own
    /// <c>Magic/Physical</c> spelling; the two notations are read by different parsers.
    /// </summary>
    [Fact]
    public void Overrides_PatchingASchoolOntoAHeroSpellReachesTheMap()
    {
        var overrides = OverridesSource.FromInline("""
            { "rime": { "FreezingTorrent": { "school": "Magic, Physical" } } }
            """);

        var result = MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides });
        var spell = result.Spells.Single(s => s.Scope == "rime" && s.Member == "FreezingTorrent");

        spell.Spell.School.ShouldBe(MagicSchool.Magic | MagicSchool.Physical);
        result.Schools[spell.FSLID.Value].ShouldBe(MagicSchool.Magic | MagicSchool.Physical);
    }

    [Fact]
    public void Spell_LeavesSchoolUnsetWhenTheDumpNeedsNoCuration() =>
        MergeEngine.Run(MergeInputs.Load()).Spells
            .Single(s => s.Scope == "rime" && s.Member == "BurstingIce")
            .Spell.School.ShouldBeNull();

    [Fact]
    public void Serialize_RoundTripsSpellSchoolOnACuratedEntry()
    {
        var original = MergeEngine.Run(MergeInputs.Load());
        var json = SpellDbWriter.Serialize(original);

        json.ShouldContain("\"school\": \"Physical\"");

        SpellDbWriter.Deserialize(json).Spells
            .Single(s => s.Scope == "shared" && s.Member == "SanguineClaws")
            .Spell.School.ShouldBe(MagicSchool.Physical);
    }

    [Fact]
    public void Serialize_WritesSchoolsSectionSortedById()
    {
        var json = SpellDbWriter.Serialize(MergeEngine.Run(MergeInputs.Load()));

        json.ShouldContain("\"schools\"");
        json.ShouldContain("\"2187\": \"Physical\"");
        json.ShouldContain("\"255\": \"Magic/Physical\"");

        var ids = System.Text.RegularExpressions.Regex
            .Matches(json[json.IndexOf("\"schools\"", StringComparison.Ordinal)..], "\"(\\d+)\": \"")
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToList();
        ids.ShouldBe(ids.Order().ToList());
    }

    [Fact]
    public void Deserialize_RoundTripsTheSchoolsSection()
    {
        var original = MergeEngine.Run(MergeInputs.Load());
        var restored = SpellDbWriter.Deserialize(SpellDbWriter.Serialize(original));

        restored.Schools.Count.ShouldBe(original.Schools.Count);
        restored.Schools[255].ShouldBe(MagicSchool.Magic | MagicSchool.Physical);
        restored.Schools[2187].ShouldBe(MagicSchool.Physical);
    }
}
