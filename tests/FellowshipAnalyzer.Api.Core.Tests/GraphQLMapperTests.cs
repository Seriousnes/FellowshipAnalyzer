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

    [Theory]
    [InlineData("1", MagicSchool.Physical)]
    [InlineData("2", MagicSchool.Magic)]
    [InlineData("8", MagicSchool.Healing)]
    [InlineData("1024", MagicSchool.Stagger)]
    [InlineData("3", MagicSchool.Physical | MagicSchool.Magic)]
    public void MapAbility_ReadsTheTypeCodeAsASchool(string type, MagicSchool expected) =>
        new GraphQLMapper().MapAbility(new StubAbility(2190, "Attack", "icon.jpg", type))
            .Type.ShouldBe(expected);

    /// <summary>
    /// The old mapper defaulted an unreadable <c>type</c> to <c>0</c>, which was
    /// <see cref="MagicSchool.Physical"/> at the time and silently passed a physical-damage filter.
    /// Every row here must land on the unresolved default so that filter fails closed instead.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("4")]
    [InlineData("512")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    [InlineData("")]
    [InlineData(null)]
    public void MapAbility_UnreadableOrUnmappedTypeIsUnresolvedRatherThanPhysical(string? type)
    {
        var mapped = new GraphQLMapper().MapAbility(new StubAbility(2190, "Attack", "icon.jpg", type));

        mapped.Type.ShouldBe(default);
        ((mapped.Type & MagicSchool.Physical) != 0).ShouldBeFalse();
    }
}
