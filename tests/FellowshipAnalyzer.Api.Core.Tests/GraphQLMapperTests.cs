using FellowshipAnalyzer.Api.Core;
using FellowshipAnalyzer.Api.GraphQL;
using FellowshipAnalyzer.Core.Events;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Api.Core.Tests;

public class GraphQLMapperTests
{
    private sealed record StubAbility(double? GameID, string? Name, string? Icon, string? Type)
        : IGetReportMasterData_ReportData_Report_MasterData_Abilities;

    [Fact]
    public void MapAbility_CarriesIdentityFromMasterData()
    {
        var mapped = new GraphQLMapper().MapAbility(new StubAbility(2190, "Attack", "icon.jpg", "1"));

        mapped.FSLID.Value.ShouldBe(2190);
        mapped.Name.ShouldBe("Attack");
        mapped.Icon.ShouldBe("icon.jpg");
    }

    /// <summary>
    /// The report's <c>type</c> field does not encode a damage school: the same value covers physical
    /// and magic abilities alike. The mapper therefore ignores it whatever it holds, and the school
    /// comes from <c>data/spelldb.json</c> at compile time.
    /// </summary>
    [Theory]
    [InlineData("1")]
    [InlineData("1024")]
    [InlineData("not-a-number")]
    [InlineData("")]
    [InlineData(null)]
    public void MapAbility_IgnoresTheTypeField(string? type)
    {
        var mapped = new GraphQLMapper().MapAbility(new StubAbility(2190, "Attack", "icon.jpg", type));

        mapped.FSLID.Value.ShouldBe(2190);
        mapped.Name.ShouldBe("Attack");
    }
}
