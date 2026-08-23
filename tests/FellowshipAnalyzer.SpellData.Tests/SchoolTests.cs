using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Sources;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public partial class SchoolTests
{
    [Theory]
    [InlineData("Physical", MagicSchool.Physical)]
    [InlineData("Magic", MagicSchool.Magic)]
    [InlineData("Magic/Physical", MagicSchool.Magic | MagicSchool.Physical)]
    [InlineData("physical", MagicSchool.Physical)]
    public void ParseSchool_ReadsEveryFormSpellDbWrites(string text, MagicSchool expected) =>
        Schools.Parse(text).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseSchool_UnclassifiedIsDefault(string? text) =>
        Schools.Parse(text).ShouldBe(default);

    [Theory]
    [InlineData("Chaos")]
    [InlineData("Magic/Chaos")]
    public void ParseSchool_ThrowsOnASchoolTheEnumDoesNotName(string text) =>
        Should.Throw<ArgumentException>(() => Schools.Parse(text));

    [Theory]
    [InlineData(new[] { "Physical" }, MagicSchool.Physical)]
    [InlineData(new[] { "Magic / Fire" }, MagicSchool.Magic)]
    [InlineData(new[] { "Physical / Bleed" }, MagicSchool.Physical)]
    [InlineData(new[] { "Magic / Fire", "Magic / Frost" }, MagicSchool.Magic)]
    [InlineData(new[] { "Magic / Frost", "Physical" }, MagicSchool.Magic | MagicSchool.Physical)]
    [InlineData(new string[0], default(MagicSchool))]
    public void FromExport_MapsEachEntryOntoItsLeadingSchool(string[] schools, MagicSchool expected) =>
        Schools.FromExport([.. schools]).ShouldBe(expected);

    [Fact]
    public void FromExport_ThrowsOnASchoolThatIsNeitherMagicNorPhysical() =>
        Should.Throw<InvalidOperationException>(() => Schools.FromExport(["Chaos / Void"]));

    [Fact]
    public void Export_HasSchoolOnAbilitiesAndEffects()
    {
        var export = ExportSource.Load(SourcePaths.Entities, SourcePaths.Settings);

        Schools.FromExport(export.Abilities[2190].Schools).ShouldBe(MagicSchool.Physical);
        Schools.FromExport(export.Effects[3005].Schools).ShouldBe(MagicSchool.Magic);
        Schools.FromExport(export.Abilities[255].Schools).ShouldBe(MagicSchool.Magic | MagicSchool.Physical);
        Schools.FromExport(export.Abilities[2187].Schools).ShouldBe(default);
    }

    [Fact]
    public void Merge_KeysSchoolsByFslIdAcrossBothRecordTypes()
    {
        var schools = MergeEngine.Run(MergeInputs.Load()).Schools;

        schools[2190].ShouldBe(MagicSchool.Physical);
        schools[1_000_249].ShouldBe(MagicSchool.Magic | MagicSchool.Physical);
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
    public void Overrides_SpellSchoolWinsOverTheExport()
    {
        var overrides = OverridesSource.FromInline("""
            { "shared": { "EnemyAttack": { "id": 2190, "school": "Magic" } } }
            """);

        MergeEngine.Run(MergeInputs.Load() with { Overrides = overrides })
            .Schools[2190].ShouldBe(MagicSchool.Magic);
    }

    /// <summary>
    /// A curated <c>school</c> is a <see cref="MagicSchool"/> in .NET flags notation, so a spell that
    /// deals both schools is written <c>"Magic, Physical"</c>. The flat map keeps spelldb.json's own
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
    public void Spell_LeavesSchoolUnsetWhenTheExportNeedsNoCuration() =>
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

        var ids = MyRegex().Matches(json[json.IndexOf("\"schools\"", StringComparison.Ordinal)..])
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToList();
        ids.ShouldBe([.. ids.Order()]);
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

    [System.Text.RegularExpressions.GeneratedRegex("\"(\\d+)\": \"")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}
