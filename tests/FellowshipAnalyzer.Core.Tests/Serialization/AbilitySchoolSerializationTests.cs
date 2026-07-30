using System.Text.Json;
using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.Core.Serialization;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Serialization;

/// <summary>
/// <see cref="MagicSchool"/> names no member for zero, and <see cref="Ability"/> rides inside the
/// blob-cached <see cref="AnalysisPreload"/>, so an unclassified ability crosses the API boundary
/// carrying a flags value with no name. These cover that crossing.
/// </summary>
public sealed class AbilitySchoolSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();
    private static readonly FellowshipAnalyzerJsonContext JsonContext = new(JsonOptions);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerOptions.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowOutOfOrderMetadataProperties = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
        return options;
    }

    private static AnalysisPreload Preload(params Ability[] abilities) =>
        new(new ReportInfo("code", "title", 0, 1, [], []), new ReportMasterData(abilities, []));

    [Theory]
    [InlineData((MagicSchool)0)]
    [InlineData(MagicSchool.Physical)]
    [InlineData(MagicSchool.Magic)]
    [InlineData(MagicSchool.Magic | MagicSchool.Physical)]
    [InlineData(MagicSchool.Healing)]
    [InlineData(MagicSchool.Stagger)]
    public void AnalysisPreload_RoundTripsEverySchoolIncludingTheUnnamedZero(MagicSchool school)
    {
        var json = JsonSerializer.Serialize(
            Preload(new Ability { FSLID = 2190, Name = "Attack", Icon = "i.jpg", Type = school }),
            JsonContext.AnalysisPreload);

        var restored = JsonSerializer.Deserialize(json, JsonContext.AnalysisPreload).ShouldNotBeNull();

        restored.MasterData.Abilities.Single().Type.ShouldBe(school);
    }

    [Fact]
    public void AnalysisPreload_CarriesTheAbilityIdentityAlongsideTheSchool()
    {
        var json = JsonSerializer.Serialize(
            Preload(new Ability { FSLID = 2190, Name = "Attack", Icon = "i.jpg", Type = MagicSchool.Physical }),
            JsonContext.AnalysisPreload);

        var ability = JsonSerializer.Deserialize(json, JsonContext.AnalysisPreload)
            .ShouldNotBeNull().MasterData.Abilities.Single();

        ability.FSLID.Value.ShouldBe(2190);
        ability.Name.ShouldBe("Attack");
        ability.Icon.ShouldBe("i.jpg");
        ability.Type.ShouldBe(MagicSchool.Physical);
    }
}
